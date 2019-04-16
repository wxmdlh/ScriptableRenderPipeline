
namespace UnityEngine.Experimental.VoxelizedShadows
{
    [ExecuteAlways]
    [AddComponentMenu("Rendering/VxShadowMaps/DirectionalVxShadowMap", 100)]
    public sealed class DirectionalVxShadowMap : VxShadowMap
    {
        public float volumeScale = 10.0f;
        public VoxelResolution voxelResolution = VoxelResolution._4096;
        public override int voxelResolutionInt => (int)voxelResolution;
        public override VoxelResolution subtreeResolution =>
            voxelResolutionInt < MaxSubtreeResolutionInt ? voxelResolution : MaxSubtreeResolution;

        private void OnEnable()
        {
            VxShadowMapsManager.instance.RegisterVxShadowMapComponent(this);
        }
        private void OnDisable()
        {
            VxShadowMapsManager.instance.UnregisterVxShadowMapComponent(this);
        }

        public override bool IsValid()
        {
            return VxShadowMapsManager.instance.ValidVxShadowMapsBuffer;
        }

#if UNITY_EDITOR
        public void OnDrawGizmos()
        {
            var light = GetComponent<Light>();

            if (light != null)
            {
                Gizmos.color = new Color(1.0f, 0.0f, 0.0f, 0.3f);
                Gizmos.matrix = light.transform.localToWorldMatrix;
                Gizmos.DrawCube(Vector3.zero, new Vector3(volumeScale, volumeScale, volumeScale));
            }
        }
#endif
    }
}
