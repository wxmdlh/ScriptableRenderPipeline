using System;
using System.Runtime.Serialization;

namespace UnityEngine.Experimental.VoxelizedShadows
{
    public enum VxShadowsLightType
    {
        Directional = 0,
        Spot,
        Point,
    }

    [Serializable]
    public struct VxShadowsLight
    {
        public VxShadowsLightType Type;
        public Vector3 Position;
        public Quaternion Rotation;
        public uint SizeInBytes;
    }

    public class VxShadowMapsResources : ScriptableObject
    {
        [HideInInspector] public VxShadowsLight[] Table;
        [HideInInspector] public uint[] Vxsms;
    }
}
