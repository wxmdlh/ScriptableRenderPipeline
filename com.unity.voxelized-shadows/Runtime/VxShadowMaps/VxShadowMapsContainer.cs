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
            VxShadowMapsManager.Instance.RegisterVxShadowMapsContainer(this);
        }

        private void OnDisable()
        {
            VxShadowMapsManager.Instance.UnregisterVxShadowMapsContainer(this);
        }

        private void OnValidate()
        {
            VerifyResources();
        }

        private void ValidateResources()
        {
            VxShadowMapsManager.Instance.LoadResources(Resources);
            Size = (float)VxShadowMapsManager.Instance.GetSizeInBytes() / (1024.0f * 1024.0f);
        }
        private void InvalidateResources()
        {
            VxShadowMapsManager.Instance.UnloadResources();
            Size = 0.0f;
        }

        public void VerifyResources()
        {
            if (Resources != null)
                ValidateResources();
            else
                InvalidateResources();
        }
    }
}
