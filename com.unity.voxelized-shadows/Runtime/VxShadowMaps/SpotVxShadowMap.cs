using System.Collections.Generic;

namespace UnityEngine.Experimental.VoxelizedShadows
{
    [ExecuteAlways]
    [AddComponentMenu("Rendering/VxShadowMaps/SpotVxShadowMap", 120)]
    public sealed class SpotVxShadowMap : VxShadowMap
    {
        // TODO :
        public override int VoxelResolutionInt => (int)VoxelResolution._4096;

        public override int index { get { return -1; } set { } }
        public override uint bitset => 0;

        public override void ResetData()
        {
            // todo : reset index
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
            return false;
        }
    }
}
