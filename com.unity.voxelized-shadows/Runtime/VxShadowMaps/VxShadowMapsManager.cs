using System.Collections.Generic;
using UnityEngine.Rendering;

namespace UnityEngine.Experimental.VoxelizedShadows
{
    public enum RenderPipelineType
    {
        Lightweight,
        HighDefinition,
        Unknown,
    }

    public class VxShadowMapsManager
    {
        private RenderPipelineType _renderPipelineType = RenderPipelineType.Unknown;

        private VxShadowMapsContainer _container = null;

        private List<DirectionalVxShadowMap> _dirVxShadowMapList = new List<DirectionalVxShadowMap>();
        private List<PointVxShadowMap> _pointVxShadowMapList = new List<PointVxShadowMap>();
        private List<SpotVxShadowMap> _spotVxShadowMapList = new List<SpotVxShadowMap>();

        private ComputeBuffer _vxShadowMapsNullBuffer = null;
        private ComputeBuffer _vxShadowMapsBuffer = null;

        private static VxShadowMapsManager _instance = null;
        public static VxShadowMapsManager Instance
        {
            get
            {
                if (_instance == null)
                    _instance = new VxShadowMapsManager();

                return _instance;
            }
        }

        public VxShadowMapsManager()
        {
            string renderPipelineTypeString = "";
            if (GraphicsSettings.renderPipelineAsset != null)
                renderPipelineTypeString = GraphicsSettings.renderPipelineAsset.GetType().ToString();

            if (renderPipelineTypeString.Contains("LightweightRenderPipelineAsset"))
                _renderPipelineType = RenderPipelineType.Lightweight;
            else if (renderPipelineTypeString.Contains("HDRenderPipelineAsset"))
                _renderPipelineType = RenderPipelineType.HighDefinition;
            else
                _renderPipelineType = RenderPipelineType.Unknown;
        }

        private void InstantiateNullVxShadowMapsBuffer()
        {
            uint[] nullData = new uint[]
            {
                // type, volumeScale, dagScale
                0, 0, 0,
                // matrix
                0, 0, 0, 0,
                0, 0, 0, 0,
                0, 0, 0, 0,
                // data
                0,
            };

            _vxShadowMapsNullBuffer = new ComputeBuffer(nullData.Length, 4);
            _vxShadowMapsNullBuffer.SetData(nullData);
        }

        public bool RegisterVxShadowMapsContainer(VxShadowMapsContainer container)
        {
            if (_container != null)
            {
                Debug.LogError("Failed to register container, VxShadowMapsContainer must be single one.");
                return false;
            }

            //Debug.Log("Try to register VxShadowMapsContainer");
            _container = container;

            return true;
        }
        public bool UnregisterVxShadowMapsContainer(VxShadowMapsContainer container)
        {
            if (_container != container)
            {
                Debug.LogError("Failed to unregister container, Are there VxShadowMapsContainers more than one?");
                return false;
            }

            //Debug.Log("Try to unregister VxShadowMapsContainer");
            _container = null;

            return true;
        }

        public void RegisterVxShadowMapComponent(DirectionalVxShadowMap dirVxsm)
        {
#if UNITY_EDITOR
            if (_renderPipelineType == RenderPipelineType.Unknown)
            {
                Debug.LogWarning("Try to register VxShadowMap on 'unknown RenderPipeline', it may not work.");
            }
#endif
            if (_dirVxShadowMapList.Contains(dirVxsm))
            {
                Debug.LogError("'" + dirVxsm.gameObject.name + "' is already registered. try to register duplicate vxsm!!");
            }
            else
            {
                _dirVxShadowMapList.Add(dirVxsm);
            }
        }
        public void RegisterVxShadowMapComponent(PointVxShadowMap pointVxsm)
        {
#if UNITY_EDITOR
            if (_renderPipelineType == RenderPipelineType.Unknown)
            {
                Debug.LogWarning("Try to register VxShadowMap on 'unknown RenderPipeline', it may not work.");
            }
            else if (_renderPipelineType == RenderPipelineType.Lightweight)
            {
                Debug.LogWarning("VxShadowMap of PointLight is not supported on 'Lightweight RenderPipeline'.");
                return;
            }
#endif
            if (_pointVxShadowMapList.Contains(pointVxsm))
            {
                Debug.LogError("'" + pointVxsm.gameObject.name + "' is already registered. try to register duplicate vxsm!!");
            }
            else
            {
                _pointVxShadowMapList.Add(pointVxsm);
            }
        }
        public void RegisterVxShadowMapComponent(SpotVxShadowMap spotVxsm)
        {
#if UNITY_EDITOR
            if (_renderPipelineType == RenderPipelineType.Unknown)
            {
                Debug.LogWarning("Try to register VxShadowMap on 'unknown RenderPipeline', it may not work.");
            }
            else if (_renderPipelineType == RenderPipelineType.Lightweight)
            {
                Debug.LogWarning("VxShadowMap of SpotLight is not supported on 'Lightweight RenderPipeline'.");
                return;
            }
#endif
            if (_spotVxShadowMapList.Contains(spotVxsm))
            {
                Debug.LogError("'" + spotVxsm.gameObject.name + "' is already registered. try to register duplicate vxsm!!");
            }
            else
            {
                _spotVxShadowMapList.Add(spotVxsm);
            }
        }
        public void UnregisterVxShadowMapComponent(DirectionalVxShadowMap dirVxsm)
        {
            if (_dirVxShadowMapList.Contains(dirVxsm))
                _dirVxShadowMapList.Remove(dirVxsm);
        }
        public void UnregisterVxShadowMapComponent(PointVxShadowMap pointVxsm)
        {
            if (_pointVxShadowMapList.Contains(pointVxsm))
                _pointVxShadowMapList.Remove(pointVxsm);
        }
        public void UnregisterVxShadowMapComponent(SpotVxShadowMap spotVxsm)
        {
            if (_spotVxShadowMapList.Contains(spotVxsm))
                _spotVxShadowMapList.Remove(spotVxsm);
        }

        public void Build()
        {
            var dirVxShadowMaps   = Object.FindObjectsOfType<DirectionalVxShadowMap>();
            var pointVxShadowMaps = Object.FindObjectsOfType<PointVxShadowMap>();
            var spotVxShadowMaps  = Object.FindObjectsOfType<SpotVxShadowMap>();

            foreach (var vxsm in dirVxShadowMaps)
            {
                if (vxsm.enabled && _dirVxShadowMapList.Contains(vxsm) == false)
                    _dirVxShadowMapList.Add(vxsm);
            }
            foreach (var vxsm in pointVxShadowMaps)
            {
                if (vxsm.enabled && _pointVxShadowMapList.Contains(vxsm) == false)
                    _pointVxShadowMapList.Add(vxsm);
            }
            foreach (var vxsm in spotVxShadowMaps)
            {
                if (vxsm.enabled && _spotVxShadowMapList.Contains(vxsm) == false)
                    _spotVxShadowMapList.Add(vxsm);
            }
        }
        public void Cleanup()
        {
            if (_vxShadowMapsNullBuffer != null)
            {
                _vxShadowMapsNullBuffer.Release();
                _vxShadowMapsNullBuffer = null;
            }

            _dirVxShadowMapList.Clear();
            _pointVxShadowMapList.Clear();
            _spotVxShadowMapList.Clear();
        }

        public void LoadResources(VxShadowMapsResources resources)
        {
            foreach (var vxsm in _dirVxShadowMapList)
                vxsm.ResetData();
            foreach (var vxsm in _pointVxShadowMapList)
                vxsm.ResetData();
            foreach (var vxsm in _spotVxShadowMapList)
                vxsm.ResetData();

            for (int i = 0; i < resources.VxShadowsDataList.Length; i++)
            {
                var vxsData = resources.VxShadowsDataList[i];
                var vxsm = FindVxShadowMap(vxsData.Type, vxsData.InstanceId);

                if (vxsm != null)
                {
                    vxsm.DataList.Add(vxsData);
                    vxsm.index = 0;

                    // todo : remove this later
                    //Debug.Log(vxsData.SizeInBytes + "bytes of vxsData added");
                }
            }

            int count = resources.VxShadowMapList.Length;
            int stride = 4;

            if (_vxShadowMapsBuffer != null)
                _vxShadowMapsBuffer.Release();

            _vxShadowMapsBuffer = new ComputeBuffer(count, stride);
            _vxShadowMapsBuffer.SetData(resources.VxShadowMapList);

            // todo : deallocate resources.VxShadowMapList?
        }
        public void UnloadResources()
        {
            foreach (var vxsm in _dirVxShadowMapList)
                vxsm.ResetData();
            foreach (var vxsm in _pointVxShadowMapList)
                vxsm.ResetData();
            foreach (var vxsm in _spotVxShadowMapList)
                vxsm.ResetData();

            if (_vxShadowMapsBuffer != null)
            {
                _vxShadowMapsBuffer.Release();
                _vxShadowMapsBuffer = null;
            }
        }
        public uint GetSizeInBytes()
        {
            return _vxShadowMapsBuffer != null ? (uint)_vxShadowMapsBuffer.count * 4 : 0;
        }

        private DirectionalVxShadowMap FindDirVxShadowMap(int instanceId)
        {
            foreach (var vxsm in _dirVxShadowMapList)
                if (vxsm.GetInstanceID() == instanceId)
                    return vxsm;

            return null;
        }
        private PointVxShadowMap FindPointVxShadowMap(int instanceId)
        {
            foreach (var vxsm in _pointVxShadowMapList)
                if (vxsm.GetInstanceID() == instanceId)
                    return vxsm;

            return null;
        }
        private SpotVxShadowMap FindSpotVxShadowMap(int instanceId)
        {
            foreach (var vxsm in _spotVxShadowMapList)
                if (vxsm.GetInstanceID() == instanceId)
                    return vxsm;

            return null;
        }
        private VxShadowMap FindVxShadowMap(VxShadowsType type, int instanceId)
        {
            switch (type)
            {
                case VxShadowsType.Directional: return FindDirVxShadowMap(instanceId);
                case VxShadowsType.Point:       return FindPointVxShadowMap(instanceId);
                case VxShadowsType.Spot:        return FindSpotVxShadowMap(instanceId);
            }

            return null;
        }

        public VxShadowMapsContainer Container { get { return _container; } }

        public List<DirectionalVxShadowMap> DirVxShadowMaps { get { return _dirVxShadowMapList; } }
        public List<PointVxShadowMap> PointVxShadowMaps { get { return _pointVxShadowMapList; } }
        public List<SpotVxShadowMap> SpotVxShadowMaps { get { return _spotVxShadowMapList; } }

        public ComputeBuffer VxShadowMapsNullBuffer
        {
            get
            {
                if (_vxShadowMapsNullBuffer == null)
                    InstantiateNullVxShadowMapsBuffer();

                return _vxShadowMapsNullBuffer;
            }
        }

        public ComputeBuffer VxShadowMapsBuffer
        {
            get
            {
                return _vxShadowMapsBuffer != null ? _vxShadowMapsBuffer : VxShadowMapsNullBuffer;
            }
        }

        public bool ValidVxShadowMapsBuffer
        {
            get
            {
                return _vxShadowMapsBuffer != null;
            }
        }
    }
}
