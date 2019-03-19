using System;
using System.Text;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor.Graphing;
using UnityEditor.Graphing.Util;
using UnityEngine;

namespace UnityEditor.ShaderGraph
{
    //Small methods usable by the VFX since they are public
    public static class GraphUtilForVFX
    {
        public struct VFXAttribute
        {
            public string name;
            public string initialization;
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
                    remainingProperties.Add(string.Format("{0} {1}",prop.propertyType.ToString(),prop.referenceName));
                }
            }

            return remainingProperties;
        }

        public static string GenerateShader(Shader shaderGraph, ref VFXInfos vfxInfos)
        {
            Graph graph = LoadShaderGraph(shaderGraph);


            int currentPass = Graph.PassName.GBuffer;

            string shaderStart = @"
{
    SubShader
    {
        Tags { ""Queue"" = ""Geometry"" ""IgnoreProjector"" = ""False""}

";

            StringBuilder shader = new StringBuilder(shaderStart);

            //TODO add renderstate commands

            string hlslStart = @"
        HLSLINCLUDE
        #include ""Packages / com.unity.visualeffectgraph / Shaders / RenderPipeline / HDRP / VFXDefines.hlsl""


        ByteAddressBuffer attributeBuffer;
";
            shader.AppendLine(hlslStart);
            shader.AppendLine(vfxInfos.parameters);
            shader.AppendLine("\t\t" + vfxInfos.vertexFunctions.Replace("\n", "\n\t\t"));
            shader.AppendLine("\t\t" + GenerateMeshAttributesStruct(graph, currentPass).Replace("\n", "\n\t\t"));
            shader.AppendLine("\t\t" + GeneratePSInputStruct(graph, currentPass).Replace("\n", "\n\t\t"));

            shader.AppendLine(@"
        ENDHLSL");

            for (int i = 0; i < Graph.PassName.Count && i < Graph.passInfos.Length; ++i)
            {
                var passInfo = Graph.passInfos[i];
                var pass = graph.passes[i];


                shader.AppendLine(@"
        Pass
		{		
			Tags { ""LightMode""=""" + passInfo.name + @""");
            HLSLSTART");
                /***Vertex Shader***/
                string hlslNext = @"
            #pragma vertex vert
		    ps_input vert(AttributeMesh i, uint instanceID : SV_InstanceID)
		    {
			    ps_input o = (ps_input)0;
";
                shader.AppendLine(hlslNext);

                shader.Append("\t\t\t\t" + vfxInfos.loadAttributes.Replace("\n", "\n\t\t\t\t"));


                shader.AppendLine(
                @"if (!alive)
                    return o;");

                shader.Append("\t\t\t\t" + vfxInfos.vertexShaderContent.Replace("\n", "\n\t\t\t\t"));

                hlslNext = @"
                float3 size3 = float3(size,size,size);
				size3.x *= scaleX;
				size3.y *= scaleY;
				size3.z *= scaleZ;
                float4x4 elementToVFX = GetElementToVFXMatrix(axisX,axisY,axisZ,float3(angleX,angleY,angleZ),float3(pivotX,pivotY,pivotZ),size3,position);
			    float3 vPos = mul(elementToVFX,float4(i.posOS,1.0f)).xyz;
			    o.posCS = TransformPositionVFXToClip(vPos);
";
                shader.Append(hlslNext);

                if ((pass.pixel.requirements.requiresPosition & NeededCoordinateSpace.World) != 0)
                    shader.AppendLine(@"
                o.posWS =  TransformPositionVFXToWorld(vPos);
");
                if ((pass.pixel.requirements.requiresPosition & NeededCoordinateSpace.Object) != 0)
                    shader.AppendLine(@"
                o.posOS =  vPos;
");

                if ((pass.pixel.requirements.requiresNormal & NeededCoordinateSpace.Object) != 0)
                    shader.AppendLine(@"
                o.normalOS = i.normal;
");
                if ((pass.pixel.requirements.requiresNormal & NeededCoordinateSpace.World) != 0)
                    shader.AppendLine(@"
                float3 normalWS = normalize(TransformDirectionVFXToWorld(mul((float3x3)elementToVFX, i.normal)));
                o.normalWS = normalWS;
");
                if ((pass.pixel.requirements.requiresTangent & NeededCoordinateSpace.Object) != 0)
                    shader.AppendLine(@"
                o.tangentSS = i.tangent;
");
                if ((pass.pixel.requirements.requiresTangent & NeededCoordinateSpace.World) != 0)
                    shader.AppendLine(@"
                o.tangentWS = float4(normalize(TransformDirectionVFXToWorld(mul((float3x3)elementToVFX, i.tangent.xyz))), i.tangent.w);
");
                if (pass.pixel.requirements.requiresVertexColor)
                    shader.AppendLine(@"
                o.color : float4(color,a)");

                for (int uv = 0; uv < 4; ++uv)
                {
                    if (pass.pixel.requirements.requiresMeshUVs.Contains((UVChannel)uv))
                        shader.AppendFormat(@"
                o.uv{0} : i.uv{0};
"
                        , uv);
                }

                shader.Append(@"
                return o;
            }
");
                /*** ps_input -> FragInput ***/

                shader.AppendLine(@"
            #include ""Packages/com.unity.render-pipelines.high-definition/Runtime/RenderPipeline/ShaderPass/FragInputs.hlsl""

");


                //SurfaceDescriptionFunction

                var shaderProperties = new PropertyCollector();
                graph.graphData.CollectShaderProperties(shaderProperties, GenerationMode.ForReals);
                var vfxAttributesToshaderProperties = new StringBuilder();

                foreach( var prop in shaderProperties.properties)
                {
                    string matchingAttribute = vfxInfos.attributes.FirstOrDefault(t => prop.displayName.Equals(t, StringComparison.InvariantCultureIgnoreCase));
                    if (matchingAttribute != null)
                    {
                        vfxAttributesToshaderProperties.AppendLine(prop.GetPropertyDeclarationString("")+" = "+ matchingAttribute +";");
                    }
                }
             

                string surfaceDefinitionFunction = GenerateSurfaceDescriptionFunction(graph);
                // inject vfx load attributes.
                int firstBracketIndex = surfaceDefinitionFunction.IndexOf('{');
                if(firstBracketIndex> -1)
                {
                    while (surfaceDefinitionFunction.Length > firstBracketIndex+ 1 && "\n\r".Contains(surfaceDefinitionFunction[firstBracketIndex + 1]))
                        ++firstBracketIndex;

                    surfaceDefinitionFunction = surfaceDefinitionFunction.Substring(0, firstBracketIndex) +
                        "\t\t\t\t" + vfxInfos.loadAttributes.Replace("\n","\n\t") +
                        vfxAttributesToshaderProperties +
                        surfaceDefinitionFunction.Substring(firstBracketIndex);

                }

                shader.AppendLine("\t\t\t" + surfaceDefinitionFunction.Replace("\n", "\n\t\t\t"));


                shader.Append(@"
            ENDHLSL
        }
");

            }
            shader.AppendLine(@"
    }");

            return shader.ToString();
        }

        public class Graph
        {
            internal GraphData graphData;
            internal List<MaterialSlot> slots;

            internal struct Function
            {
                internal List<AbstractMaterialNode> nodes;
                internal ShaderGraphRequirements requirements;
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
                //new PassInfo("ShadowCaster",new FunctionInfo(Enumerable.Range(1, 31).ToList()),new FunctionInfo(Enumerable.Range(0,1).ToList())),
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


            graph.passes[Graph.PassName.GBuffer].pixel.nodes = ListPool<AbstractMaterialNode>.Get();
            NodeUtils.DepthFirstCollectNodesFromNode(graph.passes[Graph.PassName.GBuffer].pixel.nodes, ((AbstractMaterialNode)graph.graphData.outputNode), NodeUtils.IncludeSelf.Include, Graph.passInfos[Graph.PassName.GBuffer].pixel.activeSlots);
            graph.passes[Graph.PassName.GBuffer].vertex.nodes = ListPool<AbstractMaterialNode>.Get();
            NodeUtils.DepthFirstCollectNodesFromNode(graph.passes[Graph.PassName.GBuffer].vertex.nodes, ((AbstractMaterialNode)graph.graphData.outputNode), NodeUtils.IncludeSelf.Include, Graph.passInfos[Graph.PassName.GBuffer].vertex.activeSlots);

            graph.passes[Graph.PassName.GBuffer].pixel.requirements = ShaderGraphRequirements.FromNodes(graph.passes[Graph.PassName.GBuffer].pixel.nodes, ShaderStageCapability.Fragment, false);
            graph.passes[Graph.PassName.GBuffer].vertex.requirements = ShaderGraphRequirements.FromNodes(graph.passes[Graph.PassName.GBuffer].vertex.nodes, ShaderStageCapability.Vertex, false);

            graph.passes[Graph.PassName.GBuffer].pixel.requirements.requiresPosition |= NeededCoordinateSpace.View;
            graph.passes[Graph.PassName.GBuffer].vertex.requirements.requiresPosition |= NeededCoordinateSpace.Object;

            return graph;
        }


        public static string GenerateMeshAttributesStruct(Graph shaderGraph, int passName)
        {
            var requirements = shaderGraph.passes[passName].vertex.requirements.Union(shaderGraph.passes[passName].pixel.requirements);
            var vertexSlots = new ShaderStringBuilder();

            vertexSlots.AppendLine("struct AttributeMesh");
            vertexSlots.AppendLine("{");
            vertexSlots.IncreaseIndent();
            GenerateStructFields(requirements, vertexSlots,true);
            vertexSlots.DecreaseIndent();
            vertexSlots.AppendLine("}");

            return vertexSlots.ToString();
        }

        public static string GeneratePSInputStruct(Graph shaderGraph, int passName)
        {
            var requirements = shaderGraph.passes[passName].pixel.requirements;
            var pixelSlots = new ShaderStringBuilder();

            pixelSlots.AppendLine("struct ps_input");
            pixelSlots.AppendLine("{");
            pixelSlots.IncreaseIndent();
            GenerateStructFields(requirements, pixelSlots,false);
            pixelSlots.DecreaseIndent();
            pixelSlots.AppendLine("}");

            return pixelSlots.ToString();
        }

        private static void GenerateVertexToPixelTransfers(ShaderGraphRequirements requirements, ShaderStringBuilder builder)
        {
            if ((requirements.requiresPosition & NeededCoordinateSpace.View) != 0)
                builder.AppendLine("o.posCS = i.posCS");

            if ((requirements.requiresPosition & NeededCoordinateSpace.Object) != 0)
                builder.AppendLine("o.posOS = i.posOS");

            if ((requirements.requiresPosition & NeededCoordinateSpace.World) != 0)
                builder.AppendLine("o.posWS = i.posWS");

            if ((requirements.requiresNormal & NeededCoordinateSpace.Object) != 0)
                builder.AppendLine("o.normalOS = i.normalOS");

            if ((requirements.requiresNormal & NeededCoordinateSpace.World) != 0)
                builder.AppendLine("o.normalWS = i.normalWS");

            if ((requirements.requiresTangent & NeededCoordinateSpace.Object) != 0)
                builder.AppendLine("o.tangentOS = i.tangentOS");

            if ((requirements.requiresTangent & NeededCoordinateSpace.World) != 0)
                builder.AppendLine("o.tangentWS = i.tangentWS");
            for (int i = 0; i < 4; ++i)
            {
                if (requirements.requiresMeshUVs.Contains((UVChannel)i))
                    builder.AppendLine(string.Format("o.uv{0} = i.uv{0}", i));
            }
            if (requirements.requiresVertexColor)
                builder.AppendLine("o.color = i.color");
        }

        private static void GenerateStructFields(ShaderGraphRequirements requirements, ShaderStringBuilder builder, bool computeWSCS)
        {
            if (!computeWSCS &&(requirements.requiresPosition & NeededCoordinateSpace.View) != 0)
                builder.AppendLine("float3 posCS : SV_POSITION");

            if ((requirements.requiresPosition & NeededCoordinateSpace.Object) != 0)
                builder.AppendLine("float4 posOS : POSITION0");

            if (!computeWSCS && (requirements.requiresPosition & NeededCoordinateSpace.World) != 0)
                builder.AppendLine("float4 posWS : POSITION1");

            if ((requirements.requiresNormal & NeededCoordinateSpace.Object) != 0)
                builder.AppendLine("float4 normalOS : NORMAL0");

            if (!computeWSCS && (requirements.requiresNormal & NeededCoordinateSpace.World) != 0)
                builder.AppendLine("float4 normalWS : NORMAL1");

            if ((requirements.requiresTangent & NeededCoordinateSpace.Object) != 0)
                builder.AppendLine("float4 tangentOS : TANGENT0");

            if (!computeWSCS && (requirements.requiresTangent & NeededCoordinateSpace.World) != 0)
                builder.AppendLine("float4 tangentWS : TANGENT0");
            for (int i = 0; i < 4; ++i)
            {
                if (requirements.requiresMeshUVs.Contains((UVChannel)i))
                    builder.AppendLine(string.Format("float4 uv{0} : TEXCOORD{0}", i));
            }
            if (requirements.requiresVertexColor)
                builder.AppendLine("float4 color : COLOR");
        }



        public static string GenerateSurfaceDescriptionStruct(Graph shaderGraph)
        {
            string pixelGraphOutputStructName = "SurfaceDescription";
            var pixelSlots = new ShaderStringBuilder();
            var graph = shaderGraph.graphData;

            GraphUtil.GenerateSurfaceDescriptionStruct(pixelSlots, shaderGraph.slots, true, pixelGraphOutputStructName, null);

            return pixelSlots.ToString();
        }

        public static string GenerateSurfaceDescriptionFunction(Graph shaderGraph)
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
                shaderGraph.slots,
                pixelGraphInputStructName);

            ListPool<AbstractMaterialNode>.Release(activeNodeList);
            return pixelGraphEvalFunction.ToString();
        }
    }
}
