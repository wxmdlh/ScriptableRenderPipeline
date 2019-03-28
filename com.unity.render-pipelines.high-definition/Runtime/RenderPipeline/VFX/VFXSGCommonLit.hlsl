#include "Packages/com.unity.render-pipelines.high-definition/Runtime/Material/BuiltinUtilities.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Sampling/SampleUVMapping.hlsl"
#include "Packages/com.unity.render-pipelines.high-definition/Runtime/Material/MaterialUtilities.hlsl"
#include "Packages/com.unity.render-pipelines.high-definition/Runtime/Material/Decal/DecalUtilities.hlsl"
#include "Packages/com.unity.render-pipelines.high-definition/Runtime/Material/Lit/LitDecalData.hlsl"
//#include "Packages/com.unity.render-pipelines.high-definition/Runtime/Material/BuiltinUtilities.hlsl"
#include "Packages/com.unity.shadergraph/ShaderGraphLibrary/Functions.hlsl"

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
FragInputForSG InitializeStructs(inout FragInputs input, out SurfaceData surfaceData, out BuiltinData builtinData)
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

    surfaceData = (SurfaceData)0;
    builtinData = (BuiltinData)0;
    
    //Setup default value in case sg does not set them
    surfaceData.metallic = 1.0;
    surfaceData.ambientOcclusion = 1.0;
    surfaceData.anisotropy = 1.0;

    surfaceData.ior = 1.0;
    surfaceData.transmittanceColor = float3(1.0, 1.0, 1.0);
    surfaceData.atDistance = 1.0;
    surfaceData.transmittanceMask = 0.0;

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

    surfaceData.specularOcclusion = 1.0;

    return fisg;
}

void PostInit(FragInputs input, inout SurfaceData surfaceData, inout BuiltinData builtinData, PositionInputs posInput,float3 bentNormalWS, float alpha, float3 V)
{
    surfaceData.tangentWS = Orthonormalize(surfaceData.tangentWS, surfaceData.normalWS);

    InitBuiltinData(posInput, alpha, bentNormalWS, -input.worldToTangent[2], input.texCoord1, input.texCoord2, builtinData);

    //builtinData.depthOffset = depthOffset;

    PostInitBuiltinData(V, posInput, surfaceData, builtinData);
}

