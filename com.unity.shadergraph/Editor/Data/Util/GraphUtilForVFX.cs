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

namespace UnityEditor.ShaderGraph
{
    //Small methods usable by the VFX since they are public
    public static class GraphUtilForVFX
    {
        class Chunk
        {
            string name;
            string content;
        }

        class ShaderCode
        {
            List<Chunk> chunks = new List<Chunk>();
        }

        

        class ShaderPart
        {
            protected bool IsSame(string paramName, string document, RangeInt range)
            {
                return paramName.Length == range.length && string.Compare(document, range.start, paramName, 0, range.length, StringComparison.InvariantCultureIgnoreCase) == 0;
            }

            protected bool IsAny(IEnumerable<string> paramNames, string document, RangeInt range)
            {
                return paramNames.Any(t => IsSame(t, document, range));
            }

            protected string shaderCode;
            protected ShaderParameters parameters;


            protected int ParseParameter(string statement, RangeInt totalRange, out RangeInt nameRange, out RangeInt range)
            {
                range.start = 0;
                range.length = 0;
                nameRange.start = 0;
                nameRange.length = 0;
                int startIndex = totalRange.start;
                foreach (var charac in statement.Take(totalRange.end).Skip(totalRange.start))
                {
                    if (!char.IsWhiteSpace(charac))
                        break;
                    ++startIndex;
                }

                if (totalRange.end < startIndex + 1)
                    return -2;

                int localEndIndex = startIndex + 1;

                foreach (var charac in statement.Take(totalRange.end).Skip(localEndIndex))
                {
                    if (char.IsWhiteSpace(charac) || charac == '{' || charac == '"' || charac == '[')
                        break;
                    ++localEndIndex;
                }

                if (totalRange.end < localEndIndex)
                    return -2;

                nameRange.start = startIndex;
                nameRange.length = localEndIndex - startIndex;
                string name = statement.Substring(nameRange.start, nameRange.length);
                Debug.Log(name);

                //This is a comment line starting with two slashes : skip the line
                if (nameRange.length >= 2 && statement[startIndex] == '/' && statement[startIndex + 1] == '/')
                {
                    int lineStartIndex = localEndIndex+1;
                    int lineEndIndex = lineStartIndex;
                    foreach (var charac in statement.Take(totalRange.end).Skip(lineStartIndex))
                    {
                        lineEndIndex++;
                        if (charac == '\n')
                            break;
                    }
                    range.start = lineStartIndex;
                    range.length = lineEndIndex - lineStartIndex;
                    return 0;
                }

                if( IsAny(startShaderName,statement,nameRange))
                {
                    range.start = localEndIndex + 1;
                    return 0;
                }
                    

                bool wasEscapeChar = false;
                bool inQuotes = false;
                bool noSurrounding = false;
                int bracketLevel = 0;

                int valueStartIndex = localEndIndex;
                int valueEndIndex = valueStartIndex;
                int dataStartIndex = 0;

                foreach ( var charac in statement.Take(totalRange.end).Skip(valueStartIndex))
                {
                    if (noSurrounding && char.IsWhiteSpace(charac))
                        break;
                    valueEndIndex++;

                    if (inQuotes)
                    {
                        if (charac == '\\')
                            wasEscapeChar = true;
                        else if (!wasEscapeChar && charac == '"')
                        {
                            inQuotes = false;
                            if (bracketLevel == 0)
                            {
                                break;
                            }
                        }
                    }
                    else if (charac == '"')
                    {
                        if(dataStartIndex== 0)
                        {
                            dataStartIndex = valueEndIndex-1;
                        }
                        inQuotes = true;
                    }
                    if( ! inQuotes)
                    {
                        if (charac == '{')
                        {
                            if (dataStartIndex == 0 && bracketLevel == 0)
                            {
                                dataStartIndex = valueEndIndex-1;
                            }
                            bracketLevel++;
                        }
                        if (charac == '}')
                        {
                            bracketLevel--;
                            if (bracketLevel == 0)
                                break;
                        }
                    }
                    if (!noSurrounding && !inQuotes && bracketLevel == 0 && !char.IsWhiteSpace(charac)) // Parameter without quote or bracket ( Cull ... )
                    {
                        dataStartIndex = valueEndIndex - 1;
                        noSurrounding = true;
                    }
                }

                range.start = dataStartIndex;
                range.length = valueEndIndex - dataStartIndex;

                return 0;
            }

            protected int ParseContent(string document,RangeInt totalRange, RangeInt paramName,ref RangeInt param)
            {
                if (IsAny(startShaderName, document, paramName)) //START OF SHADER, end is not param but must find line with endShaderName
                {
                    //Look for line with one of endShaderName

                    int nextLine = paramName.end;
                    bool found = false;
                    int endLine = 0;
                    int startLine = 0;
                    do
                    {
                        foreach (var charac in document.Take(totalRange.end).Skip(nextLine))
                        {
                            nextLine++;
                            if (charac == '\n')
                                break;
                        }
                        startLine = nextLine;
                        foreach (var charac in document.Take(totalRange.end).Skip(nextLine))
                        {
                            if (!char.IsWhiteSpace(charac))
                                break;

                            ++startLine;
                        }

                        endLine = startLine + 1;

                        foreach (var charac in document.Take(totalRange.end).Skip(endLine))
                        {
                            if (char.IsWhiteSpace(charac))
                                break;
                            endLine++;
                        }

                        if (IsAny(endShaderName, document, new RangeInt(startLine, endLine - startLine)))
                        {
                            found = true;
                            break;
                        }
                        nextLine = endLine;
                        foreach (var charac in document.Take(totalRange.end).Skip(nextLine))
                        {
                            if (charac == '\n')
                                break;
                            nextLine++;
                        }

                    }
                    while (!found);
                    shaderCode = document.Substring(paramName.end, startLine - paramName.end);
                    param.length = endLine - param.start;
                }
                else if (IsSame("Cull", document, paramName))
                {
                    parameters.cullMode = document.Substring(param.start, param.length);
                }
                else if (IsSame("ZWrite", document, paramName))
                {
                    parameters.zWrite = document.Substring(param.start, param.length);
                }
                else if (IsSame("ZTest", document, paramName))
                {
                    parameters.zTest = document.Substring(param.start, param.length);
                }
                else if (IsSame("Tags", document, paramName))
                {
                    ParseTags(document,param);
                }

                return 0;
            }

            void ParseTags(string document,RangeInt param)
            {
                if (document[param.start] == '{')
                {
                    param.start++;
                    param.length--;
                }
                if (document[param.start + param.length] == '}')
                    param.length--;

                string key = null;

                int pos = param.start;

                while(pos < param.end)
                {
                    foreach (char charac in document.Skip(pos).Take(param.length))
                    {
                        if (!char.IsWhiteSpace(charac))
                            break;
                        ++pos;
                    }
                    if (document[pos] == '"')
                        pos++;
                    int startSentence = pos;
                    foreach (char charac in document.Skip(pos).Take(param.length))
                    {
                        if (char.IsWhiteSpace(charac) || charac == '=')
                            break;
                        ++pos;
                    }
                    int endSentence = pos;
                    if (document[endSentence-1] == '"')
                        endSentence--;

                    if ( key == null)
                    {
                        key = document.Substring(startSentence, endSentence - startSentence);

                        foreach (char charac in document.Skip(pos).Take(param.length))
                        {
                            ++pos;
                            if (charac == '=' )
                                break;
                        }
                    }
                    else
                    {
                        if(parameters.tags == null)
                        {
                            parameters.tags = new Dictionary<string, string>();
                        }
                        parameters.tags[key] = document.Substring(startSentence, endSentence - startSentence);
                        key = null;
                    }
                }
            }

            static readonly string[] startShaderName = { "HLSLINCLUDE", "HLSLPROGRAM", "CGINCLUDE", "CGPROGRAM" };
            static readonly string[] endShaderName = { "ENDHLSL", "ENDCG" };


            protected void AppendContentTo(StringBuilder sb)
            {
                if(parameters.tags != null)
                {
                    sb.AppendLine("Tags {"+ parameters.tags.Select(t=>string.Format("\"{0}\" = \"{1}\" ", t.Key, t.Value)).Aggregate((s,t)=> s + t) + '}');
                }

                if (parameters.cullMode != null)
                {
                    sb.AppendLine("Cull " + parameters.cullMode);
                }
                if (parameters.zTest != null)
                {
                    sb.AppendLine("ZTest " + parameters.zTest);
                }
                if (parameters.zWrite != null)
                {
                    sb.AppendLine("ZWrite " + parameters.zWrite);
                }

                if (!string.IsNullOrEmpty(shaderCode))
                {
                    sb.AppendLine("HLSLPROGRAM");
                    sb.Append(shaderCode);
                    sb.AppendLine("ENDHLSL");
                }
            }
        }

        class PassPart : ShaderPart
        {
            protected string name;
            public int Parse(string document, RangeInt totalRange)
            {
                int startIndex = document.IndexOf('{',totalRange.start,totalRange.length);
                if (startIndex == -1)
                    return -1;

                RangeInt paramName;
                RangeInt param;

                int endIndex = document.LastIndexOf('}',totalRange.end,totalRange.length);
                startIndex += 1; // skip '{' itself

                while (ParseParameter(document, new RangeInt(startIndex, endIndex - startIndex), out paramName, out param) == 0)
                {
                    if( IsSame("name",document,paramName))
                    {
                        name = document.Substring(param.start + 1, param.length - 2);
                    }
                    else
                        base.ParseContent(document, new RangeInt(startIndex, endIndex - startIndex), paramName, ref param);
                    startIndex = param.end;
                }


                return 0;
            }
            public void AppendTo(StringBuilder sb)
            {
                sb.AppendLine("Pass");
                sb.AppendLine("{");
                sb.AppendFormat("name \"{0}\"\n",name);
                base.AppendContentTo(sb);
                sb.AppendLine("}");
            }
        }

        class SubShaderPart : ShaderPart
        {
            List<PassPart> passes = new List<PassPart>();

            public int Parse(string document, RangeInt totalRange)
            {
                int startIndex = document.IndexOf('{',totalRange.start,totalRange.length);
                if (startIndex == -1)
                    return -1;

                RangeInt paramName;
                RangeInt param;

                int endIndex = document.LastIndexOf('}',totalRange.end,totalRange.length);
                startIndex += 1; // skip '{' itself

                while (ParseParameter(document, new RangeInt(startIndex, endIndex - startIndex), out paramName, out param) == 0)
                {
                    if (IsSame("Pass", document, paramName))
                    {
                        PassPart pass = new PassPart();

                        if( pass.Parse(document,param) == 0)
                            passes.Add(pass);
                    }
                    else
                    {
                        base.ParseContent(document, new RangeInt(startIndex, endIndex - startIndex), paramName, ref param);
                    }
                    startIndex = param.end;
                }


                return 0;
            }

            public void AppendTo(StringBuilder sb)
            {
                sb.AppendLine("SubShader");
                sb.AppendLine("{");
                base.AppendContentTo(sb);
                foreach(var pass in passes)
                {
                    pass.AppendTo(sb);
                }
                sb.AppendLine("}");
            }
        }

        class ShaderDocument : ShaderPart
        {
            protected string name;
            List<SubShaderPart> subShaders = new List<SubShaderPart>();

            public override string ToString()
            {
                var sb = new StringBuilder();
                sb.AppendLine("Shader \"" +name+'"');
                sb.AppendLine("{");

                base.AppendContentTo(sb);

                foreach(var subShader in subShaders)
                {
                    subShader.AppendTo(sb);
                }
                sb.AppendLine("}");

                return sb.ToString();
            }

            public int Parse(string document)
            {
                int startIndex = document.IndexOf('{');
                if (startIndex == -1)
                    return -1;

                RangeInt paramName;
                RangeInt param;

                int error = ParseParameter(document, new RangeInt(0, startIndex), out paramName, out param);
                if( error != 0)
                {
                    return 1000 + error;
                }
                if (!IsSame("Shader", document, paramName))
                    return -2;

                name = document.Substring(param.start+1, param.length-2);

                int endIndex = document.LastIndexOf('}');
                startIndex += 1; // skip '{' itself

                while( ParseParameter(document,new RangeInt(startIndex, endIndex - startIndex),out paramName, out param) == 0)
                {
                    if (IsSame("Properties", document, paramName))
                    {
                        // ignore properties Block we don't need it in our case
                    }
                    else if( IsSame("SubShader",document,paramName))
                    {
                        SubShaderPart subShader = new SubShaderPart();

                        if( subShader.Parse(document,param) == 0)
                            subShaders.Add(subShader);
                    }
                    else
                    {
                        base.ParseContent(document, new RangeInt(startIndex, endIndex - startIndex), paramName, ref param);
                        
                    }
                    startIndex = param.end;
                }


                return 0;
            }

        }

        struct StencilParameters
        {
            string writeMask;
            string reference;
            CompareFunction comp;
            string pass;
        }

        [Flags]
        enum ColorMask
        {
            R = 1<<0,
            G = 1<<1,
            B = 1<<2,
            A = 1<<3,
            RGB = R|G|B,
            All = R |G |B | A
        }

        struct ShaderParameters
        {
            public Dictionary<string, string> tags;
            public string cullMode;
            public string zWrite;
            public string zTest;
            public StencilParameters stencilParameters;
            public string ZClip;
            public string colorMask;
        }



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

        public static string NewGenerateShader(Shader shaderGraph, ref VFXInfos vfxInfos)
        {
            Graph graph = LoadShaderGraph(shaderGraph);
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


            getSurfaceDataFunction.Append(@"

ByteAddressBuffer attributeBuffer;

struct FragInputForSG
{
    float4 posCS; // In case depth offset is use, positionRWS.w is equal to depth offset
    float3 posWD; // Relative camera space position
    float4 uv0;
    float4 uv1;
    float4 uv2;
    float4 uv3;
    float4 color; // vertex color

    float3 TangentSpaceNormal;
};
FragInputForSG ConvertFragInput(FragInputs input)
{
    FragInputForSG fisg;
    fisg.TangentSpaceNormal = float3(0.0f, 0.0f, 1.0f);
    fisg.posCS = input.positionSS;
    fisg.posWD = input.positionRWS;
    fisg.uv0 = input.texCoord0;
    fisg.uv1 = input.texCoord1;
    fisg.uv2 = input.texCoord2;
    fisg.uv3 = input.texCoord3;
    fisg.color = input.color;

    return fisg;
}
#include ""Packages/com.unity.render-pipelines.core/ShaderLibrary/Sampling/SampleUVMapping.hlsl""
#include ""Packages/com.unity.render-pipelines.high-definition/Runtime/Material/MaterialUtilities.hlsl""
#include ""Packages/com.unity.render-pipelines.high-definition/Runtime/Material/Decal/DecalUtilities.hlsl""
#include ""Packages/com.unity.render-pipelines.high-definition/Runtime/Material/Lit/LitDecalData.hlsl""
#include ""Packages/com.unity.render-pipelines.high-definition/Runtime/Material/BuiltinUtilities.hlsl""
#include ""Packages/com.unity.shadergraph/ShaderGraphLibrary/Functions.hlsl""

void ParticleGetSurfaceAndBuiltinData(FragInputs input, uint index,float3 V, inout PositionInputs posInput, out SurfaceData surfaceData, out BuiltinData builtinData)
{
    surfaceData = (SurfaceData)0;
    builtinData = (BuiltinData)0;

    FragInputForSG IN = ConvertFragInput(input);

    //Setup default value in case sg does not set them
    surfaceData.metallic = 1.0;
    surfaceData.ambientOcclusion = 1.0;
    surfaceData.anisotropy = 1.0;

    surfaceData.ior = 1.0;
    surfaceData.transmittanceColor = float3(1.0, 1.0, 1.0);
    surfaceData.atDistance = 1.0;
    surfaceData.transmittanceMask = 0.0;

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

            getSurfaceDataFunction.Append("\t"+shaderGraphCode.Replace("\n","\n\t"));

            var alpha = graph.graphData.outputNode.GetInputSlots<MaterialSlot>().FirstOrDefault(t => t.shaderOutputName == "Alpha");

            if (alpha != null)
            {
                var foundEdges = graph.graphData.GetEdges(alpha.slotReference).ToArray();
                if (foundEdges.Any())
                {
                    getSurfaceDataFunction.AppendLine("\talpha = {0};/* surfaceData.baseColor = float3(alpha,alpha,alpha);*/\n", graph.graphData.outputNode.GetSlotValue(alpha.id, GenerationMode.ForReals));
                }
                else
                {
                    getSurfaceDataFunction.AppendLine("\talpha = {0};\n", alpha.GetDefaultValue(GenerationMode.ForReals));
                }
            }

            var alphaThreshold = graph.graphData.outputNode.GetInputSlots<MaterialSlot>().FirstOrDefault(t => t.shaderOutputName == "AlphaClipThreshold");
            string SubShaderTags = "\t\tTags{ \"RenderPipeline\"=\"HDRenderPipeline\" \"RenderType\" = \"HDLitShader\" }" ;
            var defines = new Dictionary<string, int>();
            if (alphaThreshold != null)
            {
                guiVariables["_ZTestGBuffer"] = "Equal";
                SubShaderTags = "\t\tTags{ \"RenderPipeline\"=\"HDRenderPipeline\" \"RenderType\" = \"HDLitShader\" \"Queue\"=\"AlphaTest+0\" }";
                var foundEdges = graph.graphData.GetEdges(alphaThreshold.slotReference).ToArray();
                if (foundEdges.Any())
                {
                    getSurfaceDataFunction.AppendLine("\float alphaCutoff = {0};\n", graph.graphData.outputNode.GetSlotValue(alphaThreshold.id, GenerationMode.ForReals));
                }
                else
                {
                    getSurfaceDataFunction.AppendLine("\tfloat alphaCutoff = {0};\n", alphaThreshold.GetDefaultValue(GenerationMode.ForReals));
                }
                getSurfaceDataFunction.AppendLine("DoAlphaTest(alpha, alphaCutoff);");
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
                    if( float.TryParse(coatMask.GetDefaultValue(GenerationMode.ForReals),out value) && value > 0)
                        defines.Add("_MATERIAL_FEATURE_CLEAR_COAT", 1);
                }
            }

            getSurfaceDataFunction.Append(@"

    surfaceData.normalWS = float3(0.0, 0.0, 0.0); // Need to init this to keep quiet the compiler, but this is overriden later (0, 0, 0) so if we forget to override the compiler may comply.
    surfaceData.geomNormalWS = float3(0.0, 0.0, 0.0); // Not used, just to keep compiler quiet.


    surfaceData.materialFeatures = MATERIALFEATUREFLAGS_LIT_STANDARD;

    #ifdef _MATERIAL_FEATURE_SUBSURFACE_SCATTERING
        surfaceData.materialFeatures |= MATERIALFEATUREFLAGS_LIT_SUBSURFACE_SCATTERING;
    #endif
    #ifdef _MATERIAL_FEATURE_TRANSMISSION
        surfaceData.materialFeatures |= MATERIALFEATUREFLAGS_LIT_TRANSMISSION;
    #endif
    #ifdef _MATERIAL_FEATURE_ANISOTROPY
        surfaceData.materialFeatures |= MATERIALFEATUREFLAGS_LIT_ANISOTROPY;
    #endif
    #ifdef _MATERIAL_FEATURE_CLEAR_COAT
        surfaceData.materialFeatures |= MATERIALFEATUREFLAGS_LIT_CLEAR_COAT;
    #endif
    #ifdef _MATERIAL_FEATURE_IRIDESCENCE
        surfaceData.materialFeatures |= MATERIALFEATUREFLAGS_LIT_IRIDESCENCE;
    #endif
    #ifdef _MATERIAL_FEATURE_SPECULAR_COLOR
        surfaceData.materialFeatures |= MATERIALFEATUREFLAGS_LIT_SPECULAR_COLOR;
    #endif

    surfaceData.tangentWS = input.worldToTangent[0].xyz; // The tangent is not normalize in worldToTangent for mikkt. TODO: Check if it expected that we normalize with Morten. Tag: SURFACE_GRADIENT



    #ifdef _DOUBLESIDED_ON
        float3 doubleSidedConstants = _DoubleSidedConstants.xyz;
    #else
        float3 doubleSidedConstants = float3(1.0, 1.0, 1.0);
    #endif
    ApplyDoubleSidedFlipOrMirror(input, doubleSidedConstants);
 ");
            var normal = graph.graphData.outputNode.GetInputSlots<MaterialSlot>().FirstOrDefault(t => t.shaderOutputName == "Normal");

            if (normal != null)
            {
                var foundEdges = graph.graphData.GetEdges(normal.slotReference).ToArray();
                if (foundEdges.Any())
                {
                    getSurfaceDataFunction.AppendLine("\tfloat3 normalTS = {0};\n", graph.graphData.outputNode.GetSlotValue(normal.id, GenerationMode.ForReals));
                }
                else
                {
                    getSurfaceDataFunction.AppendLine("\tfloat3 normalTS = {0};\n", normal.GetDefaultValue(GenerationMode.ForReals));
                }
            }
            else
            {
                getSurfaceDataFunction.AppendLine("\tfloat3 normalTS = float3(0.0,0.0,1.0);");
            }


            getSurfaceDataFunction.AppendLine(@"
    float3 bentNormalTS;
    bentNormalTS = normalTS;
    float3 bentNormalWS;
    GetNormalWS(input, normalTS, surfaceData.normalWS, doubleSidedConstants);
");
            var bentNormal = graph.graphData.outputNode.GetInputSlots<MaterialSlot>().FirstOrDefault(t=> t.shaderOutputName == "BentNormal");

            if( bentNormal != null)
            {
                var foundEdges = graph.graphData.GetEdges(bentNormal.slotReference).ToArray();
                if (foundEdges.Any())
                {
                    getSurfaceDataFunction.AppendLine("\tbentNormalTS = {0};\n", graph.graphData.outputNode.GetSlotValue(bentNormal.id, GenerationMode.ForReals));
                }
                else
                {
                    getSurfaceDataFunction.AppendLine("\tbentNormalTS = {0};\n", bentNormal.GetDefaultValue(GenerationMode.ForReals));
                }
                getSurfaceDataFunction.AppendLine("\tGetNormalWS(input, bentNormalTS, bentNormalWS, doubleSidedConstants); ");
            }
            else
            {
                getSurfaceDataFunction.AppendLine("\tbentNormalWS = surfaceData.normalWS;");
            }
            

            getSurfaceDataFunction.Append(@"
    surfaceData.geomNormalWS = input.worldToTangent[2];
    surfaceData.specularOcclusion = 1.0;

    surfaceData.tangentWS = Orthonormalize(surfaceData.tangentWS, surfaceData.normalWS);

    InitBuiltinData(posInput, alpha, bentNormalWS, -input.worldToTangent[2], input.texCoord1, input.texCoord2, builtinData);
");

            var emissive = graph.graphData.outputNode.GetInputSlots<MaterialSlot>().FirstOrDefault(t => t.shaderOutputName == "Emission");
            if (emissive != null)
            {
                var foundEdges = graph.graphData.GetEdges(emissive.slotReference).ToArray();
                if (foundEdges.Any())
                {
                    getSurfaceDataFunction.AppendLine("\tbuiltinData.emissiveColor = {0};\n", graph.graphData.outputNode.GetSlotValue(emissive.id, GenerationMode.ForReals));
                }
                else
                {
                    getSurfaceDataFunction.AppendLine("\tbuiltinData.emissiveColor = {0};\n", emissive.GetDefaultValue(GenerationMode.ForReals));
                }
            }

            getSurfaceDataFunction.Append(@"
#if (SHADERPASS == SHADERPASS_DISTORTION) || defined(DEBUG_DISPLAY)
    float3 distortion = SAMPLE_TEXTURE2D(_DistortionVectorMap, sampler_DistortionVectorMap, input.texCoord0.xy).rgb;
    distortion.rg = distortion.rg * _DistortionVectorScale.xx + _DistortionVectorBias.xx;
    builtinData.distortion = distortion.rg * _DistortionScale;
    builtinData.distortionBlur = clamp(distortion.b * _DistortionBlurScale, 0.0, 1.0) * (_DistortionBlurRemapMax - _DistortionBlurRemapMin) + _DistortionBlurRemapMin;
#endif

    //builtinData.depthOffset = depthOffset;

    PostInitBuiltinData(V, posInput, surfaceData, builtinData);
}");


        getSurfaceDataFunction.Append(@"
void ApplyVertexModification(AttributesMesh input, float3 normalWS, inout float3 positionRWS, float4 time)
{

}
                ");

            string[] standardShader = File.ReadAllLines("Packages/com.unity.render-pipelines.high-definition/Runtime/Material/Lit/Lit.shader");

            ShaderDocument document = new ShaderDocument();
            document.Parse(File.ReadAllText("Packages/com.unity.render-pipelines.high-definition/Runtime/Material/Lit/Lit.shader"));
            File.WriteAllText("C:/unity/shaderDocument.txt",document.ToString());

            var shader = new StringBuilder();
            bool withinProperties = false;
            bool propertiesSkipped = false;
            bool firstFeatureLocal = true;
            for(int i = 1; i < standardShader.Length; ++i) // to skip the "Shader "toto"" line
            {
                if (!propertiesSkipped)
                {
                    if (!withinProperties)
                    {
                        if (standardShader[i].Trim() == "Properties")
                        {
                            withinProperties = true;

                            string indentation = standardShader[i].Substring(0, standardShader[i].IndexOf('P'));

                            shader.AppendLine(indentation + "Properties");
                            shader.AppendLine(indentation + "{");
                        }
                    }
                    else
                    {
                        if (standardShader[i].Trim() == "}")
                        {
                            withinProperties = false;
                            propertiesSkipped = true;
                        }
                    }
                }
                if( !withinProperties)
                {
                    string trimmed = standardShader[i].Trim();
                    if (trimmed  != "#include \"Packages/com.unity.render-pipelines.high-definition/Runtime/Material/Lit/LitData.hlsl\"")
                    {
                        if (trimmed.StartsWith("#pragma vertex"))
                        {
                            string indentation = standardShader[i].Substring(0, standardShader[i].IndexOf('#'));
                            GenerateParticleVert(vfxInfos, shader, indentation);
                        }
                        else if (trimmed.StartsWith("#include \"Packages/com.unity.render-pipelines.high-definition/Runtime/RenderPipeline/ShaderPass/ShaderPass")) // let's hack the file matching the shader pass
                        {
                            int indexOfQuote = trimmed.IndexOf('"');
                            string fileName = trimmed.Substring(indexOfQuote + 1, trimmed.Length - 2 - indexOfQuote);

                            string passFile = File.ReadAllText(fileName);

                            // Replace calls to GetSurfaceAndBuiltinData to calls to ParticleGetSurfaceAndBuiltinData with an additionnal parameter
                            int callIndex = passFile.IndexOf("GetSurfaceAndBuiltinData(");
                            if (callIndex != -1)
                            {
                                int endCallIndex = passFile.IndexOf(';', callIndex + 1);
                                endCallIndex = passFile.LastIndexOf(')', endCallIndex) - 1;
                                int paramStartIndex = callIndex + "GetSurfaceAndBuiltinData(".Length;

                                string[] parameters = passFile.Substring(paramStartIndex, endCallIndex - paramStartIndex).Split(',');

                                shader.Append(passFile.Substring(0, callIndex));
                                shader.Append("ParticleGetSurfaceAndBuiltinData(");

                                var args = parameters.Take(1).Concat(Enumerable.Repeat("packedInput.vmesh.instanceID", 1).Concat(parameters.Skip(1)));

                                shader.Append(args.Aggregate((a, b) => a + "," + b));

                                shader.Append(passFile.Substring(endCallIndex));
                            }
                            else
                                shader.Append(passFile);
                        }
                        else if (trimmed == "HLSLPROGRAM")
                        {
                            string indentation = standardShader[i].Substring(0, standardShader[i].IndexOf('H'));
                            shader.AppendLine(standardShader[i]);
                            shader.AppendLine(indentation + "#define UNITY_VERTEX_INPUT_INSTANCE_ID uint instanceID : SV_InstanceID;");

                            shader.AppendLine(indentation + "#include \"Packages/com.unity.visualeffectgraph/Shaders/RenderPipeline/HDRP/VFXDefines.hlsl\"");
                            shader.AppendLine(indentation + "#include \"Packages/com.unity.visualeffectgraph/Shaders/RenderPipeline/HDRP/VFXCommon.cginc\"");
                            shader.AppendLine(indentation + "#include \"Packages/com.unity.visualeffectgraph/Shaders/VFXCommon.cginc\"");
                        }
                        else if( trimmed == "Tags{ \"RenderPipeline\"=\"HDRenderPipeline\" \"RenderType\" = \"HDLitShader\" }")
                        {
                            shader.AppendLine(SubShaderTags);
                        }
                        else if( ! trimmed.StartsWith("#pragma shader_feature_local")) // remove all feature_local pragmas
                        {
                            string str = standardShader[i];
                            foreach( var kv in guiVariables)
                            {
                                str = str.Replace("[" + kv.Key + "]", " " + kv.Value);
                            }
                            shader.AppendLine(str);
                        }
                        else if(firstFeatureLocal)
                        {
                            firstFeatureLocal = false;

                            foreach( var define in defines)
                                shader.AppendLine(string.Format("#define {0} {1}",define.Key,define.Value));
                        }
                    }
                    else
                    {
                        string indentation = standardShader[i].Substring(0, standardShader[i].IndexOf('#'));
                        shader.AppendLine(indentation + getSurfaceDataFunction.ToString().Replace("\n", "\n" + indentation));
                    }
                }
            }
            
            return shader.ToString();
        }

        private static void GenerateParticleVert(VFXInfos vfxInfos, StringBuilder shader, string indentation)
        {
            shader.Append(indentation + vfxInfos.vertexFunctions.Replace("\n","\n"+ indentation));

            shader.AppendLine(indentation + @"
PackedVaryingsType ParticleVert(AttributesMesh inputMesh)
{
    VaryingsType varyingsType;
    
    
    varyingsType.vmesh = VertMesh(inputMesh);
    uint index = inputMesh.instanceID;
".Replace("\n", "\n" + indentation));
            shader.Append(indentation + "\t" + vfxInfos.loadAttributes.Replace("\n", "\n\t" + indentation));

            shader.AppendLine(indentation + @"
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
	#ifdef VARYINGS_NEED_POSITION_WS
	    varyingsType.vmesh.positionRWS = TransformObjectToWorld(vPos);
    #endif
			
	varyingsType.vmesh.positionCS = TransformPositionVFXToClip(vPos);

    #ifdef ATTRIBUTES_NEED_NORMAL
        float3 normalWS = TransformObjectToWorldNormal(inputMesh.normalOS);
    #endif

    #ifdef ATTRIBUTES_NEED_TANGENT
        float4 tangentWS = float4(TransformObjectToWorldDir(inputMesh.tangentOS.xyz), inputMesh.tangentOS.w);
    #endif

    #ifdef VARYINGS_NEED_TANGENT_TO_WORLD
        varyingsType.vmesh.normalWS = normalWS;
        varyingsType.vmesh.tangentWS = tangentWS;
    #endif

    PackedVaryingsType result = PackVaryingsType(varyingsType);
    result.vmesh.instanceID = inputMesh.instanceID; // transmit the instanceID to the pixel shader through the varyings
");
            shader.Append(indentation + "\t" + vfxInfos.vertexShaderContent.Replace("\n","\n\t" + indentation));
            shader.Append(@"


    return result;
}
".Replace("\n", "\n" + indentation));

            shader.AppendLine(indentation + "#pragma vertex ParticleVert");
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


        public static string GenerateMeshAttributesStruct(Graph shaderGraph, int passName)
        {
            var requirements = shaderGraph.passes[passName].vertex.requirements.Union(shaderGraph.passes[passName].pixel.requirements);
            var vertexSlots = new ShaderStringBuilder();

            vertexSlots.AppendLine("struct AttributesMesh");
            vertexSlots.AppendLine("{");
            vertexSlots.IncreaseIndent();
            GenerateStructFields(requirements, vertexSlots, true);
            vertexSlots.DecreaseIndent();
            vertexSlots.AppendLine("};");

            return vertexSlots.ToString();
        }

        public static string GenerateMeshToPSStruct(Graph shaderGraph, int passName)
        {
            var requirements = shaderGraph.passes[passName].pixel.requirements;
            var pixelSlots = new ShaderStringBuilder();

            pixelSlots.AppendLine("struct VaryingsMeshToPS");
            pixelSlots.AppendLine("{");
            pixelSlots.IncreaseIndent();
            GenerateStructFields(requirements, pixelSlots, false);
            pixelSlots.AppendLine("uint instanceID : TEXCOORD9; ");
            pixelSlots.DecreaseIndent();
            pixelSlots.AppendLine("};");

            return pixelSlots.ToString();
        }

        public static string GeneratePackedMeshToPSStruct(Graph shaderGraph, int passName)
        {
            var requirements = shaderGraph.passes[passName].pixel.requirements;
            var pixelSlots = new ShaderStringBuilder();

            pixelSlots.AppendLine("struct PackedVaryingsMeshToPS");
            pixelSlots.AppendLine("{");
            pixelSlots.IncreaseIndent();
            GenerateStructFields(requirements, pixelSlots, false);
            pixelSlots.AppendLine("uint instanceID : TEXCOORD9; ");
            pixelSlots.DecreaseIndent();
            pixelSlots.AppendLine("};");

            return pixelSlots.ToString();
        }

        private static void GenerateVertexToPixelTransfers(ShaderGraphRequirements requirements, StringBuilder builder)
        {
            if ((requirements.requiresPosition & NeededCoordinateSpace.View) != 0)
                builder.AppendLine("o.positionCS = i.positionCS;");

            if ((requirements.requiresPosition & NeededCoordinateSpace.Object) != 0)
                builder.AppendLine("o.positionOS = i.positionOS;");

            if ((requirements.requiresPosition & NeededCoordinateSpace.World) != 0)
                builder.AppendLine("o.positionWS = i.positionWS;");

            if ((requirements.requiresNormal & NeededCoordinateSpace.Object) != 0)
                builder.AppendLine("o.normalOS = i.normalOS;");

            if ((requirements.requiresNormal & NeededCoordinateSpace.World) != 0)
                builder.AppendLine("o.normalWS = i.normalWS;");

            if ((requirements.requiresTangent & NeededCoordinateSpace.Object) != 0)
                builder.AppendLine("o.tangentOS = i.tangentOS;");

            if ((requirements.requiresTangent & NeededCoordinateSpace.World) != 0)
                builder.AppendLine("o.tangentWS = i.tangentWS;");
            for (int i = 0; i < 4; ++i)
            {
                if (requirements.requiresMeshUVs.Contains((UVChannel)i))
                    builder.AppendLine(string.Format("o.uv{0} = i.uv{0};", i));
            }
            if (requirements.requiresVertexColor)
                builder.AppendLine("o.color = i.color;");
        }

        private static void GenerateStructFields(ShaderGraphRequirements requirements, ShaderStringBuilder builder, bool computeWSCS)
        {
            if (!computeWSCS && (requirements.requiresPosition & NeededCoordinateSpace.View) != 0)
                builder.AppendLine("float4 positionCS : SV_POSITION;");

            if ((requirements.requiresPosition & NeededCoordinateSpace.Object) != 0)
                builder.AppendLine("float3 positionOS : POSITION0;");

            if (!computeWSCS && (requirements.requiresPosition & NeededCoordinateSpace.World) != 0)
                builder.AppendLine("float3 positionWS : POSITION1;");

            if ((requirements.requiresNormal & NeededCoordinateSpace.Object) != 0)
                builder.AppendLine("float4 normalOS : NORMAL0;");

            if (!computeWSCS && (requirements.requiresNormal & NeededCoordinateSpace.World) != 0)
                builder.AppendLine("float4 normalWS : NORMAL1;");

            if ((requirements.requiresTangent & NeededCoordinateSpace.Object) != 0)
                builder.AppendLine("float4 tangentOS : TANGENT0;");

            if (!computeWSCS && (requirements.requiresTangent & NeededCoordinateSpace.World) != 0)
                builder.AppendLine("float4 tangentWS : TANGENT0;");
            for (int i = 0; i < 4; ++i)
            {
                if (requirements.requiresMeshUVs.Contains((UVChannel)i))
                    builder.AppendLine(string.Format("float4 uv{0} : TEXCOORD{0};", i));
            }
            if (requirements.requiresVertexColor)
                builder.AppendLine("float4 color : COLOR;");
        }



        public static string GenerateSurfaceDescriptionStruct(int pass,Graph shaderGraph)
        {
            string pixelGraphOutputStructName = "SurfaceDescription";
            var pixelSlots = new ShaderStringBuilder();
            var graph = shaderGraph.graphData;

            //GraphUtil.GenerateSurfaceDescriptionStruct(pixelSlots, shaderGraph.slots.Where(t=> Graph.passInfos[pass].pixel.activeSlots.Contains(t.id)).ToList(), true, pixelGraphOutputStructName, null);

            return pixelSlots.ToString();
        }

        public static string GenerateSurfaceDescriptionFunction(int pass,Graph shaderGraph, out string functions)
        {
            var graph = shaderGraph.graphData;
            var pixelGraphEvalFunction = new ShaderStringBuilder();
            var activeNodeList = ListPool<AbstractMaterialNode>.Get();
            NodeUtils.DepthFirstCollectNodesFromNode(activeNodeList, ((AbstractMaterialNode)graph.outputNode), NodeUtils.IncludeSelf.Include, Enumerable.Range(1, 31).ToList()); // hardcoded hd pixel slots.

            string pixelGraphInputStructName = "SurfaceDescriptionInputs";
            string pixelGraphOutputStructName = "SurfaceDescription";
            string pixelGraphEvalFunctionName = "SurfaceDescriptionFunction";

            ShaderStringBuilder graphNodeFunctions = new ShaderStringBuilder();
            graphNodeFunctions.IncreaseIndent();
            var functionRegistry = new FunctionRegistry(graphNodeFunctions);
            var sharedProperties = new PropertyCollector();
            var pixelRequirements = ShaderGraphRequirements.FromNodes(activeNodeList, ShaderStageCapability.Fragment, false);



            GraphUtil.GenerateSurfaceDescriptionFunction(
                activeNodeList,
                shaderGraph.graphData.outputNode,
                shaderGraph.graphData as GraphData,
                pixelGraphEvalFunction,
                functionRegistry,
                sharedProperties,
                pixelRequirements,  // TODO : REMOVE UNUSED
                GenerationMode.ForReals,
                pixelGraphEvalFunctionName,
                pixelGraphOutputStructName,
                null,
                shaderGraph.slots.Where(t => Graph.passInfos[pass].pixel.activeSlots.Contains(t.id)).ToList(),
                pixelGraphInputStructName);

            ListPool<AbstractMaterialNode>.Release(activeNodeList);
            functions = graphNodeFunctions.ToString();
            return pixelGraphEvalFunction.ToString() ;
        }
    }
}
