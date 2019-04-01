using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Globalization;
using System.Text;
using UnityEditor.Graphing;
using UnityEditor.Graphing.Util;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor.ShaderGraph;
using UnityEditor.VFX;

namespace UnityEditor.Experimental.Rendering.HDPipeline.VFXSG
{
    //Small methods usable by the VFX since they are public
    public static class VFXSGShaderGenerator
    {

        public static List<string> GetPropertiesExcept(Graph graph, List<string> attributes)
        {
            var shaderProperties = new PropertyCollector();
            graph.graphData.CollectShaderProperties(shaderProperties, GenerationMode.ForReals);
            var vfxAttributesToshaderProperties = new StringBuilder();

            List<string> remainingProperties = new List<string>();
            foreach (var prop in shaderProperties.properties)
            {
                string matchingAttribute = attributes.FirstOrDefault(t => prop.displayName.Equals(t, StringComparison.InvariantCultureIgnoreCase));
                if (matchingAttribute == null)
                {
                    remainingProperties.Add(string.Format("{0} {1}", prop.propertyType.ToString(), prop.referenceName));
                }
            }

            return remainingProperties;
        }



        static readonly Dictionary<string, string> s_ShaderGraphToSurfaceDescriptionName = new Dictionary<string, string>()
            {
                {"Albedo" ,"baseColor"},
                {"Smoothness", "perceptualSmoothness" },
                {"Occlusion", "ambientOcclusion" }
            };

        static string ShaderGraphToSurfaceDescriptionName(string name)
        {
            string result;
            if (s_ShaderGraphToSurfaceDescriptionName.TryGetValue(name, out result))
                return result;
            return name.Substring(0,1).ToLower() + name.Substring(1);
        }


        static bool AddCodeIfSlotExist(Graph graph,ShaderStringBuilder builder,string slotName,string existsFormat, string dontExistStr, IEnumerable<MaterialSlot> slots)
        {
            var slot = slots.FirstOrDefault(t => t.shaderOutputName == slotName );

            if (slot != null)
            {
                if(existsFormat != null )
                {
                    var foundEdges = graph.graphData.GetEdges(slot.slotReference).ToArray();
                    if (foundEdges.Any())
                    {
                        builder.AppendLine(existsFormat, graph.graphData.outputNode.GetSlotValue(slot.id, GenerationMode.ForReals));
                    }
                    else
                    {
                        builder.AppendLine(existsFormat, slot.GetDefaultValue(GenerationMode.ForReals));
                    }
                }
                return true;
            }
            else if(dontExistStr != null)
            {
                builder.AppendLine(dontExistStr);
            }
            return false;
        }


        static string GenerateParticleGetSurfaceAndBuiltinData(Graph graph, ref VFXInfos vfxInfos, int currentPass, PassPart pass,Dictionary<string, string> guiVariables,Dictionary<string, int> defines)
        {
            var getSurfaceDataFunction = new ShaderStringBuilder();

            getSurfaceDataFunction.Append(vfxInfos.parameters);
            string shaderGraphCode;

            IEnumerable<MaterialSlot> usedSlots;

            PropertyCollector shaderProperties = new PropertyCollector();
            {   // inspired by GenerateSurfaceDescriptionFunction

                ShaderStringBuilder functionsString = new ShaderStringBuilder();
                FunctionRegistry functionRegistry = new FunctionRegistry(functionsString);

                graph.graphData.CollectShaderProperties(shaderProperties, GenerationMode.ForReals);

                ShaderGenerator sg = new ShaderGenerator();

                GraphContext graphContext = new GraphContext("SurfaceDescriptionInputs");

                foreach (var activeNode in graph.passes[currentPass].pixel.nodes.OfType<AbstractMaterialNode>())
                {
                    if (activeNode is IGeneratesFunction)
                    {
                        functionRegistry.builder.currentNode = activeNode;
                        (activeNode as IGeneratesFunction).GenerateNodeFunction(functionRegistry, graphContext, GenerationMode.ForReals);
                    }
                    if (activeNode is IGeneratesBodyCode)
                        (activeNode as IGeneratesBodyCode).GenerateNodeCode(sg, graphContext, GenerationMode.ForReals);

                    activeNode.CollectShaderProperties(shaderProperties, GenerationMode.ForReals);
                }

                getSurfaceDataFunction.AppendLines(functionsString.ToString());
                functionRegistry.builder.currentNode = null;

                var sb = new StringBuilder();
                sb.Append(sg.GetShaderString(0));
                usedSlots = /*slots ?? */graph.graphData.outputNode.GetInputSlots<MaterialSlot>().Where(t => t.shaderOutputName != "Position"
                                                                                                        && t.shaderOutputName != "Normal"
                                                                                                        && t.shaderOutputName != "BentNormal"
                                                                                                        && t.shaderOutputName != "Emission"
                                                                                                        && t.shaderOutputName != "Alpha"
                                                                                                        && t.shaderOutputName != "AlphaClipThreshold"
                                                                                                        && t.shaderOutputName != "DepthOffset").Intersect(graph.passes[currentPass].pixel.slots);


                foreach (var input in usedSlots)
                {
                    if (input != null)
                    {
                        var foundEdges = graph.graphData.GetEdges(input.slotReference).ToArray();
                        if (foundEdges.Any())
                        {
                            sb.AppendFormat("surfaceData.{0} = {1};\n", ShaderGraphToSurfaceDescriptionName(NodeUtils.GetHLSLSafeName(input.shaderOutputName)), graph.graphData.outputNode.GetSlotValue(input.id, GenerationMode.ForReals));
                        }
                        else
                        {
                            sb.AppendFormat("surfaceData.{0} = {1};\n", ShaderGraphToSurfaceDescriptionName(NodeUtils.GetHLSLSafeName(input.shaderOutputName)), input.GetDefaultValue(GenerationMode.ForReals));
                        }
                    }
                }

                shaderGraphCode = sb.ToString();
            }
            getSurfaceDataFunction.Append(@"


#include ""Packages/com.unity.render-pipelines.high-definition/Runtime/RenderPipeline/VFX/VFXSGCommonLit.hlsl""

void ParticleGetSurfaceAndBuiltinData(FragInputs input, uint index,float3 V, inout PositionInputs posInput, out SurfaceData surfaceData, out BuiltinData builtinData)
{
    FragInputForSG IN = InitializeFragStructs(input, posInput,V, surfaceData, builtinData);

    #ifdef _DOUBLESIDED_ON
      #ifdef _DOUBLESIDED_FLIP
        float3 doubleSidedConstants = float3(-1.0, -1.0, -1.0);
      #elif defined(_DOUBLESIDED_MIRROR)
        float3 doubleSidedConstants = float3(1.0, 1.0, -1.0);
      #else
        float3 doubleSidedConstants = float3(1.0, 1.0, 1.0);
      #endif
    #else
        float3 doubleSidedConstants = float3(1.0, 1.0, 1.0);
    #endif
    ApplyDoubleSidedFlipOrMirror(input, doubleSidedConstants);
    
    surfaceData.geomNormalWS = input.worldToTangent[2];
");
            getSurfaceDataFunction.Append("    " + vfxInfos.loadAttributes.Replace("\n", "\n    "));

            foreach (var prop in shaderProperties.properties)
            {
                string matchingAttribute = vfxInfos.attributes.FirstOrDefault(t => prop.displayName.Equals(t, StringComparison.InvariantCultureIgnoreCase));
                if (matchingAttribute != null)
                {
                    if (matchingAttribute == "color")
                        getSurfaceDataFunction.AppendLine(prop.GetPropertyDeclarationString("") + " = float4(color,1);");
                    else
                        getSurfaceDataFunction.AppendLine(prop.GetPropertyDeclarationString("") + " = " + matchingAttribute + ";");
                }
            }

            getSurfaceDataFunction.Append("    " + shaderGraphCode.Replace("\n", "\n    "));


            AddCodeIfSlotExist(graph, getSurfaceDataFunction, "Alpha", "\tfloat alphaSG = {0};\n alpha *= alphaSG;\n", null, graph.passes[currentPass].pixel.slots);
            bool alphaThresholdExist = AddCodeIfSlotExist(graph, getSurfaceDataFunction, "AlphaClipThreshold", "\tfloat alphaCutoff = {0};\nDoAlphaTest(alpha, alphaCutoff);\n", null, graph.passes[currentPass].pixel.slots);
            if( alphaThresholdExist)
            {
                guiVariables["_ZTestGBuffer"] = "Equal";
                pass.AddTag("Queue", "AlphaTest + 0");
                defines["_ALPHATEST_ON"] = 1;
            }
            else
            {
                guiVariables["_ZTestGBuffer"] = "LEqual";
            }
            AddCodeIfSlotExist(graph, getSurfaceDataFunction, "DepthOffset", "float depthOffset = {0};\nApplyDepthOffsetPositionInput(V, depthOffset, GetViewForwardDir(), GetWorldToHClipMatrix(), posInput);\n", null, graph.passes[currentPass].pixel.slots);

            var coatMask = graph.passes[currentPass].pixel.slots.FirstOrDefault(t => t.shaderOutputName == "CoatMask");
            if (coatMask != null)
            {
                var foundEdges = graph.graphData.GetEdges(coatMask.slotReference).ToArray();
                if (foundEdges.Any())
                {
                    defines["_MATERIAL_FEATURE_CLEAR_COAT"]= 1;
                }
                else
                {
                    float value;
                    if (float.TryParse(coatMask.GetDefaultValue(GenerationMode.ForReals), out value) && value > 0)
                        defines["_MATERIAL_FEATURE_CLEAR_COAT"] = 1;
                }
            }

            AddCodeIfSlotExist(graph, getSurfaceDataFunction, "Normal", "\tfloat3 normalTS = {0};\n", "\tfloat3 normalTS = float3(0.0,0.0,1.0);", graph.passes[currentPass].pixel.slots);

            getSurfaceDataFunction.AppendLine(@"
    float3 bentNormalTS;
    bentNormalTS = normalTS;
    float3 bentNormalWS;
    GetNormalWS(input, normalTS, surfaceData.normalWS, doubleSidedConstants);
");

            AddCodeIfSlotExist(graph, getSurfaceDataFunction, "BentNormal", "\tbentNormalTS = {0};\n", "\tbentNormalWS = surfaceData.normalWS;", graph.passes[currentPass].pixel.slots);

            getSurfaceDataFunction.AppendLine(@"
    GetNormalWS(input, bentNormalTS, bentNormalWS, doubleSidedConstants);
");

            AddCodeIfSlotExist(graph, getSurfaceDataFunction, "Emission", "\tbuiltinData.emissiveColor = {0};\n", null, graph.passes[currentPass].pixel.slots);

            getSurfaceDataFunction.Append(@"
    PostInit(input, surfaceData, builtinData, posInput,bentNormalWS,alpha,V);
}
void ApplyVertexModification(AttributesMesh input, float3 normalWS, inout float3 positionRWS, float4 time)
{

}
                ");

            return getSurfaceDataFunction.ToString();
        }


        internal static string NewGenerateShader(Shader shaderGraph, ref VFXInfos vfxInfos)
        {
            Graph graph = LoadShaderGraph(shaderGraph);

            Dictionary<string, string> guiVariables = new Dictionary<string, string>()
            {
                {"_StencilRef","2" },
                {"_StencilRefDepth","0" },
                {"_StencilRefDistortionVec","64" },
                {"_StencilRefGBuffer", "2"},
                {"_StencilRefMV","128" },
                {"_StencilWriteMask","3" },
                {"_StencilWriteMaskDepth","48" },
                {"_StencilMaskDistortionVec","64" },
                {"_StencilWriteMaskGBuffer", "51"},
                {"_StencilWriteMaskMV","176" },

                {"_CullMode","Back" },
                {"_CullModeForward","Back" },
                {"_SrcBlend","One" },
                {"_DstBlend","Zero" },
                {"_ZWrite","On" },
                {"_ColorMaskTransparentVel","RGBA" },
                {"_ZTestDepthEqualForOpaque","Equal" },
                {"_ZTestGBuffer","LEqual"},
                {"_DistortionSrcBlend","One" },
                {"_DistortionDstBlend","Zero" },
                {"_DistortionBlurBlendOp","Add" },
                {"_ZTestModeDistortion","Always" },
                {"_DistortionBlurSrcBlend","One" },
                {"_DistortionBlurDstBlend","Zero" },
            };

            ShaderDocument document = new ShaderDocument();
            document.Parse(File.ReadAllText("Packages/com.unity.render-pipelines.high-definition/Runtime/Material/Lit/Lit.shader"));

            var defines = new Dictionary<string, int>();


            string[] standardShader = File.ReadAllLines("Packages/com.unity.render-pipelines.high-definition/Runtime/Material/Lit/Lit.shader");


            document.InsertShaderLine(0, "#define UNITY_VERTEX_INPUT_INSTANCE_ID nointerpolation uint instanceID : SV_InstanceID;");
            document.InsertShaderLine(1, "#include \"Packages/com.unity.visualeffectgraph/Shaders/RenderPipeline/HDRP/VFXDefines.hlsl\"");
            document.InsertShaderLine(2, "#include \"Packages/com.unity.visualeffectgraph/Shaders/RenderPipeline/HDRP/VFXCommon.cginc\"");
            document.InsertShaderLine(3, "#include \"Packages/com.unity.visualeffectgraph/Shaders/VFXCommon.cginc\"");


            document.RemoveShaderCodeContaining("#pragma shader_feature_local"); // remove all feature local that are used by the GUI to change some values


            var masterNode = graph.graphData.outputNode as HDLitMasterNode;

            if( masterNode != null)
            {
                if( masterNode.doubleSidedMode != DoubleSidedMode.Disabled)
                {
                    defines["_DOUBLESIDED_ON"] = 1;
                    guiVariables["_CullMode"] = "Off";
                    guiVariables["_CullModeForward"] = "Off";
                    if (masterNode.doubleSidedMode == DoubleSidedMode.FlippedNormals)
                        defines["_DOUBLESIDED_FLIP"] = 1;
                    else if (masterNode.doubleSidedMode == DoubleSidedMode.MirroredNormals)
                        defines["_DOUBLESIDED_MIRROR"] = 1;
                }

                if( masterNode.depthOffset.isOn)
                {
                    defines["_DEPTHOFFSET_ON"] = 1;
                }
            }

            foreach (var pass in document.passes)
            {
                int currentPass = Array.FindIndex(Graph.passInfos, t => t.name == pass.name);
                if (currentPass == -1)
                    continue;
                var sb = new StringBuilder();
                GenerateParticleVert(graph, vfxInfos, sb, currentPass);
                string getSurfaceDataFunction = GenerateParticleGetSurfaceAndBuiltinData(graph, ref vfxInfos, currentPass, pass, guiVariables, defines);

                pass.ReplaceInclude("Packages/com.unity.render-pipelines.high-definition/Runtime/Material/Lit/LitData.hlsl", getSurfaceDataFunction);

                pass.InsertShaderCode(-1,sb.ToString());
                pass.RemoveShaderCodeContaining("#pragma vertex Vert");

                // The hard part : replace the pass specific include with its contents where the call to GetSurfaceAndBuiltinData is replaced by a call to ParticleGetSurfaceAndBuiltinData
                // with an additionnal second parameter ( the instanceID )
                int index = pass.IndexOfLineMatching(@"\s*#include\s*""Packages/com.unity.render-pipelines.high-definition/Runtime/RenderPipeline/ShaderPass/ShaderPass.*\.hlsl""\s*");

                if(index >= 0)
                {
                    string line = pass.shaderCode[index];
                    pass.shaderCode.RemoveAt(index);

                    int firstQuote = line.IndexOf('"');
                    string filePath = line.Substring(firstQuote + 1, line.LastIndexOf('"') - firstQuote - 1);

                    string passFile = File.ReadAllText(filePath);

                    // Replace calls to GetSurfaceAndBuiltinData to calls to ParticleGetSurfaceAndBuiltinData with an additionnal parameter
                    int callIndex = passFile.IndexOf("GetSurfaceAndBuiltinData(");
                    if (callIndex != -1)
                    {
                        int endCallIndex = passFile.IndexOf(';', callIndex + 1);
                        endCallIndex = passFile.LastIndexOf(')', endCallIndex) - 1;
                        int paramStartIndex = callIndex + "GetSurfaceAndBuiltinData(".Length;

                        string[] parameters = passFile.Substring(paramStartIndex, endCallIndex - paramStartIndex).Split(',');

                        var ssb = new StringBuilder();

                        ssb.Append(passFile.Substring(0, callIndex));
                        ssb.Append("ParticleGetSurfaceAndBuiltinData(");

                        var args = parameters.Take(1).Concat(Enumerable.Repeat("packedInput.vmesh.instanceID", 1).Concat(parameters.Skip(1)));

                        ssb.Append(args.Aggregate((a, b) => a + "," + b));

                        ssb.Append(passFile.Substring(endCallIndex));

                        pass.InsertShaderCode(index, ssb.ToString());
                    }

                }
            }
            foreach (var define in defines)
                document.InsertShaderCode(-1, string.Format("#define {0} {1}", define.Key, define.Value));

            document.ReplaceParameterVariables(guiVariables);
            
            return document.ToString(false);
        }

        private static void GenerateParticleVert(Graph graph,VFXInfos vfxInfos, StringBuilder shader, int currentPass)
        {
            shader.Append(vfxInfos.vertexFunctions);

            PropertyCollector shaderProperties = new PropertyCollector();
            var sb = new StringBuilder();
            {   // inspired by GenerateSurfaceDescriptionFunction

                ShaderStringBuilder functionsString = new ShaderStringBuilder();
                FunctionRegistry functionRegistry = new FunctionRegistry(functionsString);

                graph.graphData.CollectShaderProperties(shaderProperties, GenerationMode.ForReals);

                ShaderGenerator sg = new ShaderGenerator();

                GraphContext graphContext = new GraphContext("SurfaceDescriptionInputs");

                foreach (var activeNode in graph.passes[currentPass].vertex.nodes.OfType<AbstractMaterialNode>())
                {
                    if (activeNode is IGeneratesFunction)
                    {
                        functionRegistry.builder.currentNode = activeNode;
                        (activeNode as IGeneratesFunction).GenerateNodeFunction(functionRegistry, graphContext, GenerationMode.ForReals);
                    }
                    if (activeNode is IGeneratesBodyCode)
                        (activeNode as IGeneratesBodyCode).GenerateNodeCode(sg, graphContext, GenerationMode.ForReals);

                    activeNode.CollectShaderProperties(shaderProperties, GenerationMode.ForReals);
                }

                shader.AppendLine(functionsString.ToString());
                functionRegistry.builder.currentNode = null;

                sb.Append(sg.GetShaderString(0));
                var usedSlots = /*slots ?? */graph.graphData.outputNode.GetInputSlots<MaterialSlot>().Where(t => t.shaderOutputName != "Position"
                                                                                                        && t.shaderOutputName != "Normal"
                                                                                                        && t.shaderOutputName != "BentNormal"
                                                                                                        && t.shaderOutputName != "Emission"
                                                                                                        && t.shaderOutputName != "Alpha"
                                                                                                        && t.shaderOutputName != "AlphaClipThreshold").Intersect(graph.passes[currentPass].vertex.slots);

                foreach (var input in usedSlots)
                {
                    if (input != null)
                    {
                        var foundEdges = graph.graphData.GetEdges(input.slotReference).ToArray();
                        if (foundEdges.Any())
                        {
                            sb.AppendFormat("surfaceData.{0} = {1};\n", ShaderGraphToSurfaceDescriptionName(NodeUtils.GetHLSLSafeName(input.shaderOutputName)), graph.graphData.outputNode.GetSlotValue(input.id, GenerationMode.ForReals));
                        }
                        else
                        {
                            sb.AppendFormat("surfaceData.{0} = {1};\n", ShaderGraphToSurfaceDescriptionName(NodeUtils.GetHLSLSafeName(input.shaderOutputName)), input.GetDefaultValue(GenerationMode.ForReals));
                        }
                    }
                }

            }



            shader.AppendLine(@"
PackedVaryingsType ParticleVert(AttributesMesh inputMesh)
{
    VaryingsType varyingsType;
    
    
    varyingsType.vmesh = VertMesh(inputMesh);
    uint index = inputMesh.instanceID;
".Replace("\n", "\n"));
            shader.Append("\t" + vfxInfos.loadAttributes.Replace("\n", "\n\t"));

            shader.AppendLine(@"
    float3 size3 = float3(size,size,size);
	#if VFX_USE_SCALEX_CURRENT
	size3.x *= scaleX;
	#endif
	#if VFX_USE_SCALEY_CURRENT
	size3.y *= scaleY;
	#endif
	#if VFX_USE_SCALEZ_CURRENT
	size3.z *= scaleZ;
	#endif

	float4x4 elementToVFX = GetElementToVFXMatrix(axisX,axisY,axisZ,float3(angleX,angleY,angleZ),float3(pivotX,pivotY,pivotZ),size3,position);

	float3 particlePos;
	VertInputForSG IN = InitializeVertStructs(inputMesh,elementToVFX, particlePos);");

            // add shader code to compute Position if any
            shader.AppendLine(sb.ToString());

            // add shader code to take new objectPos into account if the position slot is linked to something
            var slot = graph.passes[currentPass].vertex.slots.FirstOrDefault(t => t.shaderOutputName == "Position");
            if (slot != null)
            {
                var foundEdges = graph.graphData.GetEdges(slot.slotReference).ToArray();
                if (foundEdges.Any())
                {
                    shader.AppendFormat("float3 objectPos = {0};\nparticlePos = mul(elementToVFX,float4(objectPos,1)).xyz; \n", graph.graphData.outputNode.GetSlotValue(slot.id, GenerationMode.ForReals));
                }
            }

            shader.Append(@"
	varyingsType.vmesh.positionCS = TransformPositionVFXToClip(particlePos);

	#ifdef VARYINGS_NEED_POSITION_WS
	    varyingsType.vmesh.positionRWS = TransformObjectToWorld(particlePos);
    #endif

    #ifdef VARYINGS_NEED_TANGENT_TO_WORLD
        varyingsType.vmesh.normalWS = IN.WorldSpaceNormal.xyz;
        varyingsType.vmesh.tangentWS = float4(IN.WorldSpaceTangent.xyz,inputMesh.tangentOS.w);
    #endif

    PackedVaryingsType result = PackVaryingsType(varyingsType);
    result.vmesh.instanceID = inputMesh.instanceID; // transmit the instanceID to the pixel shader through the varyings
");
            shader.Append("\t" + vfxInfos.vertexShaderContent.Replace("\n","\n\t"));
            shader.Append(@"


    return result;
}
");

            shader.AppendLine("#pragma vertex ParticleVert");
        }

        public class Graph
        {
            internal GraphData graphData;
            internal List<MaterialSlot> slots;

            internal struct Function
            {
                internal List<AbstractMaterialNode> nodes;
                internal ShaderGraphRequirements requirements;
                internal List<MaterialSlot> slots;
            }

            internal struct Pass
            {
                internal Function vertex;
                internal Function pixel;
            }

            internal Pass[] passes = new Pass[passInfos.Length];

            internal class PassInfo
            {
                public PassInfo(string name, FunctionInfo pixel, FunctionInfo vertex)
                {
                    this.name = name;
                    this.pixel = pixel;
                    this.vertex = vertex;
                }
                public readonly string name;
                public readonly FunctionInfo pixel;
                public readonly FunctionInfo vertex;
            }
            internal class FunctionInfo
            {
                public FunctionInfo(List<int> activeSlots)
                {
                    this.activeSlots = activeSlots;
                }
                public readonly List<int> activeSlots;
            }

            internal readonly static PassInfo[] passInfos = new PassInfo[]
                {
                //GBuffer
                new PassInfo("GBuffer",new FunctionInfo(HDLitSubShader.passGBuffer.PixelShaderSlots),new FunctionInfo(HDLitSubShader.passGBuffer.VertexShaderSlots)),
                //ShadowCaster
                new PassInfo("ShadowCaster",new FunctionInfo(HDLitSubShader.passShadowCaster.PixelShaderSlots),new FunctionInfo(HDLitSubShader.passShadowCaster.VertexShaderSlots)),
                new PassInfo("DepthOnly",new FunctionInfo(HDLitSubShader.passDepthOnly.PixelShaderSlots),new FunctionInfo(HDLitSubShader.passDepthOnly.VertexShaderSlots)),
                new PassInfo("SceneSelectionPass",new FunctionInfo(HDLitSubShader.passSceneSelection.PixelShaderSlots),new FunctionInfo(HDLitSubShader.passSceneSelection.VertexShaderSlots)),
                new PassInfo("META",new FunctionInfo(HDLitSubShader.passMETA.PixelShaderSlots),new FunctionInfo(HDLitSubShader.passMETA.VertexShaderSlots)),
                new PassInfo("MotionVectors",new FunctionInfo(HDLitSubShader.passMotionVector.PixelShaderSlots),new FunctionInfo(HDLitSubShader.passMotionVector.VertexShaderSlots)),
                new PassInfo("DistortionVectors",new FunctionInfo(HDLitSubShader.passDistortion.PixelShaderSlots),new FunctionInfo(HDLitSubShader.passDistortion.VertexShaderSlots)),
                new PassInfo("TransparentDepthPrepass",new FunctionInfo(HDLitSubShader.passTransparentPrepass.PixelShaderSlots),new FunctionInfo(HDLitSubShader.passTransparentPrepass.VertexShaderSlots)),
                new PassInfo("TransparentBackface",new FunctionInfo(HDLitSubShader.passTransparentBackface.PixelShaderSlots),new FunctionInfo(HDLitSubShader.passTransparentBackface.VertexShaderSlots)),
                new PassInfo("Forward",new FunctionInfo(HDLitSubShader.passForward.PixelShaderSlots),new FunctionInfo(HDLitSubShader.passForward.VertexShaderSlots)),
                new PassInfo("TransparentDepthPostpass",new FunctionInfo(HDLitSubShader.passTransparentDepthPostpass.PixelShaderSlots),new FunctionInfo(HDLitSubShader.passTransparentDepthPostpass.VertexShaderSlots)),
                };
        }

        public static Graph LoadShaderGraph(Shader shader)
        {
            string shaderGraphPath = AssetDatabase.GetAssetPath(shader);

            if (Path.GetExtension(shaderGraphPath).Equals(".shadergraph", StringComparison.InvariantCultureIgnoreCase))
            {
                return LoadShaderGraph(shaderGraphPath);
            }

            return null;
        }

        public static Graph LoadShaderGraph(string shaderFilePath)
        {
            var textGraph = File.ReadAllText(shaderFilePath, Encoding.UTF8);

            Graph graph = new Graph();
            graph.graphData = JsonUtility.FromJson<GraphData>(textGraph);
            graph.graphData.OnEnable();
            graph.graphData.ValidateGraph();

            graph.slots = new List<MaterialSlot>();
            foreach (var activeNode in ((AbstractMaterialNode)graph.graphData.outputNode).ToEnumerable())
            {
                if (activeNode is IMasterNode || activeNode is SubGraphOutputNode)
                    graph.slots.AddRange(activeNode.GetInputSlots<MaterialSlot>());
                else
                    graph.slots.AddRange(activeNode.GetOutputSlots<MaterialSlot>());
            }
            for(int currentPass = 0; currentPass < Graph.passInfos.Length; ++currentPass)
            {
                graph.passes[currentPass].pixel.nodes = ListPool<AbstractMaterialNode>.Get();
                NodeUtils.DepthFirstCollectNodesFromNode(graph.passes[currentPass].pixel.nodes, ((AbstractMaterialNode)graph.graphData.outputNode), NodeUtils.IncludeSelf.Include, Graph.passInfos[currentPass].pixel.activeSlots);
                graph.passes[currentPass].vertex.nodes = ListPool<AbstractMaterialNode>.Get();
                NodeUtils.DepthFirstCollectNodesFromNode(graph.passes[currentPass].vertex.nodes, ((AbstractMaterialNode)graph.graphData.outputNode), NodeUtils.IncludeSelf.Include, Graph.passInfos[currentPass].vertex.activeSlots);

                graph.passes[currentPass].pixel.requirements = ShaderGraphRequirements.FromNodes(graph.passes[currentPass].pixel.nodes, ShaderStageCapability.Fragment, false);
                graph.passes[currentPass].vertex.requirements = ShaderGraphRequirements.FromNodes(graph.passes[currentPass].vertex.nodes, ShaderStageCapability.Vertex, false);

                graph.passes[currentPass].pixel.requirements.requiresPosition |= NeededCoordinateSpace.View | NeededCoordinateSpace.World;
                graph.passes[currentPass].vertex.requirements.requiresPosition |= NeededCoordinateSpace.Object;

                graph.passes[currentPass].pixel.slots = graph.slots.Where(t => Graph.passInfos[currentPass].pixel.activeSlots.Contains(t.id)).ToList();
                graph.passes[currentPass].vertex.slots = graph.slots.Where(t => Graph.passInfos[currentPass].vertex.activeSlots.Contains(t.id)).ToList();
            }

            return graph;
        }
    }
}
