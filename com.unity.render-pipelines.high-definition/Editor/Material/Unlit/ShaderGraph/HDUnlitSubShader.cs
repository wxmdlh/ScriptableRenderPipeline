using System.Collections.Generic;
using UnityEditor.Graphing;
using UnityEditor.ShaderGraph;
using UnityEngine.Experimental.Rendering.HDPipeline;
using UnityEngine.Rendering;

namespace UnityEditor.Experimental.Rendering.HDPipeline
{
    class HDUnlitSubShader : IHDUnlitSubShader
    {
        Pass m_PassMETA = new Pass()
        {
            Name = "META",
            LightMode = "META",
            TemplateName = "HDUnlitPass.template",
            MaterialName = "Unlit",
            ShaderPassName = "SHADERPASS_LIGHT_TRANSPORT",
            Includes = new List<string>()
            {
                "#include \"Packages/com.unity.render-pipelines.high-definition/Runtime/RenderPipeline/ShaderPass/ShaderPassLightTransport.hlsl\"",
            },
            RequiredFields = new List<string>()
            {
                "AttributesMesh.normalOS",
                "AttributesMesh.tangentOS",     // Always present as we require it also in case of Variants lighting
                "AttributesMesh.uv0",
                "AttributesMesh.uv1",
                "AttributesMesh.color",
                "AttributesMesh.uv2",           // SHADERPASS_LIGHT_TRANSPORT always uses uv2
            },
            PixelShaderSlots = new List<int>()
            {
                HDUnlitMasterNode.ColorSlotId,
                HDUnlitMasterNode.AlphaSlotId,
                HDUnlitMasterNode.AlphaThresholdSlotId,
                HDUnlitMasterNode.EmissionSlotId
            },
            VertexShaderSlots = new List<int>()
            {
                //HDUnlitMasterNode.PositionSlotId
            },
            UseInPreview = false,
            OnGeneratePassImpl = (IMasterNode node, ref Pass pass) =>
            {
                HDSubShaderUtilities.SetCullModeOff(ref pass);
            }
        };

        Pass m_SceneSelectionPass = new Pass()
        {
            Name = "SceneSelectionPass",
            LightMode = "SceneSelectionPass",
            TemplateName = "HDUnlitPass.template",
            MaterialName = "Unlit",
            ShaderPassName = "SHADERPASS_DEPTH_ONLY",
            ExtraDefines = new List<string>()
            {
                "#define SCENESELECTIONPASS",
            },            
            Includes = new List<string>()
            {
                "#include \"Packages/com.unity.render-pipelines.high-definition/Runtime/RenderPipeline/ShaderPass/ShaderPassDepthOnly.hlsl\"",
            },
            PixelShaderSlots = new List<int>()
            {
                HDUnlitMasterNode.AlphaSlotId,
                HDUnlitMasterNode.AlphaThresholdSlotId
            },
            VertexShaderSlots = new List<int>()
            {
                HDUnlitMasterNode.PositionSlotId
            },
            UseInPreview = false,
            OnGeneratePassImpl = (IMasterNode node, ref Pass pass) =>
            {
                HDSubShaderUtilities.SetColorMaskOff(ref pass);
                HDSubShaderUtilities.SetCullMode(ref pass);
            }
        };

        Pass m_PassDepthForwardOnly = new Pass()
        {
            Name = "DepthForwardOnly",
            LightMode = "DepthForwardOnly",
            TemplateName = "HDUnlitPass.template",
            MaterialName = "Unlit",
            ShaderPassName = "SHADERPASS_DEPTH_ONLY",
            ZWriteOverride = "ZWrite On",

            ExtraDefines = new List<string>()
            {
                "#pragma multi_compile _ WRITE_MSAA_DEPTH"
                // Note we don't need to define WRITE_NORMAL_BUFFER
            },            

            Includes = new List<string>()
            {
                "#include \"Packages/com.unity.render-pipelines.high-definition/Runtime/RenderPipeline/ShaderPass/ShaderPassDepthOnly.hlsl\"",
            },
            PixelShaderSlots = new List<int>()
            {
                HDUnlitMasterNode.AlphaSlotId,
                HDUnlitMasterNode.AlphaThresholdSlotId
            },

            VertexShaderSlots = new List<int>()
            {
                HDUnlitMasterNode.PositionSlotId
            },
            UseInPreview = false,

            OnGeneratePassImpl = (IMasterNode node, ref Pass pass) =>
            {
                HDSubShaderUtilities.SetStencilStateForDepth(ref pass);
                HDSubShaderUtilities.SetCullMode(ref pass);
                
                // Caution: When using MSAA we have normal and depth buffer bind.
                // Mean unlit object need to not write in it (or write 0) - Disable color mask for this RT
                // This is not a problem in no MSAA mode as there is no buffer bind
                HDSubShaderUtilities.SetColorMaskOffTarget0(ref pass);
            }
        };

        Pass m_PassMotionVectors = new Pass()
        {
            Name = "MotionVectors",
            LightMode = "MotionVectors",
            TemplateName = "HDUnlitPass.template",
            MaterialName = "Unlit",
            ShaderPassName = "SHADERPASS_MOTION_VECTORS",

            ExtraDefines = new List<string>()
            {
                "#pragma multi_compile _ WRITE_MSAA_DEPTH"
                // Note we don't need to define WRITE_NORMAL_BUFFER
            },
            Includes = new List<string>()
            {
                "#include \"Packages/com.unity.render-pipelines.high-definition/Runtime/RenderPipeline/ShaderPass/ShaderPassMotionVectors.hlsl\"",
            },
            RequiredFields = new List<string>()
            {
                "FragInputs.positionRWS",
            },
            PixelShaderSlots = new List<int>()
            {
                HDUnlitMasterNode.AlphaSlotId,
                HDUnlitMasterNode.AlphaThresholdSlotId
            },
            VertexShaderSlots = new List<int>()
            {
                HDUnlitMasterNode.PositionSlotId
            },
            UseInPreview = false,

            OnGeneratePassImpl = (IMasterNode node, ref Pass pass) =>
            {
                HDSubShaderUtilities.SetStencilStateForMotionVector(ref pass);
                HDSubShaderUtilities.SetCullMode(ref pass);

                // Caution: When using MSAA we have motion vector, normal and depth buffer bind.
                // Mean unlit object need to not write in it (or write 0) - Disable color mask for this RT
                // This is not a problem in no MSAA mode as there is no buffer bind
                HDSubShaderUtilities.SetColorMaskOffTarget1(ref pass);
            }
        };

        Pass m_PassDistortion = new Pass()
        {
            Name = "DistortionVectors",
            LightMode = "DistortionVectors",
            TemplateName = "HDUnlitPass.template",
            MaterialName = "Unlit",
            ShaderPassName = "SHADERPASS_DISTORTION",
            ZWriteOverride = "ZWrite Off",
            Includes = new List<string>()
            {
                "#include \"Packages/com.unity.render-pipelines.high-definition/Runtime/RenderPipeline/ShaderPass/ShaderPassDistortion.hlsl\"",
            },
            PixelShaderSlots = new List<int>()
            {
                HDUnlitMasterNode.AlphaSlotId,
                HDUnlitMasterNode.AlphaThresholdSlotId,
                HDUnlitMasterNode.DistortionSlotId,
                HDUnlitMasterNode.DistortionBlurSlotId,
            },
            VertexShaderSlots = new List<int>()
            {
                HDUnlitMasterNode.PositionSlotId
            },
            UseInPreview = true,

            OnGeneratePassImpl = (IMasterNode node, ref Pass pass) =>
            {
                HDSubShaderUtilities.SetStencilStateForDistortionVector(ref pass);
                HDSubShaderUtilities.SetBlendModeForDistortionVector(ref pass);
                HDSubShaderUtilities.SetCullMode(ref pass);
            }
        };

        Pass m_PassForwardOnly = new Pass()
        {
            Name = "ForwardOnly",
            LightMode = "ForwardOnly",
            TemplateName = "HDUnlitPass.template",
            MaterialName = "Unlit",
            ShaderPassName = "SHADERPASS_FORWARD_UNLIT",
            ExtraDefines = new List<string>()
            {
                "#pragma multi_compile _ DEBUG_DISPLAY"
            },
            Includes = new List<string>()
            {
                "#include \"Packages/com.unity.render-pipelines.high-definition/Runtime/RenderPipeline/ShaderPass/ShaderPassForwardUnlit.hlsl\"",
            },
            PixelShaderSlots = new List<int>()
            {
                HDUnlitMasterNode.ColorSlotId,
                HDUnlitMasterNode.AlphaSlotId,
                HDUnlitMasterNode.AlphaThresholdSlotId,
                HDUnlitMasterNode.EmissionSlotId
            },
            VertexShaderSlots = new List<int>()
            {
                HDUnlitMasterNode.PositionSlotId
            },
            UseInPreview = true,

            OnGeneratePassImpl = (IMasterNode node, ref Pass pass) =>
            {
                HDSubShaderUtilities.SetStencilStateForForward(ref pass);
                HDSubShaderUtilities.SetCullMode(ref pass);
            }
        };

        public int GetPreviewPassIndex() { return 0; }

        private static HashSet<string> GetActiveFieldsFromMasterNode(AbstractMaterialNode iMasterNode, Pass pass)
        {
            HashSet<string> activeFields = new HashSet<string>();

            HDUnlitMasterNode masterNode = iMasterNode as HDUnlitMasterNode;
            if (masterNode == null)
            {
                return activeFields;
            }

            return activeFields;
        }

        private static bool GenerateShaderPassUnlit(HDUnlitMasterNode masterNode, Pass pass, GenerationMode mode, ShaderGenerator result, List<string> sourceAssetDependencyPaths)
        {
            if (mode == GenerationMode.ForReals || pass.UseInPreview)
            {
                pass.OnGeneratePass(masterNode);

                // apply master node options to active fields
                HashSet<string> activeFields = GetActiveFieldsFromMasterNode(masterNode, pass);

                // use standard shader pass generation
                bool vertexActive = masterNode.IsSlotConnected(HDUnlitMasterNode.PositionSlotId);
                return HDSubShaderUtilities.GenerateShaderPass(masterNode, pass, mode, activeFields, result, sourceAssetDependencyPaths, vertexActive);
            }
            else
            {
                return false;
            }
        }

        public string GetSubshader(IMasterNode iMasterNode, GenerationMode mode, List<string> sourceAssetDependencyPaths = null)
        {
            if (sourceAssetDependencyPaths != null)
            {
                // HDUnlitSubShader.cs
                sourceAssetDependencyPaths.Add(AssetDatabase.GUIDToAssetPath("1c44ec077faa54145a89357de68e5d26"));
                // HDSubShaderUtilities.cs
                sourceAssetDependencyPaths.Add(AssetDatabase.GUIDToAssetPath("713ced4e6eef4a44799a4dd59041484b"));
            }

            var masterNode = iMasterNode as HDUnlitMasterNode;

            var subShader = new ShaderGenerator();
            subShader.AddShaderChunk("SubShader", true);
            subShader.AddShaderChunk("{", true);
            subShader.Indent();
            {
                // Add tags at the SubShader level
                {
                    string tags = HDSubShaderUtilities.GetTags(HDRenderPipeline.k_ShaderTagName, HDRenderTypeTags.HDUnlitShader);
                    subShader.AddShaderChunk(tags, false);
                }
  
                GenerateShaderPassUnlit(masterNode, m_PassMETA, mode, subShader, sourceAssetDependencyPaths);
                GenerateShaderPassUnlit(masterNode, m_SceneSelectionPass, mode, subShader, sourceAssetDependencyPaths);

                GenerateShaderPassUnlit(masterNode, m_PassDepthForwardOnly, mode, subShader, sourceAssetDependencyPaths);
                GenerateShaderPassUnlit(masterNode, m_PassMotionVectors, mode, subShader, sourceAssetDependencyPaths);

                GenerateShaderPassUnlit(masterNode, m_PassDistortion, mode, subShader, sourceAssetDependencyPaths);

                GenerateShaderPassUnlit(masterNode, m_PassForwardOnly, mode, subShader, sourceAssetDependencyPaths);                                
            }
            subShader.Deindent();
            subShader.AddShaderChunk("}", true);

            subShader.AddShaderChunk(@"CustomEditor ""UnityEditor.Experimental.Rendering.HDPipeline.HDUnlitGUI""");

            return subShader.GetShaderString(0);
        }

        public bool IsPipelineCompatible(RenderPipelineAsset renderPipelineAsset)
        {
            return renderPipelineAsset is HDRenderPipelineAsset;
        }
    }
}
