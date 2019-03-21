using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor.Graphing;
using UnityEditor.Graphing.Util;
using UnityEngine;

namespace UnityEditor.ShaderGraph
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


        public static string NewGenerateShader(Shader shaderGraph, ref VFXInfos vfxInfos)
        {
            Graph graph = LoadShaderGraph(shaderGraph);
            var getSurfaceDataFunction = new ShaderStringBuilder();

            string shaderGraphCode;
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
                shaderGraphCode = sg.GetShaderString(0);
            }

            getSurfaceDataFunction.Append(@"

ByteAddressBuffer attributeBuffer;

void GetSurfaceAndBuiltinData(FragInputs input, float3 V, inout PositionInputs posInput, out SurfaceData surfaceData, out BuiltinData builtinData)
{
    surfaceData = (SurfaceData)0;
    builtinData = (BuiltinData)0;

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

            getSurfaceDataFunction.Append(@"
    float alpha = 1;

    // Perform alha test very early to save performance (a killed pixel will not sample textures)
    #if defined(_ALPHATEST_ON) && !defined(LAYERED_LIT_SHADER)
        float alphaCutoff = _AlphaCutoff;
        #ifdef CUTOFF_TRANSPARENT_DEPTH_PREPASS
        alphaCutoff = _AlphaCutoffPrepass;
        #elif defined(CUTOFF_TRANSPARENT_DEPTH_POSTPASS)
        alphaCutoff = _AlphaCutoffPostpass;
        #endif
    #if SHADERPASS == SHADERPASS_SHADOWS 
        DoAlphaTest(alpha, _UseShadowThreshold ? _AlphaCutoffShadow : alphaCutoff);
    #else
        DoAlphaTest(alpha, alphaCutoff);
    #endif
    #endif
");


            getSurfaceDataFunction.Append(@"
}");


        getSurfaceDataFunction.Append(@"
void ApplyVertexModification(AttributesMesh input, float3 normalWS, inout float3 positionRWS, float4 time)
{

}
                ");

            string[] standardShader = File.ReadAllLines("Packages/com.unity.render-pipelines.high-definition/Runtime/Material/Lit/Lit.shader");

            var shader = new StringBuilder();
            bool withinProperties = false;
            bool propertiesSkipped = false;
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

                            shader.AppendLine(@"
PackedVaryingsType ParticleVert(AttributesMesh inputMesh, uint instanceID : SV_InstanceID)
{
    VaryingsType varyingsType;
    
    
    varyingsType.vmesh = VertMesh(inputMesh);
    PackedVaryingsType result = PackVaryingsType(varyingsType);
    result.instanceID = instanceID; // transmit the instanceID to the pixel shader through the varyings

    return result;
}
".Replace("\n", "\n" + indentation));

                            shader.AppendLine(indentation + "#pragma vertex ParticleVert");
                        }
                        else
                            shader.AppendLine(standardShader[i]);
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

        public static string GenerateShader(Shader shaderGraph, ref VFXInfos vfxInfos)
        {
            Graph graph = LoadShaderGraph(shaderGraph);



            string shaderStart = @"
{
    SubShader
    {
        Tags { ""Queue"" = ""Geometry"" ""IgnoreProjector"" = ""False""}

";

            StringBuilder shader = new StringBuilder(shaderStart);

            //TODO add renderstate commands

            for (int currentPass = 0; currentPass < Graph.PassName.Count && currentPass < Graph.passInfos.Length; ++currentPass)
            {
                var passInfo = Graph.passInfos[currentPass];
                var pass = graph.passes[currentPass];


                shader.AppendLine(@"
        Pass
		{		
			Tags { ""LightMode""=""" + passInfo.name + @""" }
            HLSLPROGRAM

            #pragma target 4.5
            #pragma only_renderers d3d11 ps4 xboxone vulkan metal switch

            #include ""Packages/com.unity.visualeffectgraph/Shaders/RenderPipeline/HDRP/VFXDefines.hlsl""
		    #include ""Packages/com.unity.visualeffectgraph/Shaders/RenderPipeline/HDRP/VFXCommon.cginc""
		    #include ""Packages/com.unity.visualeffectgraph/Shaders/VFXCommon.cginc""

            #include ""Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl""

            # include ""Packages/com.unity.render-pipelines.core/ShaderLibrary/NormalSurfaceGradient.hlsl""

            #pragma multi_compile _ WRITE_NORMAL_BUFFER
            #pragma multi_compile _ WRITE_MSAA_DEPTH

            #include ""Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl""
        #ifdef DEBUG_DISPLAY
            #include ""Packages/com.unity.render-pipelines.high-definition/Runtime/Debug/DebugDisplay.hlsl""
        #endif
        
            #include ""Packages/com.unity.render-pipelines.high-definition/Runtime/Material/Material.hlsl""
        
        #if (SHADERPASS == SHADERPASS_FORWARD)
            #include ""Packages/com.unity.render-pipelines.high-definition/Runtime/Lighting/Lighting.hlsl""
        
            #define HAS_LIGHTLOOP
        
            #include ""Packages/com.unity.render-pipelines.high-definition/Runtime/Lighting/LightLoop/LightLoopDef.hlsl""
            #include ""Packages/com.unity.render-pipelines.high-definition/Runtime/Material/Lit/Lit.hlsl""
            #include ""Packages/com.unity.render-pipelines.high-definition/Runtime/Lighting/LightLoop/LightLoop.hlsl""
        #else
            #include ""Packages/com.unity.render-pipelines.high-definition/Runtime/Material/Lit/Lit.hlsl""
        #endif
        
            #include ""Packages/com.unity.render-pipelines.high-definition/Runtime/RenderPipeline/ShaderPass/FragInputs.hlsl""

            #include ""Packages/com.unity.render-pipelines.high-definition/Runtime/Material/BuiltinUtilities.hlsl""
            #include ""Packages/com.unity.render-pipelines.high-definition/Runtime/Material/MaterialUtilities.hlsl""
            #include ""Packages/com.unity.render-pipelines.high-definition/Runtime/Material/Decal/DecalUtilities.hlsl""
            #include ""Packages/com.unity.render-pipelines.high-definition/Runtime/Material/Lit/LitDecalData.hlsl""
            #include ""Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderGraphFunctions.hlsl""


            ByteAddressBuffer attributeBuffer;
");
                shader.AppendLine("\t\t" + vfxInfos.parameters.Replace("\n", "\n\t\t"));
                shader.AppendLine("\t\t" + vfxInfos.vertexFunctions.Replace("\n", "\n\t\t"));
                shader.AppendLine("\t\t" + GenerateMeshAttributesStruct(graph, currentPass).Replace("\n", "\n\t\t"));
                shader.AppendLine("\t\t" + GenerateMeshToPSStruct(graph, currentPass).Replace("\n", "\n\t\t"));
                shader.AppendLine("\t\t" + GeneratePackedMeshToPSStruct(graph, currentPass).Replace("\n", "\n\t\t"));

                shader.Append(@"
        PackedVaryingsMeshToPS PackVaryingsMeshToPS(VaryingsMeshToPS i)
        {
            PackedVaryingsMeshToPS o;
            o = (PackedVaryingsMeshToPS)0;
");
                var copyVarying = new StringBuilder();
                GenerateVertexToPixelTransfers(pass.pixel.requirements, copyVarying);

                shader.Append("\t\t\t" + copyVarying.ToString().Replace("\n", "\n\t\t\t"));

                shader.Append(@"
            o.instanceID = i.instanceID;
            return o;
        }

        VaryingsMeshToPS UnpackVaryingsMeshToPS(PackedVaryingsMeshToPS i)
        {
            VaryingsMeshToPS o;
            o = (VaryingsMeshToPS)0;
");
                shader.Append("\t\t\t" + copyVarying.ToString().Replace("\n", "\n\t\t\t"));

                shader.AppendLine(@"
            o.instanceID = i.instanceID;
            return o;
        }
");


                /***Vertex Shader***/
                string hlslNext = @"
            #pragma vertex vert
		    VaryingsMeshToPS vert(AttributesMesh i, uint instanceID : SV_InstanceID)
		    {
				uint index = instanceID;
			    VaryingsMeshToPS o = (VaryingsMeshToPS)0;
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
			    float3 vPos = mul(elementToVFX,float4(i.positionOS.xyz,1.0f)).xyz;
			    o.positionCS = TransformPositionVFXToClip(vPos);
                o.positionWS =  TransformPositionVFXToWorld(vPos);
                o.instanceID = instanceID; // needed by pixel stuff using attributes
";
                shader.Append(hlslNext);

                if ((pass.pixel.requirements.requiresPosition & NeededCoordinateSpace.Object) != 0)
                    shader.Append(@"
                o.positionOS =  vPos;
");

                if ((pass.pixel.requirements.requiresNormal & NeededCoordinateSpace.Object) != 0)
                    shader.Append(@"
                o.normalOS = i.normal;
");
                if ((pass.pixel.requirements.requiresNormal & NeededCoordinateSpace.World) != 0)
                    shader.Append(@"
                float3 normalWS = normalize(TransformDirectionVFXToWorld(mul((float3x3)elementToVFX, i.normal)));
                o.normalWS = normalWS;
");
                if ((pass.pixel.requirements.requiresTangent & NeededCoordinateSpace.Object) != 0)
                    shader.Append(@"
                o.tangentOS = i.tangent;
");
                if ((pass.pixel.requirements.requiresTangent & NeededCoordinateSpace.World) != 0)
                    shader.Append(@"
                o.tangentWS = float4(normalize(TransformDirectionVFXToWorld(mul((float3x3)elementToVFX, i.tangent.xyz))), i.tangent.w);
");
                if (pass.pixel.requirements.requiresVertexColor)
                    shader.Append(@"
                o.color = float4(color,a);
");

                for (int uv = 0; uv < 4; ++uv)
                {
                    if (pass.pixel.requirements.requiresMeshUVs.Contains((UVChannel)uv))
                        shader.AppendFormat(@"
                o.uv{0} = i.uv{0};
"
                        , uv);
                }

                shader.Append(@"
                return o;
            }
");
                /*** VaryingsMeshToPS -> FragInput ***/

                shader.AppendLine(
            @"
            FragInputs UnpackVaryingsMeshToFragInputs(VaryingsMeshToPS i)
            {
                FragInputs o = (FragInputs)0;
                o.worldToTangent = k_identity3x3;
                o.positionSS = i.positionCS;
");


                //if ((pass.pixel.requirements.requiresPosition & NeededCoordinateSpace.World) != 0) //required
                shader.Append(@"
                o.positionRWS = i.positionWS;
");/*
                if ((pass.pixel.requirements.requiresPosition & NeededCoordinateSpace.Object) != 0)
                    shader.AppendLine(@"
                o.positionOS = i.posOS;
");

                if ((pass.pixel.requirements.requiresNormal & NeededCoordinateSpace.Object) != 0)
                    shader.AppendLine(@"
                o.normalOS = i.normalOS;
");
                if ((pass.pixel.requirements.requiresNormal & NeededCoordinateSpace.World) != 0)
                    shader.AppendLine(@"
                o.normalWS = i.normalWS;
");
                if ((pass.pixel.requirements.requiresTangent & NeededCoordinateSpace.Object) != 0)
                    shader.AppendLine(@"
                o.tangentOS = i.tangentOS;
");
                if ((pass.pixel.requirements.requiresTangent & NeededCoordinateSpace.World) != 0)
                    shader.AppendLine(@"
                o.tangentWS = i.tangentWS;
");*/
                if (pass.pixel.requirements.requiresVertexColor)
                    shader.Append(@"
                o.color = i.color;");

                for (int uv = 0; uv < 4; ++uv)
                {
                    if (pass.pixel.requirements.requiresMeshUVs.Contains((UVChannel)uv))
                        shader.AppendFormat(@"
                o.texCoord{0} = i.uv{0};
"
                        , uv);
                }
                shader.AppendLine(@"
                return o;
            }
");
                //SurfaceDescriptionInput
                var surfaceInputBuilder = new ShaderStringBuilder();
                surfaceInputBuilder.IncreaseIndent(3);
                GraphUtil.GenerateSurfaceInputStruct(surfaceInputBuilder, graph.passes[currentPass].pixel.requirements, "SurfaceDescriptionInputs");

                //inject instanceID in surfacedescriptioninput
                shader.AppendLine(surfaceInputBuilder.ToString().Replace("}", "uint instanceID;\n\t\t\t}"));

                //FragInput to ShaderDescriptionInput

                shader.AppendLine(@"
            SurfaceDescriptionInputs FragInputsToSurfaceDescriptionInputs(FragInputs input, float3 viewWS)
             {
                SurfaceDescriptionInputs output;
                ZERO_INITIALIZE(SurfaceDescriptionInputs, output);
");
                if ((pass.pixel.requirements.requiresNormal & NeededCoordinateSpace.World) != 0)
                    shader.Append(@"
                output.WorldSpaceNormal = normalize(input.worldToTangent[2].xyz);
");
                if ((pass.pixel.requirements.requiresNormal & NeededCoordinateSpace.Object) != 0)
                    shader.Append(@"
                output.ObjectSpaceNormal = mul(output.WorldSpaceNormal, (float3x3)UNITY_MATRIX_M);           // transposed multiplication by inverse matrix to handle normal scale
");
                if ((pass.pixel.requirements.requiresNormal & NeededCoordinateSpace.View) != 0)
                    shader.Append(@"
                output.ViewSpaceNormal = mul(output.WorldSpaceNormal, (float3x3)UNITY_MATRIX_I_V);         // transposed multiplication by inverse matrix to handle normal scale
");
                if ((pass.pixel.requirements.requiresNormal & NeededCoordinateSpace.Tangent) != 0)
                    shader.Append(@"
                output.TangentSpaceNormal = float3(0.0f, 0.0f, 1.0f);
");

                if ((pass.pixel.requirements.requiresTangent & NeededCoordinateSpace.World) != 0)
                    shader.Append(@"
                output.WorldSpaceTangent = input.worldToTangent[0].xyz;
");
                if ((pass.pixel.requirements.requiresTangent & NeededCoordinateSpace.Object) != 0)
                    shader.Append(@"
                output.ObjectSpaceTangent = TransformWorldToObjectDir(output.WorldSpaceTangent);
");
                if ((pass.pixel.requirements.requiresTangent & NeededCoordinateSpace.View) != 0)
                    shader.Append(@"
                 output.ViewSpaceTangent = TransformWorldToViewDir(output.WorldSpaceTangent);
");
                if ((pass.pixel.requirements.requiresTangent & NeededCoordinateSpace.Tangent) != 0)
                    shader.Append(@"
                output.TangentSpaceTangent = float3(1.0f, 0.0f, 0.0f);
");
                if ((pass.pixel.requirements.requiresBitangent & NeededCoordinateSpace.World) != 0)
                    shader.Append(@"
                output.WorldSpaceBiTangent = input.worldToTangent[1].xyz;
");
                if ((pass.pixel.requirements.requiresBitangent & NeededCoordinateSpace.Object) != 0)
                    shader.Append(@"
                output.ObjectSpaceBiTangent = TransformWorldToObjectDir(output.WorldSpaceBiTangent);
");
                if ((pass.pixel.requirements.requiresBitangent & NeededCoordinateSpace.View) != 0)
                    shader.Append(@"
                 output.ViewSpaceBiTangent = TransformWorldToViewDir(output.WorldSpaceBiTangent);
");
                if ((pass.pixel.requirements.requiresBitangent & NeededCoordinateSpace.Tangent) != 0)
                    shader.Append(@"
                output.TangentSpaceBiTangent = float3(0.0f, 1.0f, 0.0f);
");
                if ((pass.pixel.requirements.requiresViewDir & NeededCoordinateSpace.World) != 0)
                    shader.Append(@"
                output.WorldSpaceViewDirection = normalize(viewWS);
");
                if ((pass.pixel.requirements.requiresViewDir & NeededCoordinateSpace.Object) != 0)
                    shader.Append(@"
                output.ObjectSpaceViewDirection = TransformWorldToObjectDir(output.WorldSpaceViewDirection);
");
                if ((pass.pixel.requirements.requiresViewDir & NeededCoordinateSpace.View) != 0)
                    shader.Append(@"
                output.ViewSpaceViewDirection = TransformWorldToViewDir(output.WorldSpaceViewDirection);
");
                if ((pass.pixel.requirements.requiresViewDir & NeededCoordinateSpace.Tangent) != 0)
                    shader.Append(@"
                float3x3 tangentSpaceTransform = float3x3(output.WorldSpaceTangent, output.WorldSpaceBiTangent, output.WorldSpaceNormal);
                output.TangentSpaceViewDirection = mul(tangentSpaceTransform, output.WorldSpaceViewDirection);
");
                if ((pass.pixel.requirements.requiresPosition & NeededCoordinateSpace.World) != 0)
                    shader.Append(@"
                output.WorldSpacePosition = GetAbsolutePositionWS(input.positionRWS);
");
                if ((pass.pixel.requirements.requiresPosition & NeededCoordinateSpace.Object) != 0)
                    shader.Append(@"
                output.ObjectSpacePosition = TransformWorldToObject(input.positionRWS);
");
                if ((pass.pixel.requirements.requiresPosition & NeededCoordinateSpace.View) != 0)
                    shader.Append(@"
                output.ViewSpacePosition = TransformWorldToView(input.positionRWS);
");
                if ((pass.pixel.requirements.requiresPosition & NeededCoordinateSpace.Tangent) != 0)
                    shader.Append(@"
                output.TangentSpacePosition = float3(0.0f, 0.0f, 0.0f);
");
                if (pass.pixel.requirements.requiresScreenPosition)
                    shader.Append(@"
                output.ScreenPosition = ComputeScreenPos(TransformWorldToHClip(input.positionRWS), _ProjectionParams.x);
");
                for (int j = 0; j < 4; ++j)
                {
                    if (pass.pixel.requirements.requiresMeshUVs.Contains((UVChannel)j))
                        shader.AppendFormat(@"
                output.uv0 = input.texCoord{0};
", j);
                }
                if (pass.pixel.requirements.requiresVertexColor)
                    shader.Append(@"
                output.VertexColor = input.color;
");
                if (pass.pixel.requirements.requiresFaceSign)
                    shader.Append(@"
                output.FaceSign = input.isFrontFace;
");

                shader.AppendLine(@"
                return output;
            }
");
                //Surface Description

                shader.AppendLine("\t\t\t" + GenerateSurfaceDescriptionStruct(currentPass,graph).Replace("\n","\n\t\t\t"));

                //SurfaceDescriptionFunction

                var shaderProperties = new PropertyCollector();
                graph.graphData.CollectShaderProperties(shaderProperties, GenerationMode.ForReals);
                var vfxAttributesToshaderProperties = new StringBuilder();

                foreach (var prop in shaderProperties.properties)
                {
                    string matchingAttribute = vfxInfos.attributes.FirstOrDefault(t => prop.displayName.Equals(t, StringComparison.InvariantCultureIgnoreCase));
                    if (matchingAttribute != null)
                    {
                        if(matchingAttribute == "color")
                            vfxAttributesToshaderProperties.AppendLine(prop.GetPropertyDeclarationString("") + " = float4(color,alpha);");
                        else
                            vfxAttributesToshaderProperties.AppendLine(prop.GetPropertyDeclarationString("") + " = " + matchingAttribute + ";");
                    }
                }

                string shaderGraphfunctions;
                string surfaceDefinitionFunction = GenerateSurfaceDescriptionFunction(currentPass,graph, out shaderGraphfunctions);
                // inject vfx load attributes.
                int firstBracketIndex = surfaceDefinitionFunction.IndexOf('{');
                if (firstBracketIndex > -1)
                {
                    while (surfaceDefinitionFunction.Length > firstBracketIndex + 1 && "\n\r".Contains(surfaceDefinitionFunction[firstBracketIndex + 1]))
                        ++firstBracketIndex;

                    surfaceDefinitionFunction = surfaceDefinitionFunction.Substring(0, firstBracketIndex) +
                        "\t\t\t\t uint index = IN.instanceID;\n" + 
                        "\t\t\t\t" + vfxInfos.loadAttributes.Replace("\n", "\n\t") +
                        vfxAttributesToshaderProperties +
                        surfaceDefinitionFunction.Substring(firstBracketIndex);

                }
                shader.AppendLine("\t\t\t" + shaderGraphfunctions.Replace("\n", "\n\t\t\t"));
                shader.AppendLine("\t\t\t" + surfaceDefinitionFunction.Replace("\n", "\n\t\t\t"));

                shader.Append(@"
void BuildSurfaceData(FragInputs fragInputs, inout SurfaceDescription surfaceDescription, float3 V, PositionInputs posInput, out SurfaceData surfaceData, out float3 bentNormalWS)
            {
                // setup defaults -- these are used if the graph doesn't output a value
                ZERO_INITIALIZE(SurfaceData, surfaceData);
        
                // copy across graph values, if defined
");
                if (pass.pixel.slots.Any(t => t.shaderOutputName == "Albedo"))
                    shader.Append(@"
                surfaceData.baseColor = surfaceDescription.Albedo;
");
                if (pass.pixel.slots.Any(t => t.shaderOutputName == "Smoothness"))
                    shader.Append(@"
                surfaceData.perceptualSmoothness = surfaceDescription.Smoothness;
");
                if (pass.pixel.slots.Any(t => t.shaderOutputName == "Occlusion"))
                    shader.Append(@"
                surfaceData.ambientOcclusion = surfaceDescription.Occlusion;
");
                if (pass.pixel.slots.Any(t => t.shaderOutputName == "SpecularOcclusion"))
                    shader.Append(@"
                surfaceData.specularOcclusion = surfaceDescription.SpecularOcclusion;
");
                if (pass.pixel.slots.Any(t => t.shaderOutputName == "Metallic"))
                    shader.Append(@"
                surfaceData.metallic = surfaceDescription.Metallic;
");
                if (pass.pixel.slots.Any(t => t.shaderOutputName == "SubsurfaceMask"))
                    shader.Append(@"
                surfaceData.subsurfaceMask = surfaceDescription.SubsurfaceMask;
");
                if (pass.pixel.slots.Any(t => t.shaderOutputName == "Thickness"))
                    shader.Append(@"
                surfaceData.thickness = surfaceDescription.Thickness;
");
                if (pass.pixel.slots.Any(t => t.shaderOutputName == "DiffusionProfileHash"))
                    shader.Append(@"
                surfaceData.diffusionProfileHash = surfaceDescription.DiffusionProfileHash;
");
                if (pass.pixel.slots.Any(t => t.shaderOutputName == "Specular"))
                    shader.Append(@"
                surfaceData.specularColor = surfaceDescription.Specular;
");
                if (pass.pixel.slots.Any(t => t.shaderOutputName == "CoatMask"))
                    shader.Append(@"
                surfaceData.coatMask = surfaceDescription.CoatMask;
");
                if (pass.pixel.slots.Any(t => t.shaderOutputName == "Anisotropy"))
                    shader.Append(@"
                surfaceData.anisotropy = surfaceDescription.Anisotropy;
");
                if (pass.pixel.slots.Any(t => t.shaderOutputName == "IridescenceMask"))
                    shader.Append(@"
                surfaceData.iridescenceMask = surfaceDescription.IridescenceMask;
");
                if (pass.pixel.slots.Any(t => t.shaderOutputName == "IridescenceThickness"))
                    shader.Append(@"
                surfaceData.iridescenceThickness = surfaceDescription.IridescenceThickness;
");

                shader.Append(@"
        #ifdef _HAS_REFRACTION
                if (_EnableSSRefraction)
                {
        
                    surfaceData.transmittanceMask = (1.0 - surfaceDescription.Alpha);
                    surfaceDescription.Alpha = 1.0;
                }
                else
                {
                    surfaceData.ior = 1.0;
                    surfaceData.transmittanceColor = float3(1.0, 1.0, 1.0);
                    surfaceData.atDistance = 1.0;
                    surfaceData.transmittanceMask = 0.0;
                    surfaceDescription.Alpha = 1.0;
                }
        #else
                surfaceData.ior = 1.0;
                surfaceData.transmittanceColor = float3(1.0, 1.0, 1.0);
                surfaceData.atDistance = 1.0;
                surfaceData.transmittanceMask = 0.0;
        #endif
                
                // These static material feature allow compile time optimization
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
        
        #ifdef _MATERIAL_FEATURE_IRIDESCENCE
                surfaceData.materialFeatures |= MATERIALFEATUREFLAGS_LIT_IRIDESCENCE;
        #endif
        #ifdef _MATERIAL_FEATURE_SPECULAR_COLOR
                surfaceData.materialFeatures |= MATERIALFEATUREFLAGS_LIT_SPECULAR_COLOR;
        #endif
        
        #if defined (_MATERIAL_FEATURE_SPECULAR_COLOR) && defined (_ENERGY_CONSERVING_SPECULAR)
                // Require to have setup baseColor
                // Reproduce the energy conservation done in legacy Unity. Not ideal but better for compatibility and users can unchek it
                surfaceData.baseColor *= (1.0 - Max3(surfaceData.specularColor.r, surfaceData.specularColor.g, surfaceData.specularColor.b));
        #endif
                float3 doubleSidedConstants = float3(1.0, 1.0, 1.0);
                
");
                if (pass.pixel.slots.Any(t => t.shaderOutputName == "Normal"))
                    shader.Append(@"
                // tangent-space normal
                float3 normalTS = float3(0.0f, 0.0f, 1.0f);
                normalTS = surfaceDescription.Normal;
        
                // compute world space normal
                GetNormalWS(fragInputs, normalTS, surfaceData.normalWS, doubleSidedConstants);
        
                bentNormalWS = surfaceData.normalWS;
        
                surfaceData.geomNormalWS = fragInputs.worldToTangent[2];
");

                if (pass.pixel.slots.Any(t => t.shaderOutputName == "Tangent"))
                    shader.Append(@"
                surfaceData.tangentWS = normalize(fragInputs.worldToTangent[0].xyz);    // The tangent is not normalize in worldToTangent for mikkt. TODO: Check if it expected that we normalize with Morten. Tag: SURFACE_GRADIENT
                surfaceData.tangentWS = Orthonormalize(surfaceData.tangentWS, surfaceData.normalWS);
");
                if (pass.pixel.slots.Any(t => t.shaderOutputName == "SpecularOcclusion"))
                    shader.Append(@"
                // By default we use the ambient occlusion with Tri-ace trick (apply outside) for specular occlusion.
                // If user provide bent normal then we process a better term
        #if defined(_SPECULAR_OCCLUSION_CUSTOM)
                // Just use the value passed through via the slot (not active otherwise)
        #elif defined(_SPECULAR_OCCLUSION_FROM_AO_BENT_NORMAL)
                // If we have bent normal and ambient occlusion, process a specular occlusion
                surfaceData.specularOcclusion = GetSpecularOcclusionFromBentAO(V, bentNormalWS, surfaceData.normalWS, surfaceData.ambientOcclusion, PerceptualSmoothnessToPerceptualRoughness(surfaceData.perceptualSmoothness));
        #elif defined(_AMBIENT_OCCLUSION) && defined(_SPECULAR_OCCLUSION_FROM_AO)
                surfaceData.specularOcclusion = GetSpecularOcclusionFromAmbientOcclusion(ClampNdotV(dot(surfaceData.normalWS, V)), surfaceData.ambientOcclusion, PerceptualSmoothnessToRoughness(surfaceData.perceptualSmoothness));
        #else
                surfaceData.specularOcclusion = 1.0;
        #endif
");

        shader.Append(@"
        #if HAVE_DECALS
                if (_EnableDecals)
                {
                    DecalSurfaceData decalSurfaceData = GetDecalSurfaceData(posInput, surfaceDescription.Alpha);
                    ApplyDecalToSurfaceData(decalSurfaceData, surfaceData);
                }
        #endif
        
        #ifdef _ENABLE_GEOMETRIC_SPECULAR_AA
                surfaceData.perceptualSmoothness = GeometricNormalFiltering(surfaceData.perceptualSmoothness, fragInputs.worldToTangent[2], surfaceDescription.SpecularAAScreenSpaceVariance, surfaceDescription.SpecularAAThreshold);
        #endif
        
        #ifdef DEBUG_DISPLAY
                if (_DebugMipMapMode != DEBUGMIPMAPMODE_NONE)
                {
                    // TODO: need to update mip info
                    surfaceData.metallic = 0;
                }
        
                // We need to call ApplyDebugToSurfaceData after filling the surfarcedata and before filling builtinData
                // as it can modify attribute use for static lighting
                ApplyDebugToSurfaceData(fragInputs.worldToTangent, surfaceData);
        #endif
            }
        
            void GetSurfaceAndBuiltinData(FragInputs fragInputs, float3 V, inout PositionInputs posInput, out SurfaceData surfaceData, out BuiltinData builtinData)
            {
        #ifdef LOD_FADE_CROSSFADE // enable dithering LOD transition if user select CrossFade transition in LOD group
                uint3 fadeMaskSeed = asuint((int3)(V * _ScreenSize.xyx)); // Quantize V to _ScreenSize values
                LODDitheringTransition(fadeMaskSeed, unity_LODFade.x);
        #endif
        
                float3 doubleSidedConstants = float3(1.0, 1.0, 1.0);
        
                ApplyDoubleSidedFlipOrMirror(fragInputs, doubleSidedConstants);
        
                SurfaceDescriptionInputs surfaceDescriptionInputs = FragInputsToSurfaceDescriptionInputs(fragInputs, V);
                SurfaceDescription surfaceDescription = SurfaceDescriptionFunction(surfaceDescriptionInputs);
        
                // Perform alpha test very early to save performance (a killed pixel will not sample textures)
                // TODO: split graph evaluation to grab just alpha dependencies first? tricky..
                
        
                float3 bentNormalWS;
                BuildSurfaceData(fragInputs, surfaceDescription, V, posInput, surfaceData, bentNormalWS);
        
                // Builtin Data
                // For back lighting we use the oposite vertex normal 
                InitBuiltinData(posInput, surfaceDescription.Alpha, bentNormalWS, -fragInputs.worldToTangent[2], fragInputs.texCoord1, fragInputs.texCoord2, builtinData);
        
                // override sampleBakedGI:
");
                if (graph.slots.Any(t => t.shaderOutputName == "Emission"))
                    shader.Append(@"
                builtinData.emissiveColor = surfaceDescription.Emission;
");
                shader.Append(@"
        #if (SHADERPASS == SHADERPASS_DISTORTION)
                builtinData.distortion = surfaceDescription.Distortion;
                builtinData.distortionBlur = surfaceDescription.DistortionBlur;
        #else
                builtinData.distortion = float2(0.0, 0.0);
                builtinData.distortionBlur = 0.0;
        #endif
        
                PostInitBuiltinData(V, posInput, surfaceData, builtinData);
            }
                #pragma fragment Frag

                #include ""Packages/com.unity.render-pipelines.high-definition/Runtime/RenderPipeline/ShaderPass/ShaderPassGBuffer.hlsl""

            ENDHLSL
        }
");

            }
            shader.AppendLine(@"
    }
}
");

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

            GraphUtil.GenerateSurfaceDescriptionStruct(pixelSlots, shaderGraph.slots.Where(t=> Graph.passInfos[pass].pixel.activeSlots.Contains(t.id)).ToList(), true, pixelGraphOutputStructName, null);

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
