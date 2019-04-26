using System.Collections.Generic;

namespace UnityEngine.Experimental.VoxelizedShadows
{
    [ExecuteAlways]
    [AddComponentMenu("Rendering/VxShadowMaps/DirectionalVxShadowMap", 100)]
    public sealed class DirectionalVxShadowMap : VxShadowMap
    {
        public float VolumeScale = 10.0f;
        public VoxelResolution VoxelResolution = VoxelResolution._4096;
        public override int VoxelResolutionInt => (int)VoxelResolution;

        private int _index = 0;
        private uint _beginOffset =>
            DataList.Count > _index ?
            DataList[_index].BeginOffset : 0;

        public override int index
        {
            get
            {
                return _index;
            }
            set
            {
                if (_index == value)
                    return;

                int maxCount = Mathf.Max(0, DataList.Count - 1);
                _index = Mathf.Clamp(value, 0, maxCount);

                if (_index < DataList.Count)
                {
                    var vxsData = DataList[_index];
                    gameObject.transform.position = vxsData.Position;
                    gameObject.transform.rotation = vxsData.Rotation;

                    // todo : remove this later
                    //Debug.Log("BeginOffset: " + DataList[_index].BeginOffset);
                }
            }
        }
        public override uint bitset
        {
            get
            {
                bool isOnlyVxsm = ShadowsBlend == ShadowsBlendMode.OnlyVxShadows;

                uint bitBlendMode = isOnlyVxsm ? (uint)0x80000000 : (uint)0x40000000;
                uint bitBeginOffset = _beginOffset & 0x3FFFFFFF;

                return IsValid() ? (bitBlendMode | bitBeginOffset) : 0;
            }
        }

        public override void ResetData()
        {
            _index = -1;
            DataList.Clear();
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
    }
}
