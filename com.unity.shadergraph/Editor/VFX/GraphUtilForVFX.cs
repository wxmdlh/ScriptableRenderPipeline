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

namespace UnityEditor.ShaderGraph.VFX
{
    //Small methods usable by the VFX since they are public
    public static class GraphUtilForVFX
    {
        public struct VFXInfos
        {
            public Dictionary<string, string> customParameterAccess;
            public string vertexFunctions;
            public string vertexShaderContent;
            public string shaderName;
            public string parameters;
            public List<string> attributes;
            public string loadAttributes;
        }

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


        static bool AddCodeIfSlotExist(Graph graph,ShaderStringBuilder builder,string slotName,string existsFormat, string dontExistStr)
        {
            var slot = graph.graphData.outputNode.GetInputSlots<MaterialSlot>().FirstOrDefault(t => t.shaderOutputName == slotName );

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


        static string GenerateParticleGetSurfaceAndBuiltinData(Graph graph, ref VFXInfos vfxInfos, Dictionary<string, string> guiVariables,Dictionary<string, int> defines,ShaderDocument shaderDoc)
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
                int currentPass = 0;

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
                                                                                                        && t.shaderOutputName != "AlphaClipThreshold");

                if (graph.graphData.outputNode is IMasterNode)
                {
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

                shaderGraphCode = sb.ToString();
            }
            getSurfaceDataFunction.Append(@"


#include ""Packages/com.unity.render-pipelines.high-definition/Runtime/RenderPipeline/VFX/VFXSGCommonLit.hlsl""

void ParticleGetSurfaceAndBuiltinData(FragInputs input, uint index,float3 V, inout PositionInputs posInput, out SurfaceData surfaceData, out BuiltinData builtinData)
{
    FragInputForSG IN = InitializeStructs(input, posInput,V, surfaceData, builtinData);

    #ifdef _DOUBLESIDED_ON
        float3 doubleSidedConstants = _DoubleSidedConstants.xyz;
    #else
        float3 doubleSidedConstants = float3(1.0, 1.0, 1.0);
    #endif
    ApplyDoubleSidedFlipOrMirror(input, doubleSidedConstants);
    
    surfaceData.geomNormalWS = input.worldToTangent[2];
");
            getSurfaceDataFunction.Append("\t" + vfxInfos.loadAttributes.Replace("\n", "\n\t"));

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

            getSurfaceDataFunction.Append("\t" + shaderGraphCode.Replace("\n", "\n\t"));


            AddCodeIfSlotExist(graph, getSurfaceDataFunction, "Alpha","\talpha = {0};\n", null);
            bool alphaThresholdExist = AddCodeIfSlotExist(graph, getSurfaceDataFunction, "AlphaClipThreshold", "\tfloat alphaCutoff = {0};\n", null);
            if( alphaThresholdExist)
            {
                guiVariables["_ZTestGBuffer"] = "Equal";
                shaderDoc.AddTag("Queue", "AlphaTest + 0");
                defines.Add("_ALPHATEST_ON", 1);
            }
            else
            {
                guiVariables["_ZTestGBuffer"] = "LEqual";
            }

            var coatMask = graph.graphData.outputNode.GetInputSlots<MaterialSlot>().FirstOrDefault(t => t.shaderOutputName == "CoatMask");
            if (coatMask != null)
            {
                var foundEdges = graph.graphData.GetEdges(coatMask.slotReference).ToArray();
                if (foundEdges.Any())
                {
                    defines.Add("_MATERIAL_FEATURE_CLEAR_COAT", 1);
                }
                else
                {
                    float value;
                    if (float.TryParse(coatMask.GetDefaultValue(GenerationMode.ForReals), out value) && value > 0)
                        defines.Add("_MATERIAL_FEATURE_CLEAR_COAT", 1);
                }
            }

            AddCodeIfSlotExist(graph, getSurfaceDataFunction, "Normal", "\tfloat3 normalTS = {0};\n", "\tfloat3 normalTS = float3(0.0,0.0,1.0);");

            getSurfaceDataFunction.AppendLine(@"
    float3 bentNormalTS;
    bentNormalTS = normalTS;
    float3 bentNormalWS;
    GetNormalWS(input, normalTS, surfaceData.normalWS, doubleSidedConstants);
");

            AddCodeIfSlotExist(graph, getSurfaceDataFunction, "BentNormal", "\tbentNormalTS = {0};\n", "\tbentNormalWS = surfaceData.normalWS;");

            getSurfaceDataFunction.AppendLine(@"
    GetNormalWS(input, bentNormalTS, bentNormalWS, doubleSidedConstants);
");

            AddCodeIfSlotExist(graph, getSurfaceDataFunction, "Emission", "\tbuiltinData.emissiveColor = {0};\n", null);

            getSurfaceDataFunction.Append(@"
    PostInit(input, surfaceData, builtinData, posInput,bentNormalWS,alpha,V);
}
void ApplyVertexModification(AttributesMesh input, float3 normalWS, inout float3 positionRWS, float4 time)
{

}
                ");

            return getSurfaceDataFunction.ToString();
        }


        public static string NewGenerateShader(Shader shaderGraph, ref VFXInfos vfxInfos)
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

            string getSurfaceDataFunction = GenerateParticleGetSurfaceAndBuiltinData(graph, ref vfxInfos, guiVariables, defines,document);

            string[] standardShader = File.ReadAllLines("Packages/com.unity.render-pipelines.high-definition/Runtime/Material/Lit/Lit.shader");

            document.ReplaceInclude("Packages/com.unity.render-pipelines.high-definition/Runtime/Material/Lit/LitData.hlsl", getSurfaceDataFunction);

            document.InsertShaderLine(0,"#define UNITY_VERTEX_INPUT_INSTANCE_ID uint instanceID : SV_InstanceID;");
            document.InsertShaderLine(1, "#include \"Packages/com.unity.visualeffectgraph/Shaders/RenderPipeline/HDRP/VFXDefines.hlsl\"");
            document.InsertShaderLine(2, "#include \"Packages/com.unity.visualeffectgraph/Shaders/RenderPipeline/HDRP/VFXCommon.cginc\"");
            document.InsertShaderLine(3, "#include \"Packages/com.unity.visualeffectgraph/Shaders/VFXCommon.cginc\"");

            var sb = new StringBuilder();
            GenerateParticleVert(vfxInfos, sb);

            document.RemoveShaderCodeContaining("#pragma shader_feature_local"); // remove all feature local that are used by the GUI to change some values


            foreach (var define in defines)
                document.InsertShaderCode(-1,string.Format("#define {0} {1}", define.Key, define.Value));

            foreach (var pass in document.passes)
            {
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

            document.ReplaceParameterVariables(guiVariables);
            
            return document.ToString(false);
        }

        private static void GenerateParticleVert(VFXInfos vfxInfos, StringBuilder shader)
        {
            shader.Append(vfxInfos.vertexFunctions);

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
	float3 vPos = mul(elementToVFX,float4(inputMesh.positionOS,1.0f)).xyz;
			
	varyingsType.vmesh.positionCS = TransformPositionVFXToClip(vPos);

	#ifdef VARYINGS_NEED_POSITION_WS
	    varyingsType.vmesh.positionRWS = TransformObjectToWorld(vPos);
    #endif

    #ifdef VARYINGS_NEED_TANGENT_TO_WORLD
        float3 normalWS = TransformObjectToWorldNormal(inputMesh.normalOS);
        float4 tangentWS = float4(TransformObjectToWorldDir(inputMesh.tangentOS.xyz), inputMesh.tangentOS.w);
        varyingsType.vmesh.normalWS = normalWS;
        varyingsType.vmesh.tangentWS = tangentWS;
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



            internal class PassName
            {
                public const int GBuffer = 0;
                public const int ShadowCaster = 1;
                public const int Depth = 2;
                public const int Count = 3;
            }

            internal Pass[] passes = new Pass[PassName.Count];

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
                new PassInfo("GBuffer",new FunctionInfo(Enumerable.Range(1, 31).ToList()),new FunctionInfo(Enumerable.Range(0,1).ToList())), // hardcoded pbr pixel slots.
                //ShadowCaster
                new PassInfo("ShadowCaster",new FunctionInfo(new[]{1,13,18 }.ToList()),new FunctionInfo(Enumerable.Range(0,1).ToList())),
                new PassInfo("DepthOnly",new FunctionInfo(new[]{1,13,18 }.ToList()),new FunctionInfo(Enumerable.Range(0,1).ToList())),
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
            for(int currentPass = 0; currentPass < Graph.PassName.Count; ++currentPass)
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
