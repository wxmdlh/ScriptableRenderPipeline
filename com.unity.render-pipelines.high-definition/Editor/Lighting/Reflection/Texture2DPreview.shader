Shader "Debug/Texture2DPreview"
{
    Properties
    {
        _MainTex("_MainTex", Any) = "white" {}
        _MipLevel("_MipLevel", Range(0.0, 7.0)) = 0.0
        _Exposure("_Exposure", Range(-10.0, 10.0)) = 0.0
    }

    HLSLINCLUDE

    #pragma editor_sync_compilation

    #pragma vertex vert
    #pragma fragment frag

    #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
    #include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"

    struct appdata
    {
        float4 vertex : POSITION;
        float2 texcoord0 : TEXCOORD0;
    };

    struct v2f
    {
        float4 vertex : SV_POSITION;
        float2 texcoord0 : TEXCOORD0;
    };

    TEXTURE2D(_MainTex);

    float _MipLevel;
    float _Exposure;

    v2f vert(appdata v)
    {
        v2f o;

        // Transform local to world before custom vertex code
        o.vertex = TransformObjectToHClip(v.vertex.xyz);
        o.texcoord0 = v.texcoord0;

        return o;
    }

    float4 frag(v2f i) : SV_Target
    {
        float4 color = SAMPLE_TEXTURE2D_LOD(_MainTex, s_trilinear_clamp_sampler, i.texcoord0, _MipLevel);
        color.rgb = color.rgb * exp2(_Exposure) * GetCurrentExposureMultiplier();

        return color;
    }
    ENDHLSL

    SubShader {
        Lighting Off
        Blend SrcAlpha OneMinusSrcAlpha, One One
        Cull Off
        ZWrite Off
        ZTest Always

        Pass {
            HLSLPROGRAM
            ENDHLSL
        }
    }

    SubShader {
        Lighting Off
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off
        ZTest Always

        Pass {
            HLSLPROGRAM
            ENDHLSL
        }
    }
}
