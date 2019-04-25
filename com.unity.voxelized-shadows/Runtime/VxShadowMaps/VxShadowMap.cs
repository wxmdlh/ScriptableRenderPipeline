using System.Collections.Generic;

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
        OnlyVxShadows,
        BlendDynamicShadows,
    }

    public abstract class VxShadowMap : MonoBehaviour
    {
        public static VoxelResolution MaxSubtreeResolution => VoxelResolution._4096;
        public static int MaxSubtreeResolutionInt => (int)MaxSubtreeResolution;
        public static VoxelResolution SubtreeResolution(VoxelResolution res) { return (int)res > MaxSubtreeResolutionInt ? MaxSubtreeResolution : res; }
        public static int SubtreeResolutionInt(int res) { return Mathf.Min(res, MaxSubtreeResolutionInt); }

        public abstract int VoxelResolutionInt { get; }
        public ShadowsBlendMode ShadowsBlend = ShadowsBlendMode.OnlyVxShadows;

        public List<VxShadowsData> DataList = new List<VxShadowsData>();

        public abstract int index { get; set; }
        public abstract uint bitset { get; }

        public abstract void ResetData();

        public abstract bool IsValid();
    }
}
