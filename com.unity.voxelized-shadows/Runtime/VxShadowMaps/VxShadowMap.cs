
namespace UnityEngine.Experimental.VoxelizedShadows
{
    public enum VoxelResolution
    {
        _64      = 64,
        _128     = 64 << 1,
        _256     = 64 << 2,
        _512     = 64 << 3,

        _1024    = 1024,       //    1K
        _2048    = 1024 << 1,  //    2K
        _4096    = 1024 << 2,  //    4K
        _8192    = 1024 << 3,  //    8K
        _16384   = 1024 << 4,  //   16K
        _32768   = 1024 << 5,  //   32K
        _65536   = 1024 << 6,  //   64K
        _131072  = 1024 << 7,  //  128K
        _262144  = 1024 << 8,  //  256K
        _524288  = 1024 << 9,  //  512K
        _1048576 = 1024 << 10, // 1024K
    }

    public enum ShadowsBlendMode
    {
        OnlyVxShadowMaps,
        BlendDynamicShadows,
    }

    public abstract class VxShadowMap : MonoBehaviour
    {
        public static VoxelResolution MaxSubtreeResolution => VoxelResolution._4096;
        public static int MaxSubtreeResolutionInt => (int)MaxSubtreeResolution;

        public abstract int voxelResolutionInt { get; }
        public abstract VoxelResolution subtreeResolution { get; }
        public int subtreeResolutionInt { get { return (int)subtreeResolution; } }
        public int index = -1;
        public ShadowsBlendMode shadowsBlendMode = ShadowsBlendMode.OnlyVxShadowMaps;

        public abstract bool IsValid();
    }
}
