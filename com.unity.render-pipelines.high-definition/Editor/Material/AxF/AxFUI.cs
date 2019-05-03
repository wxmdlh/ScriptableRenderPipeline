using System;
using UnityEngine;
using UnityEngine.Experimental.Rendering.HDPipeline;
using UnityEngine.Rendering;

namespace UnityEditor.Experimental.Rendering.HDPipeline
{
    class AxFGUI : ShaderGUI
    {
        // protected override uint defaultExpandedState { get { return (uint)(Expandable.Base | Expandable.Detail | Expandable.Emissive | Expandable.Input | Expandable.Other | Expandable.Tesselation | Expandable.Transparency | Expandable.VertexAnimation); } }

        MaterialUIBlockList uiBlocks = new MaterialUIBlockList
        {
            new SurfaceOptionUIBlock(MaterialUIBlock.Expandable.Base, features: SurfaceOptionUIBlock.Features.Unlit),
            new AxfSurfaceInputsUIBlock(MaterialUIBlock.Expandable.Input),
            new AdvancedOptionsUIBlock(MaterialUIBlock.Expandable.Advance, AdvancedOptionsUIBlock.Features.Instancing),
        };

        public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] props)
        {
            using (var changed = new EditorGUI.ChangeCheckScope())
            {
                // Some logic to disable the transparency block if we're opaque:
                uiBlocks.OnGUI(materialEditor, props);

                // Apply material keywords and pass:
                if (changed.changed)
                {
                    foreach (var material in uiBlocks.materials)
                        SetupMaterialKeywordsAndPass(material);
                }
            }
        }

        protected static class Styles
        {
            public static string InputsText = "Surface Inputs";

            /////////////////////////////////////////////////////////////////////////////////////////////////
            // SVBRDF Parameters
            public static GUIContent    diffuseColorMapText = new GUIContent("Diffuse Color");
            public static GUIContent    specularColorMapText = new GUIContent("Specular Color");
            public static GUIContent    specularLobeMapText = new GUIContent("Specular Lobe");
            public static GUIContent    specularLobeMapScaleText = new GUIContent("Specular Lobe Scale");
            public static GUIContent    fresnelMapText = new GUIContent("Fresnel");
            public static GUIContent    normalMapText = new GUIContent("Normal");

            // Alpha
            public static GUIContent    alphaMapText = new GUIContent("Alpha");

            // Displacement
            public static GUIContent    heightMapText = new GUIContent("Height");

            // Anisotropy
            public static GUIContent    anisoRotationMapText = new GUIContent("Anisotropy Angle");

            /////////////////////////////////////////////////////////////////////////////////////////////////
            // Car Paint Parameters
            public static GUIContent    BRDFColorMapText = new GUIContent("BRDF Color");
            public static GUIContent    BRDFColorMapScaleText = new GUIContent("BRDF Color Scale");
            public static GUIContent    BRDFColorMapUVScaleText = new GUIContent("BRDF Color Map UV scale restriction");

            public static GUIContent    BTFFlakesMapText = new GUIContent("BTF Flake Color Texture2DArray");
            public static GUIContent    BTFFlakesMapScaleText = new GUIContent("BTF Flakes Scale");
            public static GUIContent    FlakesTilingText = new GUIContent("Flakes Tiling");

            public static GUIContent    thetaFI_sliceLUTMapText = new GUIContent("ThetaFI Slice LUT");

            public static GUIContent    CarPaintIORText = new GUIContent("IOR");

            /////////////////////////////////////////////////////////////////////////////////////////////////
            // Generic

            // Clearcoat
            public static GUIContent    clearcoatColorMapText = new GUIContent("Clearcoat Color");
            public static GUIContent    clearcoatNormalMapText = new GUIContent("Clearcoat Normal");
            public static GUIContent    clearcoatIORMapText = new GUIContent("Clearcoat IOR");

            public static GUIContent    supportDecalsText = new GUIContent("Enable Decal", "Specify whether the material can receive decals.");
            public static GUIContent    receivesSSRText = new GUIContent("Receives SSR", "Specify whether the material can receive screen space reflection.");
        }

        enum    AxfBrdfType
        {
            SVBRDF,
            CAR_PAINT,
            BTF,
        }
        static readonly string[]    AxfBrdfTypeNames = Enum.GetNames(typeof(AxfBrdfType));

        enum    SvbrdfDiffuseType
        {
            LAMBERT = 0,
            OREN_NAYAR = 1,
        }
        static readonly string[]    SvbrdfDiffuseTypeNames = Enum.GetNames(typeof(SvbrdfDiffuseType));

        enum    SvbrdfSpecularType
        {
            WARD = 0,
            BLINN_PHONG = 1,
            COOK_TORRANCE = 2,
            GGX = 3,
            PHONG = 4,
        }
        static readonly string[]    SvbrdfSpecularTypeNames = Enum.GetNames(typeof(SvbrdfSpecularType));

        enum    SvbrdfSpecularVariantWard   // Ward variants
        {
            GEISLERMORODER,     // 2010 (albedo-conservative, should always be preferred!)
            DUER,               // 2006
            WARD,               // 1992 (original paper)
        }
        static readonly string[]    SvbrdfSpecularVariantWardNames = Enum.GetNames(typeof(SvbrdfSpecularVariantWard));
        enum    SvbrdfSpecularVariantBlinn  // Blinn-Phong variants
        {
            ASHIKHMIN_SHIRLEY,  // 2000
            BLINN,              // 1977 (original paper)
            VRAY,
            LEWIS,              // 1993
        }
        static readonly string[]    SvbrdfSpecularVariantBlinnNames = Enum.GetNames(typeof(SvbrdfSpecularVariantBlinn));

        enum    SvbrdfFresnelVariant
        {
            NO_FRESNEL,         // No fresnel
            FRESNEL,            // Full fresnel (1818)
            SCHLICK,            // Schlick's Approximation (1994)
        }
        static readonly string[]    SvbrdfFresnelVariantNames = Enum.GetNames(typeof(SvbrdfFresnelVariant));

        /////////////////////////////////////////////////////////////////////////////////////////////////
        // Generic Parameters
        static string               m_MaterialTilingUText = "_MaterialTilingU";
        protected MaterialProperty  m_MaterialTilingU;
        static string               m_MaterialTilingVText = "_MaterialTilingV";
        protected MaterialProperty  m_MaterialTilingV;

        static string               m_AxF_BRDFTypeText = "_AxF_BRDFType";
        protected MaterialProperty  m_AxF_BRDFType = null;

        static string               m_FlagsText = "_Flags";
        protected MaterialProperty  m_Flags;

        /////////////////////////////////////////////////////////////////////////////////////////////////
        // SVBRDF Parameters
        static string               m_SVBRDF_BRDFTypeText = "_SVBRDF_BRDFType";
        protected MaterialProperty  m_SVBRDF_BRDFType;
        static string               m_SVBRDF_BRDFVariantsText = "_SVBRDF_BRDFVariants";
        protected MaterialProperty  m_SVBRDF_BRDFVariants;
        static string               m_SVBRDF_HeightMapMaxMMText = "_SVBRDF_HeightMapMaxMM";
        protected MaterialProperty  m_SVBRDF_HeightMapMaxMM;

        // Regular maps
        static string               m_DiffuseColorMapText = "_SVBRDF_DiffuseColorMap";
        protected MaterialProperty  m_DiffuseColorMap = null;
        static string               m_SpecularColorMapText = "_SVBRDF_SpecularColorMap";
        protected MaterialProperty  m_SpecularColorMap = null;

        static string               m_SpecularLobeMapText = "_SVBRDF_SpecularLobeMap";
        protected MaterialProperty  m_SpecularLobeMap = null;
        static string               m_SpecularLobeMapScaleText = "_SVBRDF_SpecularLobeMapScale";
        protected MaterialProperty  m_SpecularLobeMapScale;

        static string               m_FresnelMapText = "_SVBRDF_FresnelMap";
        protected MaterialProperty  m_FresnelMap = null;
        static string               m_NormalMapText = "_SVBRDF_NormalMap";
        protected MaterialProperty  m_NormalMap = null;

        // Alpha
        static string               m_AlphaMapText = "_SVBRDF_AlphaMap";
        protected MaterialProperty  m_AlphaMap = null;

        // Displacement
        static string               m_HeightMapText = "_SVBRDF_HeightMap";
        protected MaterialProperty  m_HeightMap = null;

        // Anisotropy
        static string               m_AnisoRotationMapText = "_SVBRDF_AnisoRotationMap";
        protected MaterialProperty  m_AnisoRotationMap = null;


        /////////////////////////////////////////////////////////////////////////////////////////////////
        // Car Paint Parameters
        static string               m_CarPaint2_BRDFColorMapText = "_CarPaint2_BRDFColorMap";
        protected MaterialProperty  m_CarPaint2_BRDFColorMap = null;

        static string               m_CarPaint2_BRDFColorMapScaleText = "_CarPaint2_BRDFColorMapScale";
        protected MaterialProperty  m_CarPaint2_BRDFColorMapScale;

        static string               m_CarPaint2_BRDFColorMapUVScaleText = "_CarPaint2_BRDFColorMapUVScale";
        protected MaterialProperty  m_CarPaint2_BRDFColorMapUVScale;

        static string               m_CarPaint2_BTFFlakeMapText = "_CarPaint2_BTFFlakeMap";
        protected MaterialProperty  m_CarPaint2_BTFFlakeMap = null;

        static string               m_CarPaint2_BTFFlakeMapScaleText = "_CarPaint2_BTFFlakeMapScale";
        protected MaterialProperty  m_CarPaint2_BTFFlakeMapScale;

        static string               m_CarPaint2_FlakeTilingText = "_CarPaint2_FlakeTiling";
        protected MaterialProperty  m_CarPaint2_FlakeTiling;

        static string               m_CarPaint2_FlakeThetaFISliceLUTMapText = "_CarPaint2_FlakeThetaFISliceLUTMap";
        protected MaterialProperty  m_CarPaint2_FlakeThetaFISliceLUTMap;

        static string               m_CarPaint2_FlakeMaxThetaIText = "_CarPaint2_FlakeMaxThetaI";
        protected MaterialProperty  m_CarPaint2_FlakeMaxThetaI;
        static string               m_CarPaint2_FlakeNumThetaFText = "_CarPaint2_FlakeNumThetaF";
        protected MaterialProperty  m_CarPaint2_FlakeNumThetaF;
        static string               m_CarPaint2_FlakeNumThetaIText = "_CarPaint2_FlakeNumThetaI";
        protected MaterialProperty  m_CarPaint2_FlakeNumThetaI;

        static string               m_CarPaint2_ClearcoatIORText = "_CarPaint2_ClearcoatIOR";
        protected MaterialProperty  m_CarPaint2_ClearcoatIOR;

        /////////////////////////////////////////////////////////////////////////////////////////////////
        // Clearcoat
        static string               m_ClearcoatColorMapText = "_SVBRDF_ClearcoatColorMap";
        protected MaterialProperty  m_ClearcoatColorMap = null;
        static string               m_ClearcoatNormalMapText = "_ClearcoatNormalMap";
        protected MaterialProperty  m_ClearcoatNormalMap = null;
        static string               m_ClearcoatIORMapText = "_SVBRDF_ClearcoatIORMap";
        protected MaterialProperty  m_ClearcoatIORMap = null;

        // Stencil refs and masks
        protected const string kStencilRef = "_StencilRef";
        protected const string kStencilWriteMask = "_StencilWriteMask";
        protected const string kStencilRefDepth = "_StencilRefDepth";
        protected const string kStencilWriteMaskDepth = "_StencilWriteMaskDepth";
        protected const string kStencilRefMV = "_StencilRefMV";
        protected const string kStencilWriteMaskMV = "_StencilWriteMaskMV";

        // Decals and SSR
        protected const string kEnableDecals = "_SupportDecals";
        protected const string kEnableSSR = "_ReceivesSSR";
        protected MaterialProperty m_SupportDecals = null;
        protected MaterialProperty m_ReceivesSSR = null;

        // All Setup Keyword functions must be static. It allow to create script to automatically update the shaders with a script if code change
        static public void SetupMaterialKeywordsAndPass(Material material)
        {
            material.SetupBaseUnlitKeywords();
            material.SetupBaseUnlitPass();

            AxfBrdfType   BRDFType = (AxfBrdfType)material.GetFloat(m_AxF_BRDFTypeText);

            CoreUtils.SetKeyword(material, "_AXF_BRDF_TYPE_SVBRDF", BRDFType == AxfBrdfType.SVBRDF);
            CoreUtils.SetKeyword(material, "_AXF_BRDF_TYPE_CAR_PAINT", BRDFType == AxfBrdfType.CAR_PAINT);
            CoreUtils.SetKeyword(material, "_AXF_BRDF_TYPE_BTF", BRDFType == AxfBrdfType.BTF);

            // Keywords for opt-out of decals and SSR:
            bool decalsEnabled = material.HasProperty(kEnableDecals) && material.GetFloat(kEnableDecals) > 0.0f;
            CoreUtils.SetKeyword(material, "_DISABLE_DECALS", decalsEnabled == false);
            bool ssrEnabled = material.HasProperty(kEnableSSR) && material.GetFloat(kEnableSSR) > 0.0f;
            CoreUtils.SetKeyword(material, "_DISABLE_SSR", ssrEnabled == false);

            // Set the reference values for the stencil test

            // Stencil usage rules:
            // DoesntReceiveSSR and DecalsForwardOutputNormalBuffer need to be tagged during depth prepass
            // LightingMask need to be tagged during either GBuffer or Forward pass
            // ObjectMotionVectors need to be tagged in motion vectors pass.
            // As motion vectors pass can be use as a replacement of depth prepass it also need to have DoesntReceiveSSR and DecalsForwardOutputNormalBuffer
            // Object motion vectors is always render after a full depth buffer (if there is no depth prepass for GBuffer all object motion vectors are render after GBuffer)
            // so we have a guarantee than when we write object motion vectors no other object will be draw on top (and so would have require to overwrite motion vectors).
            // Final combination is:
            // Prepass: DoesntReceiveSSR,  DecalsForwardOutputNormalBuffer
            // Motion vectors: DoesntReceiveSSR,  DecalsForwardOutputNormalBuffer, ObjectMotionVectors
            // GBuffer: LightingMask, DecalsForwardOutputNormalBuffer, ObjectMotionVectors
            // Forward: LightingMask

            int stencilRef = (int)StencilLightingUsage.RegularLighting;
            int stencilWriteMask = (int)HDRenderPipeline.StencilBitMask.LightingMask;
            int stencilRefDepth = 0;
            int stencilWriteMaskDepth = 0;
            int stencilRefMV = (int)HDRenderPipeline.StencilBitMask.ObjectMotionVectors;
            int stencilWriteMaskMV = (int)HDRenderPipeline.StencilBitMask.ObjectMotionVectors;

            if (!ssrEnabled)
            {
                stencilRefDepth |= (int)HDRenderPipeline.StencilBitMask.DoesntReceiveSSR;
                stencilRefMV |= (int)HDRenderPipeline.StencilBitMask.DoesntReceiveSSR;
            }

            if (decalsEnabled)
            {
                stencilRefDepth |= (int)HDRenderPipeline.StencilBitMask.DecalsForwardOutputNormalBuffer;
                stencilRefMV |= (int)HDRenderPipeline.StencilBitMask.DecalsForwardOutputNormalBuffer;
            }

            stencilWriteMaskDepth |= (int)HDRenderPipeline.StencilBitMask.DoesntReceiveSSR | (int)HDRenderPipeline.StencilBitMask.DecalsForwardOutputNormalBuffer;
            stencilWriteMaskMV |= (int)HDRenderPipeline.StencilBitMask.DoesntReceiveSSR | (int)HDRenderPipeline.StencilBitMask.DecalsForwardOutputNormalBuffer;

            // As we tag both during motion vector pass and Gbuffer pass we need a separate state and we need to use the write mask
            material.SetInt(kStencilRef, stencilRef);
            material.SetInt(kStencilWriteMask, stencilWriteMask);
            material.SetInt(kStencilRefDepth, stencilRefDepth);
            material.SetInt(kStencilWriteMaskDepth, stencilWriteMaskDepth);
            material.SetInt(kStencilRefMV, stencilRefMV);
            material.SetInt(kStencilWriteMaskMV, stencilWriteMaskMV);
        }
    }
} // namespace UnityEditor
