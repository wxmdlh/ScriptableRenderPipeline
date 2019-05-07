using UnityEngine.Rendering;

namespace UnityEngine.Experimental.Rendering.HDPipeline
{
    [SkyUniqueID((int)SkyType.PBR)]
    public class PbrSkySettings : SkySettings
    {
        /* We use the measurements from Earth as the defaults. */
        // Radius of the planet (distance from the core to the sea level). Units: km.
        public MinFloatParameter planetaryRadius = new MinFloatParameter(6378.759f, 0);
        // Position of the center of the planet in the world space.
        public Vector3Parameter planetCenterPosition = new Vector3Parameter(new Vector3(0, -6378.759f, 0));
        // Extinction coefficient of air molecules at the sea level. Units: 1/(1000 km).
        // TODO: use mean free path?
        public ColorParameter airThickness = new ColorParameter(new Color(5.8f, 13.5f, 33.1f), hdr: true, showAlpha: false, showEyeDropper: false);
        // Single scattering albedo of air molecules.
        // It is the ratio between the scattering and the extinction coefficients.
        // The value of 0 results in absorbing molecules, and the value of 1 results in scattering ones.
        // Note: this allows us to account for absorption due to the ozone layer.
        // We assume that ozone has the same height distribution as air (CITATION NEEDED!).
        public ColorParameter airAlbedo = new ColorParameter(new Color(0.9f, 0.9f, 1.0f), hdr: false, showAlpha: false, showEyeDropper: false);
        // Exponential falloff of air density w.r.t. height. (Rayleigh). Units: 1/km.
        public ClampedFloatParameter airDensityFalloff = new ClampedFloatParameter(1.0f / 8.434f, 0, 1);
        // Extinction coefficient of aerosol molecules at the sea level. Units: 1/(1000 km).
        // Note: aerosols are (fairly large) solid or liquid particles in the air.
        // TODO: use mean free path?
        public MinFloatParameter aerosolThickness = new MinFloatParameter(2.0f, 0);
        // Single scattering albedo of aerosol molecules.
        // It is the ratio between the scattering and the extinction coefficients.
        // The value of 0 results in absorbing molecules, and the value of 1 results in scattering ones.
        public ClampedFloatParameter aerosolAlbedo = new ClampedFloatParameter(0.9f, 0, 1);
        // Exponential falloff of aerosol density w.r.t. height. (Mie). Units: 1/km.
        public ClampedFloatParameter aerosolDensityFalloff = new ClampedFloatParameter(1.0f / 1.2f, 0, 1);
        // +1: forward  scattering.
        //  0: almost isotropic.
        // -1: backward scattering.
        public ClampedFloatParameter aerosolAnisotropy = new ClampedFloatParameter(0, -1, 1);
        // Albedo of the planetary surface.
        public ColorParameter groundColor = new ColorParameter(new Color(1, 1, 1), hdr: false, showAlpha: false, showEyeDropper: false);

        /* Properties below should be interpolated, but they should not be accessible via the GUI. */

        // Height of all the atmospheric layers starting from the sea level. Units: km.
        public FloatParameter atmosphericDepth { get; set; }

        public Vector3Parameter sunRadiance { get; set; } // TODO: isn't that just a global multiplier?

        public void Awake()
        {
            // Allocate memory on startup.
            atmosphericDepth = new FloatParameter(0.0f);
            sunRadiance      = new Vector3Parameter(Vector3.zero);
        }

        float ComputeAtmosphericDepth()
        {
            // What's the thickness at the boundary of the outer space (units: 1/(1000 km))?
            const float outerThickness = 0.01f;

            // Using this thickness threshold, we can automatically determine the atmospheric range
            // for user-provided values.
            float R          = planetaryRadius;
            float airN       = airDensityFalloff;
            float airH       = 1.0f / airN;
            float airRho     = Mathf.Max(airThickness.value.r, airThickness.value.g, airThickness.value.b);
            float airLim     = -airH * Mathf.Log(outerThickness / airRho, 2.71828183f);
            float aerosolN   = aerosolDensityFalloff;
            float aerosolH   = 1.0f / aerosolN;
            float aerosolRho = aerosolThickness;
            float aerosolLim = -aerosolH * Mathf.Log(outerThickness / aerosolRho, 2.71828183f);

            // Both are stored in the same texture, so we have to fit both density profiles.
            return Mathf.Max(airLim, aerosolLim);
        }

        public void UpdateParameters(BuiltinSkyParameters builtinParams)
        {
            atmosphericDepth.value = ComputeAtmosphericDepth();

            Light sun = builtinParams.sunLight;

            if (sun != null)
            {
                sunRadiance.value = new Vector3(sun.intensity * sun.color.linear.r,
                                                sun.intensity * sun.color.linear.g,
                                                sun.intensity * sun.color.linear.b);
            }
            else
            {
                sunRadiance.value = Vector3.zero;
            }

//            sunDirection.value     = -sun.transform.forward;
        }

        public override int GetHashCode()
        {
            int hash = base.GetHashCode();

            unchecked
            {
                hash = hash * 23 + planetaryRadius.GetHashCode();
                hash = hash * 23 + planetCenterPosition.GetHashCode();
                hash = hash * 23 + airThickness.GetHashCode();
                hash = hash * 23 + airAlbedo.GetHashCode();
                hash = hash * 23 + airDensityFalloff.GetHashCode();
                hash = hash * 23 + aerosolThickness.GetHashCode();
                hash = hash * 23 + aerosolAlbedo.GetHashCode();
                hash = hash * 23 + aerosolDensityFalloff.GetHashCode();
                hash = hash * 23 + aerosolAnisotropy.GetHashCode();
                hash = hash * 23 + groundColor.GetHashCode();
                hash = hash * 23 + atmosphericDepth.GetHashCode();
                hash = hash * 23 + sunRadiance.GetHashCode();
            }

            return hash;
        }

        public override SkyRenderer CreateRenderer()
        {
            return new PbrSkyRenderer(this);
        }
    }
}
