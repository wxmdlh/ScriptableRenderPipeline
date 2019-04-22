using System.Collections.Generic;

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

        public List<VxShadowsLight> vxShadowsLightList = new List<VxShadowsLight>();

        private uint _beginOffset = 0;

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
            return
                VxShadowMapsManager.instance.ValidVxShadowMapsBuffer &&
                VxShadowMapsManager.instance.Container != null &&
                enabled;
        }

        public override void SetIndex(int index)
        {
            var vxs = vxShadowsLightList[index];
            gameObject.transform.position = vxs.Position;
            gameObject.transform.rotation = vxs.Rotation;
            _beginOffset = vxs.BeginOffset;
        }

        public override uint GetBitset()
        {
            bool isOnlyVxsm = shadowsBlendMode == ShadowsBlendMode.OnlyVxShadowMaps;

            uint bitBlendMode = isOnlyVxsm ? (uint)0x80000000 : (uint)0x40000000;
            uint bitBeginOffset = _beginOffset & 0x3FFFFFFF;

            return IsValid() ? (bitBlendMode | bitBeginOffset) : bitBeginOffset;
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
