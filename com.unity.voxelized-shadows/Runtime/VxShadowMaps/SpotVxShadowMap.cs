
namespace UnityEngine.Experimental.VoxelizedShadows
{
    [ExecuteAlways]
    [AddComponentMenu("Rendering/VxShadowMaps/SpotVxShadowMap", 120)]
    public sealed class SpotVxShadowMap : VxShadowMap
    {
        // TODO :
        public override int voxelResolutionInt => (int)VoxelResolution._4096;
        public override VoxelResolution subtreeResolution => VoxelResolution._4096;

        public override bool IsValid()
        {
            return false;
        }
    }
}
