using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering.RenderGraphModule;

namespace UnityEngine.Experimental.Rendering.HDPipeline
{
    using RTHandle = RTHandleSystem.RTHandle;

    public partial class HDRenderPipeline
    {
        protected enum MipLevel { Original, L1, L2, L3, L4, L5, L6, Count }

        protected RenderPipelineResources m_Resources;
        protected ScaleFunc[] m_ScaleFunctors;

        // MSAA-specific
        protected Material m_ResolveMaterial;

#if ENABLE_RAYTRACING
        public HDRaytracingManager m_RayTracingManager = new HDRaytracingManager();
        readonly HDRaytracingAmbientOcclusion m_RaytracingAmbientOcclusion = new HDRaytracingAmbientOcclusion();
#endif

        protected virtual void InitializeAmbientOcclusion(HDRenderPipelineAsset hdAsset)
        {
            if (!hdAsset.currentPlatformRenderPipelineSettings.supportSSAO)
                return;

            m_Resources = hdAsset.renderPipelineResources;

            if (hdAsset.currentPlatformRenderPipelineSettings.supportMSAA)
            {
                m_ResolveMaterial = CoreUtils.CreateEngineMaterial(hdAsset.renderPipelineResources.shaders.aoResolvePS);
            }

            // Prepare scale functors
            m_ScaleFunctors = new ScaleFunc[(int)MipLevel.Count];
            m_ScaleFunctors[0] = size => size; // 0 is original size (mip0)

            for (int i = 1; i < m_ScaleFunctors.Length; i++)
            {
                int mult = i;
                m_ScaleFunctors[i] = size =>
                {
                    int div = 1 << mult;
                    return new Vector2Int(
                        (size.x + (div - 1)) / div,
                        (size.y + (div - 1)) / div
                    );
                };
            }
        }

        protected virtual void CleanupAmbientOcclusion()
        {
#if ENABLE_RAYTRACING
            m_RaytracingAmbientOcclusion.Release();
#endif

            CoreUtils.Destroy(m_ResolveMaterial);
        }

#if ENABLE_RAYTRACING
        public void InitRaytracing(HDRaytracingManager raytracingManager, SharedRTManager sharedRTManager)
        {
            m_RayTracingManager = raytracingManager;
            m_RaytracingAmbientOcclusion.Init(m_Resources, m_Settings, m_RayTracingManager, sharedRTManager);
        }
#endif

        bool IsActive(HDCamera camera, AmbientOcclusion settings) => camera.frameSettings.IsEnabled(FrameSettingsField.SSAO) && settings.intensity.value > 0f;

        protected virtual RenderGraphResource RenderAmbientOcclusion(RenderGraph renderGraph, HDCamera camera, RenderGraphResource inputDepth, uint frameCount)
        {
            // Grab current settings
            var settings = VolumeManager.instance.stack.GetComponent<AmbientOcclusion>();

            //if (!IsActive(camera, settings))
            //    return renderGraph.ImportTexture(TextureXR.GetBlackTexture());

            RenderGraphResource result;
#if ENABLE_RAYTRACING
            HDRaytracingEnvironment rtEnvironement = m_RayTracingManager.CurrentEnvironment();
            if (rtEnvironement != null && rtEnvironement.raytracedAO)
                m_RaytracingAmbientOcclusion.RenderAO(camera, cmd, m_AmbientOcclusionTex, renderContext, frameCount);
            else
#endif
            {
                result = RenderAmbientOcclusion(renderGraph, camera, inputDepth, settings);
                result = ResolveAmbientOcclusion(renderGraph, camera, inputDepth, result);
            }

            return result;
        }

        protected class AOPassData
        {
            public Vector2 viewport;
            public bool msaaEnabled;
            public float tanHalfFoVHeight;
            public int cameraWidth;
            public int cameraHeight;
            public int computePassCount;
            public AmbientOcclusion settings;

            public ComputeShader downSample1CS;
            public ComputeShader downSample2CS;
            public ComputeShader renderCS;
            public ComputeShader upSampleCS;

            public RenderGraphResource inputDepth;
            public RenderGraphMutableResource output;
            public RenderGraphMutableResource linearDepthTex;
            public RenderGraphMutableResource lowDepthTex1;
            public RenderGraphMutableResource lowDepthTex2;
            public RenderGraphMutableResource lowDepthTex3;
            public RenderGraphMutableResource lowDepthTex4;
            public RenderGraphMutableResource tiledDepthTex1;
            public RenderGraphMutableResource tiledDepthTex2;
            public RenderGraphMutableResource tiledDepthTex3;
            public RenderGraphMutableResource tiledDepthTex4;
            public RenderGraphMutableResource occlusionTex1;
            public RenderGraphMutableResource occlusionTex2;
            public RenderGraphMutableResource occlusionTex3;
            public RenderGraphMutableResource occlusionTex4;
            public RenderGraphMutableResource combinedTex1;
            public RenderGraphMutableResource combinedTex2;
            public RenderGraphMutableResource combinedTex3;
        }

        protected virtual void CreateTransientResources(RenderGraphBuilder builder, AOPassData passData, bool supportMSAA)
        {
            RenderGraphMutableResource Alloc(TextureDimension dim, int slices, MipLevel size, GraphicsFormat format, bool uav, string name)
            {
                return builder.WriteTexture(builder.CreateTexture(new TextureDesc(m_ScaleFunctors[(int)size]) { dimension = dim, slices = slices, colorFormat = format, enableRandomWrite = uav, name = name, useDynamicScale = true, xrInstancing = true }));
            }

            var fmtFP16 = supportMSAA ? GraphicsFormat.R16G16_SFloat : GraphicsFormat.R16_SFloat;
            var fmtFP32 = supportMSAA ? GraphicsFormat.R32G32_SFloat : GraphicsFormat.R32_SFloat;
            var fmtFX8 = supportMSAA ? GraphicsFormat.R8G8_UNorm : GraphicsFormat.R8_UNorm;

            // All of these are pre-allocated to 1x1 and will be automatically scaled properly by
            // the internal RTHandle system
            passData.linearDepthTex = Alloc(TextureDimension.Tex2D, 1, MipLevel.Original, fmtFP16, true, "AOLinearDepth");

            passData.lowDepthTex1 = Alloc(TextureDimension.Tex2D, 1, MipLevel.L1, fmtFP32, true, "AOLowDepth1");
            passData.lowDepthTex2 = Alloc(TextureDimension.Tex2D, 1, MipLevel.L2, fmtFP32, true, "AOLowDepth2");
            passData.lowDepthTex3 = Alloc(TextureDimension.Tex2D, 1, MipLevel.L3, fmtFP32, true, "AOLowDepth3");
            passData.lowDepthTex4 = Alloc(TextureDimension.Tex2D, 1, MipLevel.L4, fmtFP32, true, "AOLowDepth4");

            passData.tiledDepthTex1 = Alloc(TextureDimension.Tex2DArray, 16, MipLevel.L3, fmtFP16, true, "AOTiledDepth1");
            passData.tiledDepthTex2 = Alloc(TextureDimension.Tex2DArray, 16, MipLevel.L4, fmtFP16, true, "AOTiledDepth2");
            passData.tiledDepthTex3 = Alloc(TextureDimension.Tex2DArray, 16, MipLevel.L5, fmtFP16, true, "AOTiledDepth3");
            passData.tiledDepthTex4 = Alloc(TextureDimension.Tex2DArray, 16, MipLevel.L6, fmtFP16, true, "AOTiledDepth4");

            passData.occlusionTex1 = Alloc(TextureDimension.Tex2D, 1, MipLevel.L1, fmtFX8, true, "AOOcclusion1");
            passData.occlusionTex2 = Alloc(TextureDimension.Tex2D, 1, MipLevel.L2, fmtFX8, true, "AOOcclusion2");
            passData.occlusionTex3 = Alloc(TextureDimension.Tex2D, 1, MipLevel.L3, fmtFX8, true, "AOOcclusion3");
            passData.occlusionTex4 = Alloc(TextureDimension.Tex2D, 1, MipLevel.L4, fmtFX8, true, "AOOcclusion4");

            passData.combinedTex1 = Alloc(TextureDimension.Tex2D, 1, MipLevel.L1, fmtFX8, true, "AOCombined1");
            passData.combinedTex2 = Alloc(TextureDimension.Tex2D, 1, MipLevel.L2, fmtFX8, true, "AOCombined2");
            passData.combinedTex3 = Alloc(TextureDimension.Tex2D, 1, MipLevel.L3, fmtFX8, true, "AOCombined3");
        }

        protected RenderGraphMutableResource CreateOutputAOTexture(RenderGraphBuilder builder, bool msaa)
        {
            GraphicsFormat format = msaa ? GraphicsFormat.R8G8_UNorm : GraphicsFormat.R8_UNorm;
            string name = msaa ? "Ambient Occlusion MSAA" : "Ambient Occlusion";
            return builder.CreateTexture(new TextureDesc(Vector2.one) { filterMode = FilterMode.Bilinear, colorFormat = format, enableRandomWrite = true, xrInstancing = true, useDynamicScale = true, name = name });
        }

        protected virtual unsafe RenderGraphResource RenderAmbientOcclusion(RenderGraph renderGraph, HDCamera camera, RenderGraphResource inputDepth, AmbientOcclusion settings)
        {
            using (var builder = renderGraph.AddRenderPass<AOPassData>("Render SSAO", out var renderPassData, CustomSamplerId.RenderSSAO.GetSampler()))
            {
                builder.EnableAsyncCompute(camera.frameSettings.SSAORunsAsync());

                renderPassData.settings = settings;
                renderPassData.cameraWidth = camera.actualWidth;
                renderPassData.cameraHeight = camera.actualHeight;
                renderPassData.viewport = RTHandles.rtHandleProperties.rtHandleScale;
                renderPassData.tanHalfFoVHeight = 1f / camera.mainViewConstants.projMatrix[0, 0];
                renderPassData.msaaEnabled = camera.frameSettings.IsEnabled(FrameSettingsField.MSAA);
                renderPassData.inputDepth = builder.ReadTexture(inputDepth);
                renderPassData.output = builder.WriteTexture(CreateOutputAOTexture(builder, renderPassData.msaaEnabled));
                renderPassData.downSample1CS = m_Resources.shaders.aoDownsample1CS;
                renderPassData.downSample2CS = m_Resources.shaders.aoDownsample2CS;
                renderPassData.renderCS = m_Resources.shaders.aoRenderCS;
                renderPassData.upSampleCS = m_Resources.shaders.aoUpsampleCS;
                renderPassData.computePassCount = camera.computePassCount;

                CreateTransientResources(builder, renderPassData, renderPassData.msaaEnabled);

                builder.SetRenderFunc(
                (AOPassData data, RenderGraphContext context) =>
                {
                    var cmd = context.cmd;
                    var resources = context.resources;
                    var renderGraphPool = context.renderGraphPool;

                    // Share alloc between all render commands
                    float[] sampleWeightTable = renderGraphPool.GetTempArray<float>(12);
                    float[] sampleThickness = renderGraphPool.GetTempArray<float>(12);
                    float[] invThicknessTable = renderGraphPool.GetTempArray<float>(12);

                    int* widths = stackalloc int[7];
                    int* heights = stackalloc int[7];

                    InitializeSizeArrays(widths, heights, data.cameraWidth, data.cameraHeight);

                    Vector2 GetSize(MipLevel mip) => new Vector2(widths[(int)mip], heights[(int)mip]);
                    Vector3 GetSizeArray(MipLevel mip) => new Vector3(widths[(int)mip], heights[(int)mip], 16);

                    // Render logic
                    PushDownsampleCommands(cmd, data, resources, widths, heights);

                    PushRenderCommands(cmd, data, resources.GetTexture(data.tiledDepthTex1), resources.GetTexture(data.occlusionTex1), GetSizeArray(MipLevel.L3), sampleWeightTable, sampleThickness, invThicknessTable);
                    PushRenderCommands(cmd, data, resources.GetTexture(data.tiledDepthTex2), resources.GetTexture(data.occlusionTex2), GetSizeArray(MipLevel.L4), sampleWeightTable, sampleThickness, invThicknessTable);
                    PushRenderCommands(cmd, data, resources.GetTexture(data.tiledDepthTex3), resources.GetTexture(data.occlusionTex3), GetSizeArray(MipLevel.L5), sampleWeightTable, sampleThickness, invThicknessTable);
                    PushRenderCommands(cmd, data, resources.GetTexture(data.tiledDepthTex4), resources.GetTexture(data.occlusionTex4), GetSizeArray(MipLevel.L6), sampleWeightTable, sampleThickness, invThicknessTable);

                    PushUpsampleCommands(cmd, data, resources.GetTexture(data.lowDepthTex4), resources.GetTexture(data.occlusionTex4), resources.GetTexture(data.lowDepthTex3), resources.GetTexture(data.occlusionTex3), resources.GetTexture(data.combinedTex3), GetSize(MipLevel.L4), GetSize(MipLevel.L3));
                    PushUpsampleCommands(cmd, data, resources.GetTexture(data.lowDepthTex3), resources.GetTexture(data.combinedTex3), resources.GetTexture(data.lowDepthTex2), resources.GetTexture(data.occlusionTex2), resources.GetTexture(data.combinedTex2), GetSize(MipLevel.L3), GetSize(MipLevel.L2));
                    PushUpsampleCommands(cmd, data, resources.GetTexture(data.lowDepthTex2), resources.GetTexture(data.combinedTex2), resources.GetTexture(data.lowDepthTex1), resources.GetTexture(data.occlusionTex1), resources.GetTexture(data.combinedTex1), GetSize(MipLevel.L2), GetSize(MipLevel.L1));
                    PushUpsampleCommands(cmd, data, resources.GetTexture(data.lowDepthTex1), resources.GetTexture(data.combinedTex1), resources.GetTexture(data.linearDepthTex), null, resources.GetTexture(data.output), GetSize(MipLevel.L1), GetSize(MipLevel.Original));
                });

                return renderPassData.output;
            }
        }

        static unsafe void InitializeSizeArrays(int* widths, int* heights, int inputWidth, int inputHeight)
        {
            // Base size
            widths[0] = inputWidth;
            heights[0] = inputHeight;

            // L1 -> L6 sizes
            // We need to recalculate these on every frame, we can't rely on RTHandle width/height
            // values as they may have been rescaled and not the actual size we want
            for (int i = 1; i < (int)MipLevel.Count; i++)
            {
                int div = 1 << i;
                widths[i] = (widths[0] + (div - 1)) / div;
                heights[i] = (heights[0] + (div - 1)) / div;
            }
        }

        static void InitializeSamplingData(float[] sampleWeightTable, float[] sampleThickness, float[] invThicknessTable, in Vector3 sourceSize, float tanHalfFoVHeight)
        {
            sampleThickness[0] = Mathf.Sqrt(1f - 0.2f * 0.2f);
            sampleThickness[1] = Mathf.Sqrt(1f - 0.4f * 0.4f);
            sampleThickness[2] = Mathf.Sqrt(1f - 0.6f * 0.6f);
            sampleThickness[3] = Mathf.Sqrt(1f - 0.8f * 0.8f);
            sampleThickness[4] = Mathf.Sqrt(1f - 0.2f * 0.2f - 0.2f * 0.2f);
            sampleThickness[5] = Mathf.Sqrt(1f - 0.2f * 0.2f - 0.4f * 0.4f);
            sampleThickness[6] = Mathf.Sqrt(1f - 0.2f * 0.2f - 0.6f * 0.6f);
            sampleThickness[7] = Mathf.Sqrt(1f - 0.2f * 0.2f - 0.8f * 0.8f);
            sampleThickness[8] = Mathf.Sqrt(1f - 0.4f * 0.4f - 0.4f * 0.4f);
            sampleThickness[9] = Mathf.Sqrt(1f - 0.4f * 0.4f - 0.6f * 0.6f);
            sampleThickness[10] = Mathf.Sqrt(1f - 0.4f * 0.4f - 0.8f * 0.8f);
            sampleThickness[11] = Mathf.Sqrt(1f - 0.6f * 0.6f - 0.6f * 0.6f);

            // Here we compute multipliers that convert the center depth value into (the reciprocal
            // of) sphere thicknesses at each sample location. This assumes a maximum sample radius
            // of 5 units, but since a sphere has no thickness at its extent, we don't need to
            // sample that far out. Only samples whole integer offsets with distance less than 25
            // are used. This means that there is no sample at (3, 4) because its distance is
            // exactly 25 (and has a thickness of 0.)

            // The shaders are set up to sample a circular region within a 5-pixel radius.
            const float kScreenspaceDiameter = 10f;

            // SphereDiameter = CenterDepth * ThicknessMultiplier. This will compute the thickness
            // of a sphere centered at a specific depth. The ellipsoid scale can stretch a sphere
            // into an ellipsoid, which changes the characteristics of the AO.
            // TanHalfFovH: Radius of sphere in depth units if its center lies at Z = 1
            // ScreenspaceDiameter: Diameter of sample sphere in pixel units
            // ScreenspaceDiameter / BufferWidth: Ratio of the screen width that the sphere actually covers
            float thicknessMultiplier = 2f * tanHalfFoVHeight * kScreenspaceDiameter / sourceSize.x;

            // This will transform a depth value from [0, thickness] to [0, 1].
            float inverseRangeFactor = 1f / thicknessMultiplier;

            // The thicknesses are smaller for all off-center samples of the sphere. Compute
            // thicknesses relative to the center sample.
            for (int i = 0; i < 12; i++)
                invThicknessTable[i] = inverseRangeFactor / sampleThickness[i];

            // These are the weights that are multiplied against the samples because not all samples
            // are equally important. The farther the sample is from the center location, the less
            // they matter. We use the thickness of the sphere to determine the weight.  The scalars
            // in front are the number of samples with this weight because we sum the samples
            // together before multiplying by the weight, so as an aggregate all of those samples
            // matter more. After generating this table, the weights are normalized.
            sampleWeightTable[0] = 4 * sampleThickness[0];    // Axial
            sampleWeightTable[1] = 4 * sampleThickness[1];    // Axial
            sampleWeightTable[2] = 4 * sampleThickness[2];    // Axial
            sampleWeightTable[3] = 4 * sampleThickness[3];    // Axial
            sampleWeightTable[4] = 4 * sampleThickness[4];    // Diagonal
            sampleWeightTable[5] = 8 * sampleThickness[5];    // L-shaped
            sampleWeightTable[6] = 8 * sampleThickness[6];    // L-shaped
            sampleWeightTable[7] = 8 * sampleThickness[7];    // L-shaped
            sampleWeightTable[8] = 4 * sampleThickness[8];    // Diagonal
            sampleWeightTable[9] = 8 * sampleThickness[9];    // L-shaped
            sampleWeightTable[10] = 8 * sampleThickness[10];    // L-shaped
            sampleWeightTable[11] = 4 * sampleThickness[11];    // Diagonal

            // Zero out the unused samples.
            // FIXME: should we support SAMPLE_EXHAUSTIVELY mode?
            sampleWeightTable[0] = 0;
            sampleWeightTable[2] = 0;
            sampleWeightTable[5] = 0;
            sampleWeightTable[7] = 0;
            sampleWeightTable[9] = 0;

            // Normalize the weights by dividing by the sum of all weights
            float totalWeight = 0f;

            foreach (float w in sampleWeightTable)
                totalWeight += w;

            for (int i = 0; i < sampleWeightTable.Length; i++)
                sampleWeightTable[i] /= totalWeight;
        }

        protected static unsafe void PushDownsampleCommands(CommandBuffer cmd, AOPassData data, RenderGraphResourceRegistry resources, int* widths, int* heights)
        {
            var kernelName = data.msaaEnabled ? "KMain_MSAA" : "KMain";

            // 1st downsampling pass.
            var cs = data.downSample1CS;
            int kernel = cs.FindKernel(kernelName);

            cmd.SetComputeTextureParam(cs, kernel, HDShaderIDs._LinearZ, resources.GetTexture(data.linearDepthTex));
            cmd.SetComputeTextureParam(cs, kernel, HDShaderIDs._DS2x, resources.GetTexture(data.lowDepthTex1));
            cmd.SetComputeTextureParam(cs, kernel, HDShaderIDs._DS4x, resources.GetTexture(data.lowDepthTex2));
            cmd.SetComputeTextureParam(cs, kernel, HDShaderIDs._DS2xAtlas, resources.GetTexture(data.tiledDepthTex1));
            cmd.SetComputeTextureParam(cs, kernel, HDShaderIDs._DS4xAtlas, resources.GetTexture(data.tiledDepthTex2));
            cmd.SetComputeTextureParam(cs, kernel, HDShaderIDs._Depth, resources.GetTexture(data.inputDepth), 0);

            cmd.DispatchCompute(cs, kernel, widths[(int)MipLevel.L4], heights[(int)MipLevel.L4], data.computePassCount);

            // 2nd downsampling pass.
            cs = data.downSample2CS;
            kernel = cs.FindKernel(kernelName);

            cmd.SetComputeTextureParam(cs, kernel, HDShaderIDs._DS4x, resources.GetTexture(data.lowDepthTex2));
            cmd.SetComputeTextureParam(cs, kernel, HDShaderIDs._DS8x, resources.GetTexture(data.lowDepthTex3));
            cmd.SetComputeTextureParam(cs, kernel, HDShaderIDs._DS16x, resources.GetTexture(data.lowDepthTex4));
            cmd.SetComputeTextureParam(cs, kernel, HDShaderIDs._DS8xAtlas, resources.GetTexture(data.tiledDepthTex3));
            cmd.SetComputeTextureParam(cs, kernel, HDShaderIDs._DS16xAtlas, resources.GetTexture(data.tiledDepthTex4));

            cmd.DispatchCompute(cs, kernel, widths[(int)MipLevel.L6], heights[(int)MipLevel.L6], data.computePassCount);
        }

        protected static void PushRenderCommands(CommandBuffer cmd, AOPassData data, RTHandle source, RTHandle destination, in Vector3 sourceSize, float[] sampleWeightTable, float[] sampleThickness, float[] invThicknessTable)
        {
            InitializeSamplingData(sampleWeightTable, sampleThickness, invThicknessTable, sourceSize, data.tanHalfFoVHeight);

            // Set the arguments for the render kernel.
            var cs = data.renderCS;
            int kernel = cs.FindKernel(data.msaaEnabled ? "KMainInterleaved_MSAA" : "KMainInterleaved");

            cmd.SetComputeFloatParams(cs, HDShaderIDs._InvThicknessTable, invThicknessTable);
            cmd.SetComputeFloatParams(cs, HDShaderIDs._SampleWeightTable, sampleWeightTable);
            cmd.SetComputeVectorParam(cs, HDShaderIDs._InvSliceDimension, new Vector2(1f / sourceSize.x * data.viewport.x, 1f / sourceSize.y * data.viewport.y));
            cmd.SetComputeVectorParam(cs, HDShaderIDs._AdditionalParams, new Vector2(-1f / data.settings.thicknessModifier.value, data.settings.intensity.value));
            cmd.SetComputeTextureParam(cs, kernel, HDShaderIDs._Depth, source);
            cmd.SetComputeTextureParam(cs, kernel, HDShaderIDs._Occlusion, destination);

            // Calculate the thread group count and add a dispatch command with them.
            cs.GetKernelThreadGroupSizes(kernel, out var xsize, out var ysize, out var zsize);

            cmd.DispatchCompute(
                cs, kernel,
                ((int)sourceSize.x + (int)xsize - 1) / (int)xsize,
                ((int)sourceSize.y + (int)ysize - 1) / (int)ysize,
                data.computePassCount * ((int)sourceSize.z + (int)zsize - 1) / (int)zsize
            );
        }

        protected static void PushUpsampleCommands(CommandBuffer cmd, AOPassData passData, RTHandle lowResDepth, RTHandle interleavedAO, RTHandle highResDepth, RTHandle highResAO, RTHandle dest, in Vector3 lowResDepthSize, in Vector2 highResDepthSize)
        {
            var cs = passData.upSampleCS;
            int kernel = passData.msaaEnabled
                ? cs.FindKernel(highResAO == null ? "KMainInvert_MSAA" : "KMainBlendout_MSAA")
                : cs.FindKernel(highResAO == null ? "KMainInvert" : "KMainBlendout");

            float stepSize = 1920f / lowResDepthSize.x;
            float bTolerance = 1f - Mathf.Pow(10f, passData.settings.blurTolerance.value) * stepSize;
            bTolerance *= bTolerance;
            float uTolerance = Mathf.Pow(10f, passData.settings.upsampleTolerance.value);
            float noiseFilterWeight = 1f / (Mathf.Pow(10f, passData.settings.noiseFilterTolerance.value) + uTolerance);

            cmd.SetComputeVectorParam(cs, HDShaderIDs._InvLowResolution, new Vector2(1f / lowResDepthSize.x * passData.viewport.x, 1f / lowResDepthSize.y * passData.viewport.y));
            cmd.SetComputeVectorParam(cs, HDShaderIDs._InvHighResolution, new Vector2(1f / highResDepthSize.x * passData.viewport.x, 1f / highResDepthSize.y * passData.viewport.y));
            cmd.SetComputeVectorParam(cs, HDShaderIDs._AdditionalParams, new Vector4(noiseFilterWeight, stepSize, bTolerance, uTolerance));

            cmd.SetComputeTextureParam(cs, kernel, HDShaderIDs._LoResDB, lowResDepth);
            cmd.SetComputeTextureParam(cs, kernel, HDShaderIDs._HiResDB, highResDepth);
            cmd.SetComputeTextureParam(cs, kernel, HDShaderIDs._LoResAO1, interleavedAO);

            if (highResAO != null)
                cmd.SetComputeTextureParam(cs, kernel, HDShaderIDs._HiResAO, highResAO);

            cmd.SetComputeTextureParam(cs, kernel, HDShaderIDs._AoResult, dest);

            int xcount = ((int)highResDepthSize.x + 17) / 16;
            int ycount = ((int)highResDepthSize.y + 17) / 16;
            cmd.DispatchCompute(cs, kernel, xcount, ycount, passData.computePassCount);
        }

        protected class AOPostPassData
        {
            public bool enableMSAA;
            public bool isActive; // Temporary
            public RenderGraphResource inputDepth;
            public RenderGraphResource inputAO;
            public RenderGraphMutableResource output;
            public RenderGraphResource finalOutput; // Temporary
            public AmbientOcclusion settings;
            public Material resolveMaterial;
        }

        protected virtual RenderGraphResource ResolveAmbientOcclusion(RenderGraph renderGraph, HDCamera camera, RenderGraphResource inputDepth, RenderGraphResource inputAO)
        {
            using (var builder = renderGraph.AddRenderPass<AOPostPassData>("MSAA Resolve AO Buffer", out var renderPassData, CustomSamplerId.ResolveSSAO.GetSampler()))
            {
                var settings = VolumeManager.instance.stack.GetComponent<AmbientOcclusion>();
                renderPassData.enableMSAA = camera.frameSettings.IsEnabled(FrameSettingsField.MSAA);
                renderPassData.isActive = IsActive(camera, settings);
                renderPassData.settings = settings;
                renderPassData.resolveMaterial = m_ResolveMaterial;
                if (renderPassData.enableMSAA)
                {
                    renderPassData.inputDepth = builder.ReadTexture(inputDepth);
                    renderPassData.inputAO = builder.ReadTexture(inputAO);
                    renderPassData.output = builder.WriteTexture(CreateOutputAOTexture(builder, false));
                }
                else
                {
                    renderPassData.finalOutput = builder.ReadTexture(inputAO); // TEMP: Should be set by pass reading it (lighting etc)
                }

                builder.SetRenderFunc(
                (AOPostPassData data, RenderGraphContext renderGraphContext) =>
                {
                    var cmd = renderGraphContext.cmd;
                    var renderGraphPool = renderGraphContext.renderGraphPool;
                    var resources = renderGraphContext.resources;

                    // MSAA Resolve
                    if (data.enableMSAA)
                    {
                        MaterialPropertyBlock mpb = renderGraphPool.GetTempMaterialPropertyBlock();
                        HDUtils.SetRenderTarget(cmd, resources.GetTexture(data.output));
                        mpb.SetTexture(HDShaderIDs._DepthValuesTexture, resources.GetTexture(data.inputDepth));
                        mpb.SetTexture(HDShaderIDs._MultiAmbientOcclusionTexture, resources.GetTexture(data.inputAO));
                        cmd.DrawProcedural(Matrix4x4.identity, data.resolveMaterial, 0, MeshTopology.Triangles, 3, 1, mpb);
                    }

                    // TODO: This pass should only do MSAA resolve.
                    // Change this to "move resource" a black texture to AO output outside this function and implement SetGlobalTexture on TextureRead
                    if (!data.isActive)
                    {
                        // No AO applied - neutral is black, see the comment in the shaders
                        cmd.SetGlobalTexture(HDShaderIDs._AmbientOcclusionTexture, TextureXR.GetBlackTexture());
                        cmd.SetGlobalVector(HDShaderIDs._AmbientOcclusionParam, Vector4.zero);
                    }
                    else
                    {
                        cmd.SetGlobalTexture(HDShaderIDs._AmbientOcclusionTexture, resources.GetTexture(data.enableMSAA ? data.output : data.finalOutput));
                        cmd.SetGlobalVector(HDShaderIDs._AmbientOcclusionParam, new Vector4(0f, 0f, 0f, data.settings.directLightingStrength.value));
                    }
                });

                return renderPassData.output;
            }

            // TODO: All the pushdebug stuff should be centralized somewhere
            //(RenderPipelineManager.currentPipeline as HDRenderPipeline).PushFullScreenDebugTexture(camera, cmd, m_AmbientOcclusionTex, FullScreenDebugMode.SSAO);
        }
    }
}
