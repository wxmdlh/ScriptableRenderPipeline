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
            ValidateResources();
        }

        private void OnDisable()
        {
            InvalidateResources();
        }

        private void ValidateResources()
        {
            if (Resources != null)
            {
                VxShadowMapsManager.instance.RegisterVxShadowMapsContainer(this);
                VxShadowMapsManager.instance.LoadResources(Resources);
                Size = (float)VxShadowMapsManager.instance.GetSizeInBytes() / (1024.0f * 1024.0f);
            }
            else
            {
                VxShadowMapsManager.instance.Unloadresources();
                Size = 0.0f;
            }
        }
        private void InvalidateResources()
        {
            VxShadowMapsManager.instance.UnregisterVxShadowMapsContainer(this);
            VxShadowMapsManager.instance.Unloadresources();
            Size = 0.0f;
        }
    }
}
