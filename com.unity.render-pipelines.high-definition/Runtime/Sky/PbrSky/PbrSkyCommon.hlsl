#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonLighting.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/VolumeRendering.hlsl"
#include "Packages/com.unity.render-pipelines.high-definition/Runtime/Sky/PbrSky/PbrSkyRenderer.cs.hlsl"

CBUFFER_START(UnityPbrSky)
    // All the distance-related entries use km and 1/km units.
    float  _PlanetaryRadius;
    float  _RcpPlanetaryRadius;
    float  _AtmosphericDepth;
    float  _RcpAtmosphericDepth;

    float  _PlanetaryRadiusSquared;
    float  _AtmosphericRadiusSquared;
    float  _AerosolAnisotropy;
    float  _AerosolPhasePartConstant;

    float  _AirDensityFalloff;
    float  _AirScaleHeight;
    float  _AerosolDensityFalloff;
    float  _AerosolScaleHeight;

    float3 _AirSeaLevelExtinction;
    float  _AerosolSeaLevelExtinction;

    float3 _AirSeaLevelScattering;
    float  _AerosolSeaLevelScattering;

    float3 _GroundAlbedo;

    float3 _PlanetCenterPosition;

    float3 _SunRadiance;  // TODO: isn't that just a global multiplier?
CBUFFER_END

TEXTURE2D(_OpticalDepthTexture);
TEXTURE2D(_GroundIrradianceTexture);
TEXTURE3D(_InScatteredRadianceTexture); // Emulate a 4D texture with a "deep" 3D texture

#ifndef UNITY_SHADER_VARIABLES_INCLUDED
    SAMPLER(s_linear_clamp_sampler);
#endif

float3 AirScatter(float LdotV, float height)
{
    float3 kS = _AirSeaLevelScattering * exp(-height * _AirDensityFalloff);

    return kS * RayleighPhaseFunction(-LdotV);
}

float AerosolScatter(float LdotV, float height)
{
    float kS = _AerosolSeaLevelScattering * exp(-height * _AerosolDensityFalloff);

    return kS * _AerosolPhasePartConstant * CornetteShanksPhasePartVarying(_AerosolAnisotropy, -LdotV);
}

float3 AtmosphereScatter(float LdotV, float height)
{
    return AirScatter(LdotV, height) + AerosolScatter(LdotV, height);
}

// Returns the closest hit in X and the farthest hit in Y.
// Returns a negative number if there's no intersection.
float2 IntersectSphere(float radiusSquared, float cosChi, float radialDistance)
{
    float r  = radialDistance;
    float R2 = radiusSquared;

    // r_o = float2(0, r)
    // r_d = float2(sinChi, cosChi)
    // p_s = r_o + t * r_d
    //
    // R^2 = dot(r_o + t * r_d, r_o + t * r_d)
    // R^2 = ((r_o + t * r_d).x)^2 + ((r_o + t * r_d).y)^2
    // R^2 = t^2 + 2 * dot(r_o, r_d) + dot(r_o, r_o)
    //
    // t^2 + 2 * dot(r_o, r_d) + dot(r_o, r_o) - R^2 = 0
    //
    // Solve: t^2 + (2 * b) * t + c = 0.
    //
    // t = (-2 * b + sqrt((2 * b)^2 - 4 * c)) / 2
    // t = -b + sqrt(b^2 - c)
    // t = -b + sqrt(R^2 - r^2 + (r * cosChi)^2)
    // t = -b + sqrt(R^2 - r^2 * (1 - cosChi)^2)
    // t = -b + sqrt(R^2 - (r * sinChi)^2)
    // t = -b + sqrt(d)

    float b = r * cosChi;
    float d = R2 - ((r * r) * saturate((1 - cosChi * cosChi)));

    return (d >= 0) ? float2(-b - sqrt(d), -b + sqrt(d)) : d;
}

float MapQuadraticHeight(float height)
{
    // TODO: we should adjust sub-texel coordinates
    // to account for the non-linear height distribution.
    return sqrt(height * _RcpAtmosphericDepth);
}

// Returns the height.
float UnmapQuadraticHeight(float v)
{
    return (v * v) * _AtmosphericDepth;
}

float GetCosineOfHorizonZenithAngle(float height)
{
    float R = _PlanetaryRadius;
    float h = height;
    float r = R + h;

    // cos(Pi - x) = -cos(x).
    // Compute -sqrt(r^2 - R^2) / r = -sqrt(1 - (R / r)^2).
    return -sqrt(saturate(1 - Sq(R * rcp(r))));
}

// We use the parametrization from "Outdoor Light Scattering Sample Update" by E. Yusov.
float2 MapAerialPerspective(float cosChi, float height, float texelSize)
{
    float cosHor = GetCosineOfHorizonZenithAngle(height);

    // Above horizon?
    float s = FastSign(cosChi - cosHor);

    float x = (cosChi - cosHor) * rcp(1 - s * cosHor); // in [-1, 1]
    // The pow(u, 0.25) will allocate most samples near the horizon.
    float m = pow(abs(x), 0.25);

    // Lighting must be discontinuous across the horizon.
    // Thus, we offset by half a texel to avoid interpolation artifacts.
    m = s * max(m, texelSize);

    float u = saturate(m * 0.5 + 0.5);
    float v = MapQuadraticHeight(height);

    return float2(u, v);
}

float2 MapAerialPerspectiveAboveHorizon(float cosChi, float height)
{
    float cosHor = GetCosineOfHorizonZenithAngle(height);

    float x = (cosChi - cosHor) * rcp(1 - cosHor);
    float u = pow(saturate(x), 0.25);
    float v = MapQuadraticHeight(height);

    return float2(u, v);
}

// returns {cosChi, height}.
float2 UnmapAerialPerspective(float2 uv)
{
    float height = UnmapQuadraticHeight(uv.y);
    float cosHor = GetCosineOfHorizonZenithAngle(height);

    float m = uv.x * 2 - 1;
    float s = FastSign(m);
    float x = s * (m * m) * (m * m);

    float cosChi = x * (1 - s * cosHor) + cosHor;

    return float2(cosChi, height);
}

float2 UnmapAerialPerspectiveAboveHorizon(float2 uv)
{
    float height = UnmapQuadraticHeight(uv.y);
    float cosHor = GetCosineOfHorizonZenithAngle(height);

    float x = (uv.x * uv.x) * (uv.x * uv.x);

    float cosChi = x * (1 - cosHor) + cosHor;

    return float2(cosChi, height);
}

float3 SampleTransmittanceTexture(float cosChi, float height, bool belowHorizon)
{
    // TODO: pass the sign? Do not recompute?
    float s = belowHorizon ? -1 : 1;

    // From the current position to the atmospheric boundary.
    float2 uv       = MapAerialPerspectiveAboveHorizon(s * cosChi, height).xy;
    float2 optDepth = SAMPLE_TEXTURE2D_LOD(_OpticalDepthTexture, s_linear_clamp_sampler, uv, 0).xy;

    if (belowHorizon)
    {
        // Direction points below the horizon.
        // What we want to know is transmittance from the sea level to our current position.
        // Therefore, first, we must flip the direction and perform the look-up from the ground.
        // The direction must be parametrized w.r.t. the normal of the intersection point.
        // This value corresponds to transmittance from the sea level to the atmospheric boundary.
        // If we perform a look-up from the current position (using the reversed direction),
        // we can compute transmittance from the current position to the atmospheric boundary.
        // Taking the difference will give us the desired value.

        float R    = _PlanetaryRadius;
        float rcpR = _RcpPlanetaryRadius;
        float h    = height;
        float r    = R + h;
        float x    = r * rcpR;

        // Using the Law of Sines (and remembering that the angle is obtuse),
        //
        // sin(beta) / b = sin(gamma) / c
        // sin(beta) = sin(Pi - chi) = sin(chi)
        // sin(gamma) = sin(beta) * c / b
        // sin(gamma) = sin(chi)  * r / R
        // cos(theta) = cos(Pi - gamma) = -cos(gamma), where 'gamma' is obtuse, so cos(theta) >= 0.
        //
        float sinChi   = SinFromCos(cosChi);
        float sinGamma = x * sinChi;
        float cosTheta = sqrt(saturate(1 - sinGamma * sinGamma));

        // From the sea level to the atmospheric boundary -
        // from the current position to the atmospheric boundary.
        uv       = MapAerialPerspectiveAboveHorizon(cosTheta, 0).xy;
        optDepth = SAMPLE_TEXTURE2D_LOD(_OpticalDepthTexture, s_linear_clamp_sampler, uv, 0).xy
                 - optDepth;
    }

    // Compose the optical depth with extinction at the sea level.
    return TransmittanceFromOpticalDepth(optDepth.x * _AirSeaLevelExtinction +
                                         optDepth.y * _AerosolSeaLevelExtinction);
}

float3 SampleTransmittanceTexture(float cosChi, float height)
{
    float cosHor       = GetCosineOfHorizonZenithAngle(height);
    bool  belowHorizon = cosChi < cosHor;

    return SampleTransmittanceTexture(cosChi, height, belowHorizon);
}

// Map: [-0.1975, 1] -> [0, 1].
float MapCosineOfZenithAngle(float NdotL)
{
    // Clamp to around 101 degrees. Seems arbitrary?
    float x = max(NdotL, -0.1975);
    return 0.5 * (atan(x * 5.34962350) * rcp(1.1) + (1 - 0.26));
}

// Map: [0, 1] -> [-0.1975, 1].
float UnmapCosineOfZenithAngle(float u)
{
    return -0.186929 * tan(0.814 - 2.2 * u);
}

float3 SampleGroundIrradianceTexture(float NdotL)
{
    float2 uv = float2(MapCosineOfZenithAngle(NdotL), 0);

    return SAMPLE_TEXTURE2D_LOD(_GroundIrradianceTexture, s_linear_clamp_sampler, uv, 0).rgb;
}
