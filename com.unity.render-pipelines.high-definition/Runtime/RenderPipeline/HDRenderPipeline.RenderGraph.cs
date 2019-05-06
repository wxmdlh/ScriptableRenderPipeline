using System;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering.RenderGraphModule;

namespace UnityEngine.Experimental.Rendering.HDPipeline
{
    public partial class HDRenderPipeline
    {
        void ExecuteWithRenderGraph(RenderRequest renderRequest, ScriptableRenderContext renderContext, CommandBuffer cmd, DensityVolumeList densityVolumes)
        {
            var hdCamera = renderRequest.hdCamera;
            var camera = hdCamera.camera;
            var cullingResults = renderRequest.cullingResults.cullingResults;
            //var target = renderRequest.target;

            CreateSharedResources(m_RenderGraph, hdCamera, m_CurrentDebugDisplaySettings);

#if UNITY_EDITOR
            var showGizmos = camera.cameraType == CameraType.Game
                || camera.cameraType == CameraType.SceneView;
#endif

            RenderGraphMutableResource colorBuffer = CreateColorBuffer(hdCamera, true);

            if (m_CurrentDebugDisplaySettings.IsDebugMaterialDisplayEnabled() || m_CurrentDebugDisplaySettings.IsMaterialValidationEnabled())
            {
                StartStereoRendering(m_RenderGraph, hdCamera.camera);
                RenderDebugViewMaterial(m_RenderGraph, cullingResults, hdCamera, colorBuffer);
                colorBuffer = ResolveMSAAColor(m_RenderGraph, hdCamera, colorBuffer);
                StopStereoRendering(m_RenderGraph, hdCamera.camera);
            }
            else
            {
                var prepassOutput = RenderPrepass(m_RenderGraph, cullingResults, hdCamera);

                //// Caution: We require sun light here as some skies use the sun light to render, it means that UpdateSkyEnvironment must be called after PrepareLightsForGPU.
                //// TODO: Try to arrange code so we can trigger this call earlier and use async compute here to run sky convolution during other passes (once we move convolution shader to compute).
                //UpdateSkyEnvironment(hdCamera, cmd);

                //RenderLighting();
            }

            ExecuteRenderGraph(m_RenderGraph, hdCamera, m_MSAASamples, renderContext, cmd);
        }

        static void ExecuteRenderGraph(RenderGraph renderGraph, HDCamera hdCamera, MSAASamples msaaSample, ScriptableRenderContext renderContext, CommandBuffer cmd)
        {
            var renderGraphParams = new RenderGraphExecuteParams()
            {
                renderingWidth = hdCamera.actualWidth,
                renderingHeight = hdCamera.actualHeight,
                msaaSamples = msaaSample
            };

            renderGraph.Execute(renderContext, cmd, renderGraphParams);
        }

        virtual protected RenderGraphMutableResource CreateColorBuffer(HDCamera hdCamera, bool allowMSAA)
        {
            bool msaa = hdCamera.frameSettings.IsEnabled(FrameSettingsField.MSAA) && allowMSAA;
            return m_RenderGraph.CreateTexture(
                new TextureDesc(Vector2.one)
                {
                    colorFormat = GetColorBufferFormat(),
                    enableRandomWrite = !msaa,
                    bindTextureMS = msaa,
                    enableMSAA = msaa,
                    xrInstancing = true,
                    useDynamicScale = true,
                    clearBuffer = NeedClearColorBuffer(hdCamera),
                    clearColor = GetColorBufferClearColor(hdCamera),
                    name = "CameraColor" });
        }

        class DebugViewMaterialData
        {
            public RenderGraphMutableResource   outputColor;
            public RenderGraphMutableResource   outputDepth;
            public RenderGraphResource          opaqueRendererList;
            public RenderGraphResource          transparentRendererList;
            public Material                     debugGBufferMaterial;
            public FrameSettings                frameSettings;
        }

        virtual protected void RenderDebugViewMaterial(RenderGraph renderGraph, CullingResults cull, HDCamera hdCamera, RenderGraphMutableResource output)
        {
            if (m_CurrentDebugDisplaySettings.data.materialDebugSettings.IsDebugGBufferEnabled() && hdCamera.frameSettings.litShaderMode == LitShaderMode.Deferred)
            {
                using (var builder = renderGraph.AddRenderPass<DebugViewMaterialData>("DebugViewMaterialGBuffer", out var passData, CustomSamplerId.DebugViewMaterialGBuffer.GetSampler()))
                {
                    passData.debugGBufferMaterial = m_currentDebugViewMaterialGBuffer;
                    passData.outputColor = builder.WriteTexture(output);

                    builder.SetRenderFunc(
                    (DebugViewMaterialData data, RenderGraphContext context) =>
                    {
                        var res = context.resources;
                        HDUtils.DrawFullScreen(context.cmd, data.debugGBufferMaterial, res.GetTexture(data.outputColor));
                    });
                }
            }
            else
            {
                using (var builder = renderGraph.AddRenderPass<DebugViewMaterialData>("DisplayDebug ViewMaterial", out var passData, CustomSamplerId.DisplayDebugViewMaterial.GetSampler()))
                {
                    passData.frameSettings = hdCamera.frameSettings;
                    passData.outputColor = builder.UseColorBuffer(output, 0);
                    passData.outputDepth = builder.UseDepthBuffer(GetDepthStencilBuffer(hdCamera.frameSettings.IsEnabled(FrameSettingsField.MSAA)));

                    // When rendering debug material we shouldn't rely on a depth prepass for optimizing the alpha clip test. As it is control on the material inspector side
                    // we must override the state here.
                    passData.opaqueRendererList = builder.UseRendererList(
                        builder.CreateRendererList(new RendererListDesc(m_AllForwardOpaquePassNames, cull, hdCamera.camera)
                        {
                            renderQueueRange = HDRenderQueue.k_RenderQueue_AllOpaque,
                            sortingCriteria = SortingCriteria.CommonOpaque,
                            rendererConfiguration = m_currentRendererConfigurationBakedLighting,
                            stateBlock = m_DepthStateOpaque
                        }));
                    passData.transparentRendererList= builder.UseRendererList(
                        builder.CreateRendererList(new RendererListDesc(m_AllTransparentPassNames, cull, hdCamera.camera)
                        {
                            renderQueueRange = HDRenderQueue.k_RenderQueue_AllOpaque,
                            sortingCriteria = SortingCriteria.CommonTransparent | SortingCriteria.RendererPriority,
                            rendererConfiguration = m_currentRendererConfigurationBakedLighting,
                            stateBlock = m_DepthStateOpaque
                        }));

                    builder.SetRenderFunc(
                    (DebugViewMaterialData data, RenderGraphContext context) =>
                    {
                        var res = context.resources;
                        DrawOpaqueRendererList(data.frameSettings, res.GetRendererList(data.opaqueRendererList), context);
                        DrawOpaqueRendererList(data.frameSettings, res.GetRendererList(data.transparentRendererList), context);
                    });
                }
            }
        }

        class ResolveColorData
        {
            public RenderGraphResource          input;
            public RenderGraphMutableResource   output;
            public Material                     resolveMaterial;
            public int                          passIndex;
        }

        virtual protected RenderGraphMutableResource ResolveMSAAColor(RenderGraph renderGraph, HDCamera hdCamera, RenderGraphMutableResource input)
        {
            if (hdCamera.frameSettings.IsEnabled(FrameSettingsField.MSAA))
            {
                using (var builder = renderGraph.AddRenderPass<ResolveColorData>("ResolveColor", out var passData))
                {
                    passData.input = builder.ReadTexture(input);
                    passData.output = builder.UseColorBuffer(CreateColorBuffer(hdCamera, false), 0);
                    passData.resolveMaterial = m_ColorResolveMaterial;
                    passData.passIndex = SampleCountToPassIndex(m_MSAASamples);

                    builder.SetRenderFunc(
                    (ResolveColorData data, RenderGraphContext context) =>
                    {
                        var res = context.resources;
                        var mpb = context.renderGraphPool.GetTempMaterialPropertyBlock();
                        mpb.SetTexture(HDShaderIDs._ColorTextureMS, res.GetTexture(data.input));
                        context.cmd.DrawProcedural(Matrix4x4.identity, data.resolveMaterial, data.passIndex, MeshTopology.Triangles, 3, 1, mpb);
                    });

                    return passData.output;
                }
            }
            else
            {
                return input;
            }
        }

        protected static void DrawRendererList(RendererList rendererList, RenderGraphContext context)
        {
            if (!rendererList.isValid)
                throw new ArgumentException("Invalid renderer list provided to DrawOpaqueRendererList");

            // This is done here because DrawRenderers API lives outside command buffers so we need to make call this before doing any DrawRenders
            context.renderContext.ExecuteCommandBuffer(context.cmd);
            context.cmd.Clear();

            if (rendererList.stateBlock == null)
                context.renderContext.DrawRenderers(rendererList.cullingResult, ref rendererList.drawSettings, ref rendererList.filteringSettings);
            else
            {
                var renderStateBlock = rendererList.stateBlock.Value;
                context.renderContext.DrawRenderers(rendererList.cullingResult, ref rendererList.drawSettings, ref rendererList.filteringSettings, ref renderStateBlock);
            }
        }

        protected static void DrawOpaqueRendererList(in FrameSettings frameSettings, RendererList rendererList, RenderGraphContext context)
        {
            if (!frameSettings.IsEnabled(FrameSettingsField.OpaqueObjects))
                return;

            DrawRendererList(rendererList, context);
        }

        protected static void DrawTransparentRendererList(in FrameSettings frameSettings, RendererList rendererList, RenderGraphContext context)
        {
            if (!frameSettings.IsEnabled(FrameSettingsField.TransparentObjects))
                return;

            DrawRendererList(rendererList, context);
        }

        protected static int SampleCountToPassIndex(MSAASamples samples)
        {
            switch (samples)
            {
                case MSAASamples.None:
                    return 0;
                case MSAASamples.MSAA2x:
                    return 1;
                case MSAASamples.MSAA4x:
                    return 2;
                case MSAASamples.MSAA8x:
                    return 3;
            };
            return 0;
        }

        protected bool NeedClearColorBuffer(HDCamera hdCamera)
        {
            if (hdCamera.clearColorMode == HDAdditionalCameraData.ClearColorMode.Color ||
                // If the luxmeter is enabled, the sky isn't rendered so we clear the background color
                m_CurrentDebugDisplaySettings.data.lightingDebugSettings.debugLightingMode == DebugLightingMode.LuxMeter ||
                // If we want the sky but the sky doesn't exist, still clear with background color
                (hdCamera.clearColorMode == HDAdditionalCameraData.ClearColorMode.Sky && !m_SkyManager.IsVisualSkyValid()) ||
                m_CurrentDebugDisplaySettings.IsDebugMaterialDisplayEnabled() || m_CurrentDebugDisplaySettings.IsMaterialValidationEnabled() ||
                // Special handling for Preview we force to clear with background color (i.e black)
                HDUtils.IsRegularPreviewCamera(hdCamera.camera)
                )
            {
                return true;
            }

            return false;
        }

        protected Color GetColorBufferClearColor(HDCamera hdCamera)
        {
            Color clearColor = hdCamera.backgroundColorHDR;
            // We set the background color to black when the luxmeter is enabled to avoid picking the sky color
            if (debugDisplaySettings.data.lightingDebugSettings.debugLightingMode == DebugLightingMode.LuxMeter)
                clearColor = Color.black;

            return clearColor;
        }

        // XR Specific
        protected class StereoRenderingPassData
        {
            public Camera camera;
        }

        protected void StartStereoRendering(RenderGraph renderGraph, Camera camera)
        {
            if (camera.stereoEnabled)
            {
                using (var builder = renderGraph.AddRenderPass<StereoRenderingPassData>("StartStereoRendering", out var passData))
                {
                    passData.camera = camera;
                    builder.SetRenderFunc(
                    (StereoRenderingPassData data, RenderGraphContext context) =>
                    {
                        // Reset scissor and viewport for C++ stereo code
                        context.cmd.DisableScissorRect();
                        context.cmd.SetViewport(data.camera.pixelRect);
                        context.renderContext.ExecuteCommandBuffer(context.cmd);
                        context.cmd.Clear();
                        context.renderContext.StartMultiEye(data.camera);
                    });
                }
            }
        }

        protected void StopStereoRendering(RenderGraph renderGraph, Camera camera)
        {
            if (camera.stereoEnabled)
            {
                using (var builder = renderGraph.AddRenderPass<StereoRenderingPassData>("StopStereoRendering", out var passData))
                {
                    passData.camera = camera;
                    builder.SetRenderFunc(
                    (StereoRenderingPassData data, RenderGraphContext context) =>
                    {
                        context.renderContext.ExecuteCommandBuffer(context.cmd);
                        context.cmd.Clear();
                        context.renderContext.StopMultiEye(data.camera);
                    });
                }
            }
        }
    }
}
