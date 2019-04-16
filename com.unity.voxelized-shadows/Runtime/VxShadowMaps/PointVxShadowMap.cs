
namespace UnityEngine.Experimental.VoxelizedShadows
{
    [ExecuteAlways]
    [AddComponentMenu("Rendering/VxShadowMaps/PointVxShadowMap", 110)]
    public sealed class PointVxShadowMap : VxShadowMap
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
