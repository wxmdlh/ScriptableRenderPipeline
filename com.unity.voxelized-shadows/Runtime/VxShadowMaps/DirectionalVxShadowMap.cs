using System.Collections.Generic;

namespace UnityEngine.Experimental.VoxelizedShadows
{
    [ExecuteAlways]
    [AddComponentMenu("Rendering/VxShadowMaps/DirectionalVxShadowMap", 100)]
    public sealed class DirectionalVxShadowMap : VxShadowMap
    {
        public float volumeScale = 10.0f;
        public VoxelResolution VoxelResolution = VoxelResolution._4096;
        public override int VoxelResolutionInt => (int)VoxelResolution;

        private int _index = 0;
        private uint _beginOffset =>
            VxShadowsDataList.Count > _index ?
            VxShadowsDataList[_index].BeginOffset : 0;

        public override int index
        {
            get
            {
                return _index;
            }
            set
            {
                _index = value;

                var vxsData = VxShadowsDataList[_index];
                gameObject.transform.position = vxsData.Position;
                gameObject.transform.rotation = vxsData.Rotation;
            }
        }
        public override uint bitset
        {
            get
            {
                bool isOnlyVxsm = ShadowsBlend == ShadowsBlendMode.OnlyVxShadows;

                uint bitBlendMode = isOnlyVxsm ? (uint)0x80000000 : (uint)0x40000000;
                uint bitBeginOffset = _beginOffset & 0x3FFFFFFF;

                return IsValid() ? (bitBlendMode | bitBeginOffset) : bitBeginOffset;
            }
        }

        private void OnEnable()
        {
            VxShadowMapsManager.Instance.RegisterVxShadowMapComponent(this);
        }
        private void OnDisable()
        {
            VxShadowMapsManager.Instance.UnregisterVxShadowMapComponent(this);
        }

        public override bool IsValid()
        {
            return
                VxShadowMapsManager.Instance.ValidVxShadowMapsBuffer &&
                VxShadowMapsManager.Instance.Container != null &&
                enabled;
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
