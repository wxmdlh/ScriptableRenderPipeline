using UnityEngine;

namespace UnityEngine.Experimental.VoxelizedShadows
{
    [ExecuteAlways]
    [AddComponentMenu("Rendering/VxShadowMaps/VxShadowMapsContainer", 100)]
    public class VxShadowMapsContainer : MonoBehaviour
    {
        public VxShadowMapsResources Resources = null;
        public float Size = 0;

        private void OnEnable()
        {
            VxShadowMapsManager.instance.RegisterVxShadowMapsContainer(this);
        }

        private void OnDisable()
        {
            VxShadowMapsManager.instance.UnregisterVxShadowMapsContainer(this);
        }

        private void OnValidate()
        {
            if (Resources != null)
                ValidateResources();
            else
                InvalidateResources();
        }

        private void ValidateResources()
        {
            VxShadowMapsManager.instance.LoadResources(Resources);
            Size = (float)VxShadowMapsManager.instance.GetSizeInBytes() / (1024.0f * 1024.0f);
        }
        private void InvalidateResources()
        {
            VxShadowMapsManager.instance.Unloadresources();
            Size = 0.0f;
        }
    }
}
