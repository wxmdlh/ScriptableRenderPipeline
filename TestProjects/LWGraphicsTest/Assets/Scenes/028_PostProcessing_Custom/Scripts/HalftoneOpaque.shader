Shader "Hidden/Custom/LWtest/HalftoneOpaque"
{
	Properties
	{
		_Pattern ("Pattern", 2D) = "grey" {}
		[IntRange]_Steps ("Steps", Range(1, 10)) = 4
	}
	HLSLINCLUDE

        #include "Packages/com.unity.postprocessing/PostProcessing/Shaders/StdLib.hlsl"
        #include "Packages/com.unity.postprocessing/PostProcessing/Shaders/Colors.hlsl"

        TEXTURE2D_SAMPLER2D(_MainTex, sampler_MainTex);
        TEXTURE2D_SAMPLER2D(_Pattern, sampler_Pattern_linear_repeat);
        float _Blend;
        float _Scale;
        float _Steps;

        float4 Frag(VaryingsDefault i) : SV_Target
        {
            float4 color = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.texcoord);
            float3 hsv = RgbToHsv(color.rgb);
            float pattern = SAMPLE_TEXTURE2D(_Pattern, sampler_Pattern_linear_repeat, i.texcoord * (_ScreenParams.xy * _Scale * (0.9 + hsv.z * 0.2))).r;
            hsv.z = round((pattern - _Blend) + (hsv.z * (_Steps - 1))) / _Steps;
            color.rgb = HsvToRgb(hsv);
            return color;
        }

    ENDHLSL

    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            HLSLPROGRAM

                #pragma vertex VertDefault
                #pragma fragment Frag

            ENDHLSL
        }
    }
}
