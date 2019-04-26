#include "Packages/com.unity.render-pipelines.high-definition/Runtime/Material/NormalBuffer.hlsl"

TEXTURE2D(_DebugMatCapTexture);

float4 GetMatcapValue(float2 positionSS)
{
    float depth = LoadCameraDepth(positionSS);

    if (depth == UNITY_RAW_FAR_CLIP_VALUE)
        return 0.33f;

    NormalData normalData;
    DecodeFromNormalBuffer(positionSS, normalData);
    float3 normalVS = mul((float3x3)UNITY_MATRIX_V, normalData.normalWS).xyz;
    float2 UV = saturate(normalVS.xy * 0.5f + 0.5f);
    return SAMPLE_TEXTURE2D(_DebugMatCapTexture, s_linear_repeat_sampler, UV);
}