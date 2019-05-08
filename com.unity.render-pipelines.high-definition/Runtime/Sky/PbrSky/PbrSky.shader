Shader "Hidden/HDRP/Sky/PbrSky"
{
    HLSLINCLUDE

    #pragma vertex Vert

    #pragma enable_d3d11_debug_symbols
    #pragma target 4.5
    #pragma only_renderers d3d11 ps4 xboxone vulkan metal switch

    #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
    #include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"
    #include "Packages/com.unity.render-pipelines.high-definition/Runtime/Sky/PbrSky/PbrSkyCommon.hlsl"
    #include "Packages/com.unity.render-pipelines.high-definition/Runtime/Sky/SkyUtils.hlsl"

    float4x4 _PixelCoordToViewDirWS; // Actually just 3x3, but Unity can only set 4x4
    float3   _SunDirection;

    struct Attributes
    {
        uint vertexID : SV_VertexID;
        UNITY_VERTEX_INPUT_INSTANCE_ID
    };

    struct Varyings
    {
        float4 positionCS : SV_POSITION;
        UNITY_VERTEX_OUTPUT_STEREO
    };

    Varyings Vert(Attributes input)
    {
        Varyings output;
        UNITY_SETUP_INSTANCE_ID(input);
        UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
        output.positionCS = GetFullScreenTriangleVertexPosition(input.vertexID, UNITY_RAW_FAR_CLIP_VALUE);
        return output;
    }

    float4 RenderSky(Varyings input)
    {
        // Convention:
        // V points towards the camera.
        // The normal vector N points upwards (local Z).
        // The view vector V and the normal vector N span the local X-Z plane.
        // The light vector is represented as {phiL, cosThataL}.
        float3 L = _SunDirection;
        float3 V = GetSkyViewDirWS(input.positionCS.xy, (float3x3)_PixelCoordToViewDirWS);
        float3 O = _WorldSpaceCameraPos * 0.001; // Convert m to km
        float3 C = _PlanetCenterPosition;
        float3 P = O - C;
        float3 N = normalize(P);
        float  r = length(P);
        float  h = max(0, r - _PlanetaryRadius); // Must not be inside the planet

        bool earlyOut = false;

        if (h <= _AtmosphericDepth)
        {
            // We are inside the atmosphere.
        }
        else
        {
            // We are observing the planet from space.
            float _AtmosphericRadius = _PlanetaryRadius + _AtmosphericDepth;
            float t = IntersectSphere(_AtmosphericRadius, dot(N, -V), r).x; // Min root

            if (t >= 0)
            {
                // It's in the view.
                P = P + t * -V;
                N = normalize(P);
                h = _AtmosphericDepth;
                r = h + _PlanetaryRadius;
            }
            else
            {
                // No atmosphere along the ray.
                earlyOut = true;
            }
        }


        float  NdotL = dot(N, L);
        float  NdotV = dot(N, V);
        float3 projL = L - N * NdotL;
        float3 projV = V - N * NdotV;
        float  phiL  = acos(clamp(dot(projL, projV) * rsqrt(max(dot(projL, projL) * dot(projV, projV), FLT_EPS)), -1, 1));

        TexCoord4D tc = ConvertPositionAndOrientationToTexCoords(h, NdotV, NdotL, phiL);

        float cosChi = -NdotV;
        float cosHor = GetCosineOfHorizonZenithAngle(h);

        bool lookAboveHorizon = (cosChi > cosHor);

        float3 radiance = 0;

        if (!lookAboveHorizon) // See the ground?
        {
            float  t  = IntersectSphere(_PlanetaryRadius, cosChi, r).x;
            float3 gP = P + t * -V;
            float3 gN = normalize(gP);

            // Shade the ground.
            const float3 gBrdf = INV_PI * _GroundAlbedo;
            float3 transm = SampleTransmittanceTexture(cosChi, h, true);
            radiance += transm * gBrdf * SampleGroundIrradianceTexture(dot(gN, L));
        }


        // Apply the phase function.
        float LdotV = dot(L, V);

        radiance += lerp(SAMPLE_TEXTURE3D(_AirSingleScatteringTexture,     s_linear_clamp_sampler, float3(tc.u, tc.v, tc.w0)),
                         SAMPLE_TEXTURE3D(_AirSingleScatteringTexture,     s_linear_clamp_sampler, float3(tc.u, tc.v, tc.w1)),
                         tc.a).rgb * AirPhase(LdotV);

        radiance += lerp(SAMPLE_TEXTURE3D(_AerosolSingleScatteringTexture, s_linear_clamp_sampler, float3(tc.u, tc.v, tc.w0)),
                         SAMPLE_TEXTURE3D(_AerosolSingleScatteringTexture, s_linear_clamp_sampler, float3(tc.u, tc.v, tc.w1)),
                         tc.a).rgb * AerosolPhase(LdotV);

        if (earlyOut)
        {
            // Can't perform an early return at the beginning of the shader
            // due to the compiler warning...
            radiance = 0;
        }

        return float4(radiance, 1.0);
    }

    float4 FragBaking(Varyings input) : SV_Target
    {
        return RenderSky(input);
    }

    float4 FragRender(Varyings input) : SV_Target
    {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
        float4 color = RenderSky(input);
        color.rgb *= GetCurrentExposureMultiplier();
        return color;
    }

    ENDHLSL

    SubShader
    {
        Pass
        {
            ZWrite Off
            ZTest Always
            Blend Off
            Cull Off

            HLSLPROGRAM
                #pragma fragment FragBaking
            ENDHLSL

        }

        Pass
        {
            ZWrite Off
            ZTest LEqual
            Blend Off
            Cull Off

            HLSLPROGRAM
                #pragma fragment FragRender
            ENDHLSL
        }

    }
    Fallback Off
}
