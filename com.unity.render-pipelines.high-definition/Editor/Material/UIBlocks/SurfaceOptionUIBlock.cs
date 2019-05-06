using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering.HDPipeline;

// Include material common properties names
using static UnityEngine.Experimental.Rendering.HDPipeline.HDMaterialProperties;

namespace UnityEditor.Experimental.Rendering.HDPipeline
{
    public class SurfaceOptionUIBlock : MaterialUIBlock
    {
        [Flags]
        public enum Features
        {
            Surface                     = 1 << 0,
            BlendMode                   = 1 << 1,
            DoubleSided                 = 1 << 2,
            AlphaCutoff                 = 1 << 3,
            AlphaCutoffThreshold        = 1 << 4,
            AlphaCutoffShadowThreshold  = 1 << 5,
            Unlit                       = Surface | BlendMode | DoubleSided | AlphaCutoff | AlphaCutoffShadowThreshold | AlphaCutoffThreshold,
            Lit                         = All,
            All                         = ~0,
        }

        static class Styles
        {
            public const string optionText = "Surface Options";
            public const string surfaceTypeText = "Surface Type";
            public const string renderingPassText = "Rendering Pass";
            public const string blendModeText = "Blending Mode";
            public const string notSupportedInMultiEdition = "Multiple Different Values";

            public static readonly string[] surfaceTypeNames = Enum.GetNames(typeof(SurfaceType));
            public static readonly string[] blendModeNames = Enum.GetNames(typeof(BlendMode));
            public static readonly int[] blendModeValues = Enum.GetValues(typeof(BlendMode)) as int[];

            public static GUIContent transparentPrepassText = new GUIContent("Appear in Refraction", "When enabled, HDRP handles objects with this Material before the refraction pass.");

            public static GUIContent doubleSidedEnableText = new GUIContent("Double-Sided", "When enabled, HDRP renders both faces of the polygons that make up meshes using this Material. Disables backface culling.");

            public static GUIContent useShadowThresholdText = new GUIContent("Use Shadow Threshold", "Enable separate threshold for shadow pass");
            public static GUIContent alphaCutoffEnableText = new GUIContent("Alpha Clipping", "When enabled, HDRP processes Alpha Clipping for this Material.");
            public static GUIContent alphaCutoffText = new GUIContent("Threshold", "Controls the threshold for the Alpha Clipping effect.");
            public static GUIContent alphaCutoffShadowText = new GUIContent("Shadow Threshold", "Controls the threshold for shadow pass alpha clipping.");
            public static GUIContent alphaCutoffPrepassText = new GUIContent("Prepass Threshold", "Controls the threshold for transparent depth prepass alpha clipping.");
            public static GUIContent alphaCutoffPostpassText = new GUIContent("Postpass Threshold", "Controls the threshold for transparent depth postpass alpha clipping.");
            public static GUIContent transparentDepthPostpassEnableText = new GUIContent("Transparent Depth Postpass", "When enabled, HDRP renders a depth postpass for transparent objects. This improves post-processing effects like depth of field.");
            public static GUIContent transparentDepthPrepassEnableText = new GUIContent("Transparent Depth Prepass", "When enabled, HDRP renders a depth prepass for transparent GameObjects. This improves sorting.");
            public static GUIContent transparentBackfaceEnableText = new GUIContent("Back Then Front Rendering", "When enabled, HDRP renders the back face and then the front face, in two separate draw calls, to better sort transparent meshes.");
            public static GUIContent transparentWritingMotionVecText = new GUIContent("Transparent Writes Motion Vectors", "When enabled, transparent objects write motion vectors, these replace what was previously rendered in the buffer.");

            public static GUIContent transparentSortPriorityText = new GUIContent("Sorting Priority", "Sets the sort priority (from -100 to 100) of transparent meshes using this Material. HDRP uses this value to calculate the sorting order of all transparent meshes on screen.");
            public static GUIContent enableTransparentFogText = new GUIContent("Receive fog", "When enabled, this Material can receive fog.");
            public static GUIContent enableBlendModePreserveSpecularLightingText = new GUIContent("Preserve specular lighting", "When enabled, blending only affects diffuse lighting, allowing for correct specular lighting on transparent meshes that use this Material.");

            // Lit properties
            public static GUIContent doubleSidedNormalModeText = new GUIContent("Normal Mode", "Specifies the method HDRP uses to modify the normal base.\nMirror: Mirrors the normals with the vertex normal plane.\nFlip: Flips the normal.");
            public static GUIContent depthOffsetEnableText = new GUIContent("Depth Offset", "When enabled, HDRP uses the Height Map to calculate the depth offset for this Material.");

            // Displacement mapping (POM, tessellation, per vertex)
            //public static GUIContent enablePerPixelDisplacementText = new GUIContent("Per Pixel Displacement", "");

            public static GUIContent displacementModeText = new GUIContent("Displacement Mode", "Specifies the method HDRP uses to apply height map displacement to the selected element: Vertex, pixel, or tessellated vertex.\n You must use flat surfaces for Pixel displacement.");
            public static GUIContent lockWithObjectScaleText = new GUIContent("Lock With Object Scale", "When enabled, displacement mapping takes the absolute value of the scale of the object into account.");
            public static GUIContent lockWithTilingRateText = new GUIContent("Lock With Height Map Tiling Rate", "When enabled, displacement mapping takes the absolute value of the tiling rate of the height map into account.");

            // Material ID
            public static GUIContent materialIDText = new GUIContent("Material Type", "Specifies additional feature for this Material. Customize you Material with different settings depending on which Material Type you select.");
            public static GUIContent transmissionEnableText = new GUIContent("Transmission", "When enabled HDRP processes the transmission effect for subsurface scattering. Simulates the translucency of the object.");

            // Per pixel displacement
            public static GUIContent ppdMinSamplesText = new GUIContent("Minimum Steps", "Controls the minimum number of steps HDRP uses for per pixel displacement mapping.");
            public static GUIContent ppdMaxSamplesText = new GUIContent("Maximum Steps", "Controls the maximum number of steps HDRP uses for per pixel displacement mapping.");
            public static GUIContent ppdLodThresholdText = new GUIContent("Fading Mip Level Start", "Controls the Height Map mip level where the parallax occlusion mapping effect begins to disappear.");
            public static GUIContent ppdPrimitiveLength = new GUIContent("Primitive Length", "Sets the length of the primitive (with the scale of 1) to which HDRP applies per-pixel displacement mapping. For example, the standard quad is 1 x 1 meter, while the standard plane is 10 x 10 meters.");
            public static GUIContent ppdPrimitiveWidth = new GUIContent("Primitive Width", "Sets the width of the primitive (with the scale of 1) to which HDRP applies per-pixel displacement mapping. For example, the standard quad is 1 x 1 meter, while the standard plane is 10 x 10 meters.");

            public static GUIContent supportDecalsText = new GUIContent("Receive Decals", "Enable to allow Materials to receive decals.");

            public static GUIContent enableGeometricSpecularAAText = new GUIContent("Geometric Specular AA", "When enabled, HDRP reduces specular aliasing on high density meshes (particularly useful when the not using a normal map).");
            public static GUIContent specularAAScreenSpaceVarianceText = new GUIContent("Screen space variance", "Controls the strength of the Specular AA reduction. Higher values give a more blurry result and less aliasing.");
            public static GUIContent specularAAThresholdText = new GUIContent("Threshold", "Controls the effect of Specular AA reduction. A values of 0 does not apply reduction, higher values allow higher reduction.");
            
            // SSR
            public static GUIContent receivesSSRText = new GUIContent("Receive SSR", "When enabled, this Material can receive screen space reflections.");
        }
   
        // Properties common to Unlit and Lit
        MaterialProperty surfaceType = null;
        
        MaterialProperty alphaCutoffEnable = null;
        const string kAlphaCutoffEnabled = "_AlphaCutoffEnable";
        MaterialProperty useShadowThreshold = null;
        const string kUseShadowThreshold = "_UseShadowThreshold";
        MaterialProperty alphaCutoff = null;
        const string kAlphaCutoff = "_AlphaCutoff";
        MaterialProperty alphaCutoffShadow = null;
        const string kAlphaCutoffShadow = "_AlphaCutoffShadow";
        MaterialProperty alphaCutoffPrepass = null;
        const string kAlphaCutoffPrepass = "_AlphaCutoffPrepass";
        MaterialProperty alphaCutoffPostpass = null;
        const string kAlphaCutoffPostpass = "_AlphaCutoffPostpass";
        MaterialProperty transparentDepthPrepassEnable = null;
        const string kTransparentDepthPrepassEnable = "_TransparentDepthPrepassEnable";
        MaterialProperty transparentDepthPostpassEnable = null;
        const string kTransparentDepthPostpassEnable = "_TransparentDepthPostpassEnable";
        MaterialProperty transparentBackfaceEnable = null;
        const string kTransparentBackfaceEnable = "_TransparentBackfaceEnable";
        MaterialProperty transparentSortPriority = null;
        const string kTransparentSortPriority = "_TransparentSortPriority";
        MaterialProperty transparentWritingMotionVec = null;
        const string kTransparentWritingMotionVec = "_TransparentWritingMotionVec";
        MaterialProperty doubleSidedEnable = null;
        const string kDoubleSidedEnable = "_DoubleSidedEnable";
        MaterialProperty blendMode = null;
        const string kBlendMode = "_BlendMode";
        MaterialProperty enableBlendModePreserveSpecularLighting = null;
        const string kEnableBlendModePreserveSpecularLighting = "_EnableBlendModePreserveSpecularLighting";
        MaterialProperty enableFogOnTransparent = null;
        const string kEnableFogOnTransparent = "_EnableFogOnTransparent";

        // Lit properties
        MaterialProperty doubleSidedNormalMode = null;
        const string kDoubleSidedNormalMode = "_DoubleSidedNormalMode";
        MaterialProperty materialID  = null;
        const string kMaterialID = "_MaterialID";
        MaterialProperty supportDecals = null;
        const string kSupportDecals = "_SupportDecals";
        MaterialProperty enableGeometricSpecularAA = null;
        const string kEnableGeometricSpecularAA = "_EnableGeometricSpecularAA";
        MaterialProperty specularAAScreenSpaceVariance = null;
        const string kSpecularAAScreenSpaceVariance = "_SpecularAAScreenSpaceVariance";
        MaterialProperty specularAAThreshold = null;
        const string kSpecularAAThreshold = "_SpecularAAThreshold";
        MaterialProperty transmissionEnable = null;
        const string kTransmissionEnable = "_TransmissionEnable";

        // Per pixel displacement params
        MaterialProperty ppdMinSamples = null;
        const string kPpdMinSamples = "_PPDMinSamples";
        MaterialProperty ppdMaxSamples = null;
        const string kPpdMaxSamples = "_PPDMaxSamples";
        MaterialProperty ppdLodThreshold = null;
        const string kPpdLodThreshold = "_PPDLodThreshold";
        MaterialProperty ppdPrimitiveLength = null;
        const string kPpdPrimitiveLength = "_PPDPrimitiveLength";
        MaterialProperty ppdPrimitiveWidth = null;
        const string kPpdPrimitiveWidth = "_PPDPrimitiveWidth";
        MaterialProperty invPrimScale = null;
        const string kInvPrimScale = "_InvPrimScale";

        // SSR
        protected MaterialProperty receivesSSR = null;
        protected const string kReceivesSSR = "_ReceivesSSR";

        protected MaterialProperty displacementMode = null;
        protected const string kDisplacementMode = "_DisplacementMode";
        protected MaterialProperty displacementLockObjectScale = null;
        protected const string kDisplacementLockObjectScale = "_DisplacementLockObjectScale";
        protected MaterialProperty displacementLockTilingScale = null;
        protected const string kDisplacementLockTilingScale = "_DisplacementLockTilingScale";

        protected MaterialProperty depthOffsetEnable = null;
        protected const string kDepthOffsetEnable = "_DepthOffsetEnable";

        protected MaterialProperty tessellationMode = null;
        protected const string kTessellationMode = "_TessellationMode";

        protected MaterialProperty[] heightMap = new MaterialProperty[kMaxLayerCount];
        protected const string kHeightMap = "_HeightMap";
        protected MaterialProperty[] heightAmplitude = new MaterialProperty[kMaxLayerCount];
        protected const string kHeightAmplitude = "_HeightAmplitude";
        protected MaterialProperty[] heightCenter = new MaterialProperty[kMaxLayerCount];
        protected const string kHeightCenter = "_HeightCenter";
        protected MaterialProperty[] heightPoMAmplitude = new MaterialProperty[kMaxLayerCount];
        protected const string kHeightPoMAmplitude = "_HeightPoMAmplitude";
        protected MaterialProperty[] heightTessCenter = new MaterialProperty[kMaxLayerCount];
        protected const string kHeightTessCenter = "_HeightTessCenter";
        protected MaterialProperty[] heightTessAmplitude = new MaterialProperty[kMaxLayerCount];
        protected const string kHeightTessAmplitude = "_HeightTessAmplitude";
        protected MaterialProperty[] heightMin = new MaterialProperty[kMaxLayerCount];
        protected const string kHeightMin = "_HeightMin";
        protected MaterialProperty[] heightMax = new MaterialProperty[kMaxLayerCount];
        protected const string kHeightMax = "_HeightMax";
        protected MaterialProperty[] heightOffset = new MaterialProperty[kMaxLayerCount];
        protected const string kHeightOffset = "_HeightOffset";
        protected MaterialProperty[] heightParametrization = new MaterialProperty[kMaxLayerCount];
        protected const string kHeightParametrization = "_HeightMapParametrization";

        SurfaceType defaultSurfaceType { get { return SurfaceType.Opaque; } }

        // start faking MaterialProperty for renderQueue
        bool renderQueueHasMultipleDifferentValue
        {
            get
            {
                if (materialEditor.targets.Length < 2)
                    return false;

                int firstRenderQueue = renderQueue;
                for (int index = 1; index < materialEditor.targets.Length; ++index)
                {
                    if ((materialEditor.targets[index] as Material).renderQueue != firstRenderQueue)
                        return true;
                }
                return false;
            }
        }

        // TODO: does not support material multi-editing
        int renderQueue
        {
            get => (materialEditor.targets[0] as Material).renderQueue;
            set
            {
                foreach (var target in materialEditor.targets)
                {
                    (target as Material).renderQueue = value;
                }
            }
        }

        SurfaceType surfaceTypeValue
        {
            get { return surfaceType != null ? (SurfaceType)surfaceType.floatValue : defaultSurfaceType; }
        }

        bool showBlendModePopup { get { return true; } }
        bool showPreRefractionPass { get { return true; } }
        bool showLowResolutionPass { get { return true; } }
        bool showAfterPostProcessPass { get { return true; } }

        List<string> m_RenderingPassNames = new List<string>();
        List<int> m_RenderingPassValues = new List<int>();

        Expandable  m_ExpandableBit;
        Features    m_Features;
        int         m_LayerCount;

        public SurfaceOptionUIBlock(Expandable expandableBit, int layerCount = 1, Features features = Features.All)
        {
            m_ExpandableBit = expandableBit;
            m_Features = features;
            m_LayerCount = layerCount;
        }

        public override void LoadMaterialProperties()
        {
            surfaceType = FindProperty(kSurfaceType);
            useShadowThreshold = FindProperty(kUseShadowThreshold);
            alphaCutoffEnable = FindProperty(kAlphaCutoffEnabled);
            alphaCutoff = FindProperty(kAlphaCutoff);

            // TODO: implement features flags so we do not load unused fields
            alphaCutoffShadow = FindProperty(kAlphaCutoffShadow);
            alphaCutoffPrepass = FindProperty(kAlphaCutoffPrepass);
            alphaCutoffPostpass = FindProperty(kAlphaCutoffPostpass);
            transparentDepthPrepassEnable = FindProperty(kTransparentDepthPrepassEnable);
            transparentDepthPostpassEnable = FindProperty(kTransparentDepthPostpassEnable);
            transparentBackfaceEnable = FindProperty(kTransparentBackfaceEnable);

            transparentSortPriority = FindProperty(kTransparentSortPriority);

            transparentWritingMotionVec = FindProperty(kTransparentWritingMotionVec);
            
            enableBlendModePreserveSpecularLighting = FindProperty(kEnableBlendModePreserveSpecularLighting);
            enableFogOnTransparent = FindProperty(kEnableFogOnTransparent);

            if ((m_Features & Features.DoubleSided) != 0)
                doubleSidedEnable = FindProperty(kDoubleSidedEnable);
            
            // Height
            heightMap = FindPropertyLayered(kHeightMap, m_LayerCount);
            heightAmplitude = FindPropertyLayered(kHeightAmplitude, m_LayerCount);
            heightCenter = FindPropertyLayered(kHeightCenter, m_LayerCount);
            heightPoMAmplitude = FindPropertyLayered(kHeightPoMAmplitude, m_LayerCount);
            heightMin = FindPropertyLayered(kHeightMin, m_LayerCount);
            heightMax = FindPropertyLayered(kHeightMax, m_LayerCount);
            heightTessCenter = FindPropertyLayered(kHeightTessCenter, m_LayerCount);
            heightTessAmplitude = FindPropertyLayered(kHeightTessAmplitude, m_LayerCount);
            heightOffset = FindPropertyLayered(kHeightOffset, m_LayerCount);
            heightParametrization = FindPropertyLayered(kHeightParametrization, m_LayerCount);

            blendMode = FindProperty(kBlendMode);

            transmissionEnable = FindProperty(kTransmissionEnable);

            doubleSidedNormalMode = FindProperty(kDoubleSidedNormalMode);
            depthOffsetEnable = FindProperty(kDepthOffsetEnable);

            // MaterialID
            materialID = FindProperty(kMaterialID);
            transmissionEnable = FindProperty(kTransmissionEnable);

            displacementMode = FindProperty(kDisplacementMode);
            displacementLockObjectScale = FindProperty(kDisplacementLockObjectScale);
            displacementLockTilingScale = FindProperty(kDisplacementLockTilingScale);

            // Per pixel displacement
            ppdMinSamples = FindProperty(kPpdMinSamples);
            ppdMaxSamples = FindProperty(kPpdMaxSamples);
            ppdLodThreshold = FindProperty(kPpdLodThreshold);
            ppdPrimitiveLength = FindProperty(kPpdPrimitiveLength);
            ppdPrimitiveWidth  = FindProperty(kPpdPrimitiveWidth);
            invPrimScale = FindProperty(kInvPrimScale);

            // tessellation specific, silent if not found
            tessellationMode = FindProperty(kTessellationMode);

            // Decal
            supportDecals = FindProperty(kSupportDecals);

            // specular AA
            enableGeometricSpecularAA = FindProperty(kEnableGeometricSpecularAA);
            specularAAScreenSpaceVariance = FindProperty(kSpecularAAScreenSpaceVariance);
            specularAAThreshold = FindProperty(kSpecularAAThreshold);

            // SSR
            receivesSSR = FindProperty(kReceivesSSR);
        }

        public override void OnGUI()
        {
            using (var header = new MaterialHeaderScope(Styles.optionText, (uint)m_ExpandableBit, materialEditor))
            {
                if (header.expanded)
                    DrawSurfaceOptionGUI();
            }
        }

        void DrawSurfaceOptionGUI()
        {
            if ((m_Features & Features.Surface) != 0)
                DrawSurfaceGUI();

            if ((m_Features & Features.DoubleSided) != 0)
                DrawDoubleSidedGUI();

            if ((m_Features & Features.AlphaCutoff) != 0)
                DrawAlphaCutoffGUI();

            DrawLitSurfaceOptions();
        }

        void DrawAlphaCutoffGUI()
        {
            if (alphaCutoffEnable != null)
                materialEditor.ShaderProperty(alphaCutoffEnable, Styles.alphaCutoffEnableText);

            if (alphaCutoffEnable != null && alphaCutoffEnable.floatValue == 1.0f)
            {
                EditorGUI.indentLevel++;
                if ((m_Features & Features.AlphaCutoffThreshold) != 0)
                    materialEditor.ShaderProperty(alphaCutoff, Styles.alphaCutoffText);

                if (useShadowThreshold != null)
                    materialEditor.ShaderProperty(useShadowThreshold, Styles.useShadowThresholdText);

                if (alphaCutoffShadow != null && useShadowThreshold != null && useShadowThreshold.floatValue == 1.0f && (m_Features & Features.AlphaCutoffShadowThreshold) != 0)
                {
                    EditorGUI.indentLevel++;
                    materialEditor.ShaderProperty(alphaCutoffShadow, Styles.alphaCutoffShadowText);
                    EditorGUI.indentLevel--;
                }

                // With transparent object and few specific materials like Hair, we need more control on the cutoff to apply
                // This allow to get a better sorting (with prepass), better shadow (better silhouettes fidelity) etc...
                if (surfaceTypeValue == SurfaceType.Transparent)
                {
                    if (transparentDepthPrepassEnable != null && transparentDepthPrepassEnable.floatValue == 1.0f)
                    {
                        materialEditor.ShaderProperty(alphaCutoffPrepass, Styles.alphaCutoffPrepassText);
                    }

                    if (transparentDepthPostpassEnable != null && transparentDepthPostpassEnable.floatValue == 1.0f)
                    {
                        materialEditor.ShaderProperty(alphaCutoffPostpass, Styles.alphaCutoffPostpassText);
                    }
                }
                EditorGUI.indentLevel--;
            }
        }

        void DrawDoubleSidedGUI()
        {
            // This function must finish with double sided option (see LitUI.cs)
            if (doubleSidedEnable != null)
                materialEditor.ShaderProperty(doubleSidedEnable, Styles.doubleSidedEnableText);
        }

        void DrawSurfaceGUI()
        {
            SurfaceTypePopup();
            if (surfaceTypeValue == SurfaceType.Transparent)
            {
                EditorGUI.indentLevel++;

                if (renderQueueHasMultipleDifferentValue)
                {
                    using (new EditorGUI.DisabledScope(true))
                        EditorGUILayout.LabelField(Styles.blendModeText, Styles.notSupportedInMultiEdition);
                }
                else if (blendMode != null && showBlendModePopup)
                    BlendModePopup();

                EditorGUI.indentLevel++; if (renderQueueHasMultipleDifferentValue)
                {
                    using (new EditorGUI.DisabledScope(true))
                        EditorGUILayout.LabelField(Styles.enableBlendModePreserveSpecularLightingText, Styles.notSupportedInMultiEdition);
                }
                else if (enableBlendModePreserveSpecularLighting != null && blendMode != null && showBlendModePopup)
                    materialEditor.ShaderProperty(enableBlendModePreserveSpecularLighting, Styles.enableBlendModePreserveSpecularLightingText);
                EditorGUI.indentLevel--;

                if (transparentSortPriority != null)
                {
                    EditorGUI.BeginChangeCheck();
                    materialEditor.ShaderProperty(transparentSortPriority, Styles.transparentSortPriorityText);
                    if (EditorGUI.EndChangeCheck())
                    {
                        transparentSortPriority.floatValue = HDRenderQueue.ClampsTransparentRangePriority((int)transparentSortPriority.floatValue);
                        renderQueue = HDRenderQueue.ChangeType(HDRenderQueue.GetTypeByRenderQueueValue(renderQueue), offset: (int)transparentSortPriority.floatValue);
                    }
                }

                if (enableFogOnTransparent != null)
                    materialEditor.ShaderProperty(enableFogOnTransparent, Styles.enableTransparentFogText);

                if (transparentBackfaceEnable != null)
                    materialEditor.ShaderProperty(transparentBackfaceEnable, Styles.transparentBackfaceEnableText);

                if (transparentDepthPrepassEnable != null)
                    materialEditor.ShaderProperty(transparentDepthPrepassEnable, Styles.transparentDepthPrepassEnableText);

                if (transparentDepthPostpassEnable != null)
                    materialEditor.ShaderProperty(transparentDepthPostpassEnable, Styles.transparentDepthPostpassEnableText);

                if (transparentWritingMotionVec != null)
                    materialEditor.ShaderProperty(transparentWritingMotionVec, Styles.transparentWritingMotionVecText);

                EditorGUI.indentLevel--;
            }
        }

        void SurfaceTypePopup()
        {
            if (surfaceType == null)
                return;

            Material material = materialEditor.target as Material;
            var mode = (SurfaceType)surfaceType.floatValue;
            var renderQueueType = HDRenderQueue.GetTypeByRenderQueueValue(material.renderQueue);
            bool alphaTest = material.HasProperty(kAlphaCutoffEnabled) && material.GetFloat(kAlphaCutoffEnabled) > 0.0f;

            EditorGUI.showMixedValue = surfaceType.hasMixedValue;
            var newMode = (SurfaceType)EditorGUILayout.Popup(Styles.surfaceTypeText, (int)mode, Styles.surfaceTypeNames);
            if (newMode != mode) //EditorGUI.EndChangeCheck is called even if value remain the same after the popup. Prefer not to use it here
            {
                materialEditor.RegisterPropertyChangeUndo("Surface Type");
                surfaceType.floatValue = (float)newMode;
                HDRenderQueue.RenderQueueType targetQueueType;
                switch(newMode)
                {
                    case SurfaceType.Opaque:
                        targetQueueType = HDRenderQueue.GetOpaqueEquivalent(HDRenderQueue.GetTypeByRenderQueueValue(material.renderQueue));
                        break;
                    case SurfaceType.Transparent:
                        targetQueueType = HDRenderQueue.GetTransparentEquivalent(HDRenderQueue.GetTypeByRenderQueueValue(material.renderQueue));
                        break;
                    default:
                        throw new ArgumentException("Unknown SurfaceType");
                }
                renderQueue = HDRenderQueue.ChangeType(targetQueueType, (int)transparentSortPriority.floatValue, alphaTest);
            }
            EditorGUI.showMixedValue = false;

            bool isMixedRenderQueue = surfaceType.hasMixedValue || renderQueueHasMultipleDifferentValue;
            EditorGUI.showMixedValue = isMixedRenderQueue;
            ++EditorGUI.indentLevel;
            switch (mode)
            {
                case SurfaceType.Opaque:
                    //GetOpaqueEquivalent: prevent issue when switching surface type
                    HDRenderQueue.OpaqueRenderQueue renderQueueOpaqueType = HDRenderQueue.ConvertToOpaqueRenderQueue(HDRenderQueue.GetOpaqueEquivalent(renderQueueType));
                    var newRenderQueueOpaqueType = (HDRenderQueue.OpaqueRenderQueue)DoOpaqueRenderingPassPopup(Styles.renderingPassText, (int)renderQueueOpaqueType, showAfterPostProcessPass);
                    if (newRenderQueueOpaqueType != renderQueueOpaqueType) //EditorGUI.EndChangeCheck is called even if value remain the same after the popup. Prefer not to use it here
                    {
                        materialEditor.RegisterPropertyChangeUndo("Rendering Pass");
                        renderQueueType = HDRenderQueue.ConvertFromOpaqueRenderQueue(newRenderQueueOpaqueType);
                        renderQueue = HDRenderQueue.ChangeType(renderQueueType, alphaTest: alphaTest);
                    }
                    break;
                case SurfaceType.Transparent:
                    //GetTransparentEquivalent: prevent issue when switching surface type
                    HDRenderQueue.TransparentRenderQueue renderQueueTransparentType = HDRenderQueue.ConvertToTransparentRenderQueue(HDRenderQueue.GetTransparentEquivalent(renderQueueType));
                    var newRenderQueueTransparentType = (HDRenderQueue.TransparentRenderQueue)DoTransparentRenderingPassPopup(Styles.renderingPassText, (int)renderQueueTransparentType, showPreRefractionPass, showLowResolutionPass, showAfterPostProcessPass);
                    if (newRenderQueueTransparentType != renderQueueTransparentType) //EditorGUI.EndChangeCheck is called even if value remain the same after the popup. Prefer not to use it here
                    {
                        materialEditor.RegisterPropertyChangeUndo("Rendering Pass");
                        renderQueueType = HDRenderQueue.ConvertFromTransparentRenderQueue(newRenderQueueTransparentType);
                        renderQueue = HDRenderQueue.ChangeType(renderQueueType, offset: (int)transparentSortPriority.floatValue);
                    }
                    break;
                default:
                    throw new ArgumentException("Unknown SurfaceType");
            }
            --EditorGUI.indentLevel;
            EditorGUI.showMixedValue = false;
        }

        void BlendModePopup()
        {
            EditorGUI.showMixedValue = blendMode.hasMixedValue;
            var mode = (BlendMode)blendMode.floatValue;

            EditorGUI.BeginChangeCheck();
            mode = (BlendMode)EditorGUILayout.IntPopup(Styles.blendModeText, (int)mode, Styles.blendModeNames, Styles.blendModeValues);
            if (EditorGUI.EndChangeCheck())
            {
                materialEditor.RegisterPropertyChangeUndo("Blend Mode");
                blendMode.floatValue = (float)mode;
            }

            EditorGUI.showMixedValue = false;
        }

        int DoOpaqueRenderingPassPopup(string text, int inputValue, bool afterPost)
        {
            // Build UI enums
            m_RenderingPassNames.Clear();
            m_RenderingPassValues.Clear();

            m_RenderingPassNames.Add("Default");
            m_RenderingPassValues.Add((int)HDRenderQueue.OpaqueRenderQueue.Default);

            if (afterPost)
            {
                m_RenderingPassNames.Add("After post-process");
                m_RenderingPassValues.Add((int)HDRenderQueue.OpaqueRenderQueue.AfterPostProcessing);
            }

#if ENABLE_RAYTRACING
            m_RenderingPassNames.Add("Raytracing");
            m_RenderingPassValues.Add((int)HDRenderQueue.OpaqueRenderQueue.Raytracing);
#endif

            return EditorGUILayout.IntPopup(text, inputValue, m_RenderingPassNames.ToArray(), m_RenderingPassValues.ToArray());
        }

        int DoTransparentRenderingPassPopup(string text, int inputValue, bool refraction, bool lowRes, bool afterPost)
        {
            // Build UI enums
            m_RenderingPassNames.Clear();
            m_RenderingPassValues.Clear();

            if (refraction)
            {
                m_RenderingPassNames.Add("Before refraction");
                m_RenderingPassValues.Add((int)HDRenderQueue.TransparentRenderQueue.BeforeRefraction);
            }

            m_RenderingPassNames.Add("Default");
            m_RenderingPassValues.Add((int)HDRenderQueue.TransparentRenderQueue.Default);

            if (lowRes)
            {
                m_RenderingPassNames.Add("Low resolution");
                m_RenderingPassValues.Add((int)HDRenderQueue.TransparentRenderQueue.LowResolution);
            }

            if (afterPost)
            {
                m_RenderingPassNames.Add("After post-process");
                m_RenderingPassValues.Add((int)HDRenderQueue.TransparentRenderQueue.AfterPostProcessing);
            }

#if ENABLE_RAYTRACING
            m_RenderingPassNames.Add("Raytracing");
            m_RenderingPassValues.Add((int)HDRenderQueue.TransparentRenderQueue.Raytracing);
#endif

            return EditorGUILayout.IntPopup(text, inputValue, m_RenderingPassNames.ToArray(), m_RenderingPassValues.ToArray());
        }

        void DrawLitSurfaceOptions()
        {
            // This follow double sided option
            if (doubleSidedNormalMode != null && doubleSidedEnable != null && doubleSidedEnable.floatValue > 0.0f)
            {
                EditorGUI.indentLevel++;
                materialEditor.ShaderProperty(doubleSidedNormalMode, Styles.doubleSidedNormalModeText);
                EditorGUI.indentLevel--;
            }

            if (materialID != null)
            {
                materialEditor.ShaderProperty(materialID, Styles.materialIDText);

                if ((int)materialID.floatValue == (int)MaterialId.LitSSS)
                {
                    EditorGUI.indentLevel++;
                    materialEditor.ShaderProperty(transmissionEnable, Styles.transmissionEnableText);
                    EditorGUI.indentLevel--;
                }
            }

            if (supportDecals != null)
            {
                materialEditor.ShaderProperty(supportDecals, Styles.supportDecalsText);
            }

            if (receivesSSR != null)
            {
                materialEditor.ShaderProperty(receivesSSR, Styles.receivesSSRText);
            }

            if (enableGeometricSpecularAA != null)
            {
                materialEditor.ShaderProperty(enableGeometricSpecularAA, Styles.enableGeometricSpecularAAText);

                if (enableGeometricSpecularAA.floatValue > 0.0)
                {
                    EditorGUI.indentLevel++;
                    materialEditor.ShaderProperty(specularAAScreenSpaceVariance, Styles.specularAAScreenSpaceVarianceText);
                    materialEditor.ShaderProperty(specularAAThreshold, Styles.specularAAThresholdText);
                    EditorGUI.indentLevel--;
                }
            }

            if (displacementMode != null)
            {
                EditorGUI.BeginChangeCheck();
                FilterDisplacementMode();
                materialEditor.ShaderProperty(displacementMode, Styles.displacementModeText);
                if (EditorGUI.EndChangeCheck())
                {
                    for (int i = 0; i < m_LayerCount; i++)
                        UpdateDisplacement(i);
                }

                if ((DisplacementMode)displacementMode.floatValue != DisplacementMode.None)
                {
                    EditorGUI.indentLevel++;
                    materialEditor.ShaderProperty(displacementLockObjectScale, Styles.lockWithObjectScaleText);
                    materialEditor.ShaderProperty(displacementLockTilingScale, Styles.lockWithTilingRateText);
                    EditorGUI.indentLevel--;
                }

                if ((DisplacementMode)displacementMode.floatValue == DisplacementMode.Pixel)
                {
                    EditorGUILayout.Space();
                    EditorGUI.indentLevel++;
                    materialEditor.ShaderProperty(ppdMinSamples, Styles.ppdMinSamplesText);
                    materialEditor.ShaderProperty(ppdMaxSamples, Styles.ppdMaxSamplesText);
                    ppdMinSamples.floatValue = Mathf.Min(ppdMinSamples.floatValue, ppdMaxSamples.floatValue);
                    materialEditor.ShaderProperty(ppdLodThreshold, Styles.ppdLodThresholdText);
                    materialEditor.ShaderProperty(ppdPrimitiveLength, Styles.ppdPrimitiveLength);
                    ppdPrimitiveLength.floatValue = Mathf.Max(0.01f, ppdPrimitiveLength.floatValue);
                    materialEditor.ShaderProperty(ppdPrimitiveWidth, Styles.ppdPrimitiveWidth);
                    ppdPrimitiveWidth.floatValue = Mathf.Max(0.01f, ppdPrimitiveWidth.floatValue);
                    invPrimScale.vectorValue = new Vector4(1.0f / ppdPrimitiveLength.floatValue, 1.0f / ppdPrimitiveWidth.floatValue); // Precompute
                    materialEditor.ShaderProperty(depthOffsetEnable, Styles.depthOffsetEnableText);
                    EditorGUI.indentLevel--;
                }
            }
        }

        public void UpdateDisplacement(int layerIndex)
        {
            DisplacementMode displaceMode = (DisplacementMode)displacementMode.floatValue;
            if (displaceMode == DisplacementMode.Pixel)
            {
                heightAmplitude[layerIndex].floatValue = heightPoMAmplitude[layerIndex].floatValue * 0.01f; // Conversion centimeters to meters.
                heightCenter[layerIndex].floatValue = 1.0f; // PoM is always inward so base (0 height) is mapped to 1 in the texture
            }
            else
            {
                HeightmapParametrization parametrization = (HeightmapParametrization)heightParametrization[layerIndex].floatValue;
                if (parametrization == HeightmapParametrization.MinMax)
                {
                    float offset = heightOffset[layerIndex].floatValue;
                    float amplitude = (heightMax[layerIndex].floatValue - heightMin[layerIndex].floatValue);

                    heightAmplitude[layerIndex].floatValue = amplitude * 0.01f; // Conversion centimeters to meters.
                    heightCenter[layerIndex].floatValue = -(heightMin[layerIndex].floatValue + offset) / Mathf.Max(1e-6f, amplitude);
                }
                else
                {
                    float amplitude = heightTessAmplitude[layerIndex].floatValue;
                    heightAmplitude[layerIndex].floatValue = amplitude * 0.01f;
                    heightCenter[layerIndex].floatValue = -heightOffset[layerIndex].floatValue / Mathf.Max(1e-6f, amplitude) + heightTessCenter[layerIndex].floatValue;
                }
            }
        }

        void FilterDisplacementMode()
        {
            if (tessellationMode == null)
            {
                if ((DisplacementMode)displacementMode.floatValue == DisplacementMode.Tessellation)
                    displacementMode.floatValue = (float)DisplacementMode.None;
            }
            else
            {
                if ((DisplacementMode)displacementMode.floatValue == DisplacementMode.Pixel || (DisplacementMode)displacementMode.floatValue == DisplacementMode.Vertex)
                    displacementMode.floatValue = (float)DisplacementMode.None;
            }
        }
    }
}