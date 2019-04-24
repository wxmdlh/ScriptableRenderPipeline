using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering.HDPipeline;

namespace UnityEditor.Experimental.Rendering.HDPipeline
{
    public class SurfaceOptionUIBlock : MaterialUIBlock
    {
        [Flags]
        public enum Features
        {
            Surface,
            BlendMode,
            DoubleSided,
            AlphaCutoff,
            AlphaCutoffThreshold,
            AlphaCutoffShadow,
            All = ~0
        }

        protected static class StylesBaseUnlit
        {
            public const string TransparencyInputsText = "Transparency Inputs";
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
            public static GUIContent alphaCutoffShadowText = new GUIContent("Shadow Threshold", "Contols the threshold for shadow pass alpha clipping.");
            public static GUIContent alphaCutoffPrepassText = new GUIContent("Prepass Threshold", "Controls the threshold for transparent depth prepass alpha clipping.");
            public static GUIContent alphaCutoffPostpassText = new GUIContent("Postpass Threshold", "Controls the threshold for transparent depth postpass alpha clipping.");
            public static GUIContent transparentDepthPostpassEnableText = new GUIContent("Transparent Depth Postpass", "When enabled, HDRP renders a depth postpass for transparent objects. This improves post-processing effects like depth of field.");
            public static GUIContent transparentDepthPrepassEnableText = new GUIContent("Transparent Depth Prepass", "When enabled, HDRP renders a depth prepass for transparent GameObjects. This improves sorting.");
            public static GUIContent transparentBackfaceEnableText = new GUIContent("Back Then Front Rendering", "When enabled, HDRP renders the back face and then the front face, in two separate draw calls, to better sort transparent meshes.");
            public static GUIContent transparentWritingMotionVecText = new GUIContent("Transparent Writes Motion Vectors", "When enabled, transparent objects write motion vectors, these replace what was previously rendered in the buffer.");

            public static GUIContent transparentSortPriorityText = new GUIContent("Sorting Priority", "Sets the sort priority (from -100 to 100) of transparent meshes using this Material. HDRP uses this value to calculate the sorting order of all transparent meshes on screen.");
            public static GUIContent enableTransparentFogText = new GUIContent("Receive fog", "When enabled, this Material can receive fog.");
            public static GUIContent enableBlendModePreserveSpecularLightingText = new GUIContent("Preserve specular lighting", "When enabled, blending only affects diffuse lighting, allowing for correct specular lighting on transparent meshes that use this Material.");
        }
   
        protected MaterialProperty surfaceType = null;
        protected const string kSurfaceType = "_SurfaceType";
        
        protected MaterialProperty alphaCutoffEnable = null;
        protected const string kAlphaCutoffEnabled = "_AlphaCutoffEnable";
        protected MaterialProperty useShadowThreshold = null;
        protected const string kUseShadowThreshold = "_UseShadowThreshold";
        protected MaterialProperty alphaCutoff = null;
        protected const string kAlphaCutoff = "_AlphaCutoff";
        protected MaterialProperty alphaCutoffShadow = null;
        protected const string kAlphaCutoffShadow = "_AlphaCutoffShadow";
        protected MaterialProperty alphaCutoffPrepass = null;
        protected const string kAlphaCutoffPrepass = "_AlphaCutoffPrepass";
        protected MaterialProperty alphaCutoffPostpass = null;
        protected const string kAlphaCutoffPostpass = "_AlphaCutoffPostpass";
        protected MaterialProperty transparentDepthPrepassEnable = null;
        protected const string kTransparentDepthPrepassEnable = "_TransparentDepthPrepassEnable";
        protected MaterialProperty transparentDepthPostpassEnable = null;
        protected const string kTransparentDepthPostpassEnable = "_TransparentDepthPostpassEnable";
        protected MaterialProperty transparentBackfaceEnable = null;
        protected const string kTransparentBackfaceEnable = "_TransparentBackfaceEnable";
        protected MaterialProperty transparentSortPriority = null;
        protected const string kTransparentSortPriority = "_TransparentSortPriority";
        protected MaterialProperty transparentWritingMotionVec = null;
        protected const string kTransparentWritingMotionVec = "_TransparentWritingMotionVec";
        protected MaterialProperty doubleSidedEnable = null;
        protected const string kDoubleSidedEnable = "_DoubleSidedEnable";
        protected MaterialProperty blendMode = null;
        protected const string kBlendMode = "_BlendMode";
        protected MaterialProperty enableBlendModePreserveSpecularLighting = null;
        protected const string kEnableBlendModePreserveSpecularLighting = "_EnableBlendModePreserveSpecularLighting";
        protected MaterialProperty enableFogOnTransparent = null;
        protected const string kEnableFogOnTransparent = "_EnableFogOnTransparent";

        protected virtual SurfaceType defaultSurfaceType { get { return SurfaceType.Opaque; } }

        // start faking MaterialProperty for renderQueue
        protected bool renderQueueHasMultipleDifferentValue
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

        public SurfaceOptionUIBlock(Expandable expandableBit)
        {
            m_ExpandableBit = expandableBit;
        }

        public override void LoadMaterialKeywords()
        {
            surfaceType = FindProperty(kSurfaceType);
            useShadowThreshold = FindProperty(kUseShadowThreshold, false);
            alphaCutoffEnable = FindProperty(kAlphaCutoffEnabled);
            alphaCutoff = FindProperty(kAlphaCutoff);

            // TODO: implement features flags so we do not load unused fields
            alphaCutoffShadow = FindProperty(kAlphaCutoffShadow, false);
            alphaCutoffPrepass = FindProperty(kAlphaCutoffPrepass, false);
            alphaCutoffPostpass = FindProperty(kAlphaCutoffPostpass, false);
            transparentDepthPrepassEnable = FindProperty(kTransparentDepthPrepassEnable, false);
            transparentDepthPostpassEnable = FindProperty(kTransparentDepthPostpassEnable, false);
            transparentBackfaceEnable = FindProperty(kTransparentBackfaceEnable, false);

            transparentSortPriority = FindProperty(kTransparentSortPriority);

            transparentWritingMotionVec = FindProperty(kTransparentWritingMotionVec, false);
            
            enableBlendModePreserveSpecularLighting = FindProperty(kEnableBlendModePreserveSpecularLighting, false);
            enableFogOnTransparent = FindProperty(kEnableFogOnTransparent, false);

            doubleSidedEnable = FindProperty(kDoubleSidedEnable, false);
            blendMode = FindProperty(kBlendMode);
        }

        public override void OnGUI()
        {
            using (var header = new HeaderScope(StylesBaseUnlit.optionText, (uint)m_ExpandableBit, materialEditor))
            {
                if (header.expanded)
                    DrawSurfaceOptionGUI();
            }
        }

        void DrawSurfaceOptionGUI()
        {
            SurfaceTypePopup();
            if (surfaceTypeValue == SurfaceType.Transparent)
            {
                EditorGUI.indentLevel++;

                if (renderQueueHasMultipleDifferentValue)
                {
                    using (new EditorGUI.DisabledScope(true))
                        EditorGUILayout.LabelField(StylesBaseUnlit.blendModeText, StylesBaseUnlit.notSupportedInMultiEdition);
                }
                else if (blendMode != null && showBlendModePopup)
                    BlendModePopup();

                EditorGUI.indentLevel++; if (renderQueueHasMultipleDifferentValue)
                {
                    using (new EditorGUI.DisabledScope(true))
                        EditorGUILayout.LabelField(StylesBaseUnlit.enableBlendModePreserveSpecularLightingText, StylesBaseUnlit.notSupportedInMultiEdition);
                }
                else if (enableBlendModePreserveSpecularLighting != null && blendMode != null && showBlendModePopup)
                    materialEditor.ShaderProperty(enableBlendModePreserveSpecularLighting, StylesBaseUnlit.enableBlendModePreserveSpecularLightingText);
                EditorGUI.indentLevel--;

                if (transparentSortPriority != null)
                {
                    EditorGUI.BeginChangeCheck();
                    materialEditor.ShaderProperty(transparentSortPriority, StylesBaseUnlit.transparentSortPriorityText);
                    if (EditorGUI.EndChangeCheck())
                    {
                        transparentSortPriority.floatValue = HDRenderQueue.ClampsTransparentRangePriority((int)transparentSortPriority.floatValue);
                    }
                }

                if (enableFogOnTransparent != null)
                    materialEditor.ShaderProperty(enableFogOnTransparent, StylesBaseUnlit.enableTransparentFogText);

                if (transparentBackfaceEnable != null)
                    materialEditor.ShaderProperty(transparentBackfaceEnable, StylesBaseUnlit.transparentBackfaceEnableText);

                if (transparentDepthPrepassEnable != null)
                    materialEditor.ShaderProperty(transparentDepthPrepassEnable, StylesBaseUnlit.transparentDepthPrepassEnableText);

                if (transparentDepthPostpassEnable != null)
                    materialEditor.ShaderProperty(transparentDepthPostpassEnable, StylesBaseUnlit.transparentDepthPostpassEnableText);

                if (transparentWritingMotionVec != null)
                    materialEditor.ShaderProperty(transparentWritingMotionVec, StylesBaseUnlit.transparentWritingMotionVecText);

                EditorGUI.indentLevel--;
            }

            // This function must finish with double sided option (see LitUI.cs)
            if (doubleSidedEnable != null)
            {
                materialEditor.ShaderProperty(doubleSidedEnable, StylesBaseUnlit.doubleSidedEnableText);
            }

            if (alphaCutoffEnable != null)
                materialEditor.ShaderProperty(alphaCutoffEnable, StylesBaseUnlit.alphaCutoffEnableText);

            if (alphaCutoffEnable != null && alphaCutoffEnable.floatValue == 1.0f)
            {
                EditorGUI.indentLevel++;
                materialEditor.ShaderProperty(alphaCutoff, StylesBaseUnlit.alphaCutoffText);

                if (useShadowThreshold != null)
                    materialEditor.ShaderProperty(useShadowThreshold, StylesBaseUnlit.useShadowThresholdText);

                if (alphaCutoffShadow != null && useShadowThreshold != null && useShadowThreshold.floatValue == 1.0f)
                {
                    EditorGUI.indentLevel++;
                    materialEditor.ShaderProperty(alphaCutoffShadow, StylesBaseUnlit.alphaCutoffShadowText);
                    EditorGUI.indentLevel--;
                }

                // With transparent object and few specific materials like Hair, we need more control on the cutoff to apply
                // This allow to get a better sorting (with prepass), better shadow (better silhouettes fidelity) etc...
                if (surfaceTypeValue == SurfaceType.Transparent)
                {
                    if (transparentDepthPrepassEnable != null && transparentDepthPrepassEnable.floatValue == 1.0f)
                    {
                        materialEditor.ShaderProperty(alphaCutoffPrepass, StylesBaseUnlit.alphaCutoffPrepassText);
                    }

                    if (transparentDepthPostpassEnable != null && transparentDepthPostpassEnable.floatValue == 1.0f)
                    {
                        materialEditor.ShaderProperty(alphaCutoffPostpass, StylesBaseUnlit.alphaCutoffPostpassText);
                    }
                }
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
            var newMode = (SurfaceType)EditorGUILayout.Popup(StylesBaseUnlit.surfaceTypeText, (int)mode, StylesBaseUnlit.surfaceTypeNames);
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
                    var newRenderQueueOpaqueType = (HDRenderQueue.OpaqueRenderQueue)DoOpaqueRenderingPassPopup(StylesBaseUnlit.renderingPassText, (int)renderQueueOpaqueType, showAfterPostProcessPass);
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
                    var newRenderQueueTransparentType = (HDRenderQueue.TransparentRenderQueue)DoTransparentRenderingPassPopup(StylesBaseUnlit.renderingPassText, (int)renderQueueTransparentType, showPreRefractionPass, showLowResolutionPass, showAfterPostProcessPass);
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
            mode = (BlendMode)EditorGUILayout.IntPopup(StylesBaseUnlit.blendModeText, (int)mode, StylesBaseUnlit.blendModeNames, StylesBaseUnlit.blendModeValues);
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

    }
}