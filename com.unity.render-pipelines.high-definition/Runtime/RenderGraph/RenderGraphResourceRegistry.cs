using System;
using System.Collections.Generic;
using UnityEngine.Rendering;

namespace UnityEngine.Experimental.Rendering.RenderGraphModule
{
    using RTHandle = RTHandleSystem.RTHandle;

    #region Resource Descriptors
    public enum TextureSizeMode
    {
        Explicit,
        Scale,
        Functor
    }

    public struct TextureDesc
    {
        public TextureSizeMode sizeMode;
        public int width;
        public int height;
        public int slices;
        public Vector2 scale;
        public ScaleFunc func;
        public DepthBits depthBufferBits;
        public GraphicsFormat colorFormat;
        public FilterMode filterMode;
        public TextureWrapMode wrapMode;
        public TextureDimension dimension;
        public bool enableRandomWrite;
        public bool useMipMap;
        public bool autoGenerateMips;
        public bool isShadowMap;
        public int anisoLevel;
        public float mipMapBias;
        public bool enableMSAA; // Only supported for Scale and Functor size mode
        public MSAASamples msaaSamples; // Only supported for Explicit size mode
        public bool bindTextureMS;
        public bool useDynamicScale;
        public bool xrInstancing;
        public RenderTextureMemoryless memoryless;
        public string name;

        // Initial state. Those should not be used in the hash
        public bool clearBuffer;
        public Color clearColor;

        public TextureDesc(int width, int height)
            : this()
        {
            // Size related init
            sizeMode = TextureSizeMode.Explicit;
            this.width = width;
            this.height = height;
            // Important default values not handled by zero construction in this()
            slices = 1;
            msaaSamples = MSAASamples.None;
            dimension = TextureDimension.Tex2D;
        }

        public TextureDesc(Vector2 scale)
            : this()
        {
            // Size related init
            sizeMode = TextureSizeMode.Scale;
            this.scale = scale;
            // Important default values not handled by zero construction in this()
            slices = 1;
            msaaSamples = MSAASamples.None;
            dimension = TextureDimension.Tex2D;
        }

        public TextureDesc(ScaleFunc func)
            : this()
        {
            // Size related init
            sizeMode = TextureSizeMode.Functor;
            this.func = func;
            // Important default values not handled by zero construction in this()
            slices = 1;
            msaaSamples = MSAASamples.None;
            dimension = TextureDimension.Tex2D;
        }

        public TextureDesc(TextureDesc input)
        {
            this = input;
        }

        public override int GetHashCode()
        {
            int hashCode = 17;

            unchecked
            {
                switch (sizeMode)
                {
                    case TextureSizeMode.Explicit:
                        hashCode = hashCode * 23 + width;
                        hashCode = hashCode * 23 + height;
                        hashCode = hashCode * 23 + (int)msaaSamples;
                        break;
                    case TextureSizeMode.Functor:
                        if (func != null)
                            hashCode = hashCode * 23 + func.GetHashCode();
                        hashCode = hashCode * 23 + (enableMSAA ? 1 : 0);
                        break;
                    case TextureSizeMode.Scale:
                        hashCode = hashCode * 23 + scale.x.GetHashCode();
                        hashCode = hashCode * 23 + scale.y.GetHashCode();
                        hashCode = hashCode * 23 + (enableMSAA ? 1 : 0);
                        break;
                }

                hashCode = hashCode * 23 + mipMapBias.GetHashCode();
                hashCode = hashCode * 23 + slices;
                hashCode = hashCode * 23 + (int)depthBufferBits;
                hashCode = hashCode * 23 + (int)colorFormat;
                hashCode = hashCode * 23 + (int)filterMode;
                hashCode = hashCode * 23 + (int)wrapMode;
                hashCode = hashCode * 23 + (int)dimension;
                hashCode = hashCode * 23 + (int)memoryless;
                hashCode = hashCode * 23 + anisoLevel;
                hashCode = hashCode * 23 + (enableRandomWrite ? 1 : 0);
                hashCode = hashCode * 23 + (useMipMap ? 1 : 0);
                hashCode = hashCode * 23 + (autoGenerateMips ? 1 : 0);
                hashCode = hashCode * 23 + (isShadowMap ? 1 : 0);
                hashCode = hashCode * 23 + (bindTextureMS ? 1 : 0);
                hashCode = hashCode * 23 + (useDynamicScale ? 1 : 0);
                hashCode = hashCode * 23 + (xrInstancing ? 1 : 0);
            }

            return hashCode;
        }
    }
    #endregion

    public class RenderGraphResourceRegistry
    {
        static readonly ShaderTagId s_EmptyName = new ShaderTagId("");

        #region Resources
        internal struct TextureResource
        {
            public TextureDesc  desc;
            public bool         imported;
            public RTHandle     rt;
            public int          cachedHash;
            public int          firstWritePassIndex;
            public int          lastReadPassIndex;
            public int          shaderProperty;

            internal TextureResource(RTHandle rt, int shaderProperty)
                : this()
            {
                Reset();

                this.rt = rt;
                imported = true;
                this.shaderProperty = shaderProperty;
            }

            internal TextureResource(in TextureDesc desc, int shaderProperty)
                : this()
            {
                Reset();

                this.desc = desc;
                this.shaderProperty = shaderProperty;
            }

            void Reset()
            {
                imported = false;
                rt = null;
                cachedHash = -1;
                firstWritePassIndex = int.MaxValue;
                lastReadPassIndex = 0;
            }
        }

        internal struct RendererListResource
        {
            public RendererListDesc desc;
            public RendererList     rendererList;

            internal RendererListResource(in RendererListDesc desc)
            {
                this.desc = desc;
                this.rendererList = new RendererList(); // Invalid by default
            }
        }
        #endregion

        #region Helpers
        class ResourceArray<T>
        {
            // No List<> here because we want to be able to access and update elements by ref
            // And we want to avoid allocation so TextureResource stays a struct
            T[] m_ResourceArray = new T[32];
            int m_ResourcesCount = 0;

            public void Clear()
            {
                m_ResourcesCount = 0;
            }

            public int Add(T value)
            {
                int index = m_ResourcesCount;

                // Grow array if needed;
                if (index >= m_ResourceArray.Length)
                {
                    var newArray = new T[m_ResourceArray.Length * 2];
                    Array.Copy(m_ResourceArray, newArray, m_ResourceArray.Length);
                    m_ResourceArray = newArray;
                }

                m_ResourceArray[index] = value;
                m_ResourcesCount++;
                return index;
            }

            public ref T this[int index]
            {
                get
                {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
                    if (index >= m_ResourcesCount)
                        throw new IndexOutOfRangeException();
#endif
                    return ref m_ResourceArray[index];
                }
            }
        }
        #endregion

        ResourceArray<TextureResource>      m_TextureResources = new ResourceArray<TextureResource>();
        Dictionary<int, Stack<RTHandle>>    m_TexturePool = new Dictionary<int, Stack<RTHandle>>();
        ResourceArray<RendererListResource> m_RendererListResources = new ResourceArray<RendererListResource>();
        RTHandleSystem                      m_RTHandleSystem = new RTHandleSystem();

        // Diagnostic only
        List<(int, RTHandle)>               m_AllocatedTextures = new List<(int, RTHandle)>();

        #region Public Interface
        public RTHandle GetTexture(in RenderGraphResource handle)
        {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            if (handle.type != RenderGraphResourceType.Texture || m_TextureResources[handle.handle].rt == null)
                throw new InvalidOperationException("Trying to access a RenderGraphResource that is not a texture or is invalid.");
#endif
            return m_TextureResources[handle.handle].rt;
        }

        public RendererList GetRendererList(in RenderGraphResource handle)
        {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            if (handle.type != RenderGraphResourceType.RendererList || !m_RendererListResources[handle.handle].rendererList.isValid)
                throw new InvalidOperationException("Trying to access a RenderGraphResource that is not a RendererList or is invalid.");
#endif
            return m_RendererListResources[handle.handle].rendererList;
        }
        #endregion

        #region Internal Interface
        private RenderGraphResourceRegistry()
        {

        }

        internal RenderGraphResourceRegistry(bool supportMSAA, MSAASamples initialSampleCount)
        {
            m_RTHandleSystem.Initialize(1, 1, supportMSAA, initialSampleCount);
        }

        internal void SetRTHandleReferenceSize(int width, int height, MSAASamples msaaSamples)
        {
            m_RTHandleSystem.SetReferenceSize(width, height, msaaSamples);
        }

        internal RTHandleProperties GetRTHandleProperties() { return m_RTHandleSystem.rtHandleProperties; }

        // Texture Creation/Import APIs are internal because creation should only go through RenderGraph (for globals, outside of render passes) and RenderGraphBuilder (for render passes)
        internal RenderGraphMutableResource ImportTexture(RTHandle rt, int shaderProperty = 0)
        {
            int newHandle = m_TextureResources.Add(new TextureResource(rt, shaderProperty));
            return new RenderGraphMutableResource(newHandle, RenderGraphResourceType.Texture);
        }

        internal RenderGraphMutableResource CreateTexture(in TextureDesc desc, int shaderProperty = 0)
        {
            ValidateTextureDesc(desc);

            int newHandle = m_TextureResources.Add(new TextureResource(desc, shaderProperty));
            return new RenderGraphMutableResource(newHandle, RenderGraphResourceType.Texture);
        }

        // Not sure about this... breaks encapsulation but it allows us to avoid having render graph execution code here
        // (lastRead/FirstWrite/ClearResource etc)
        internal ref TextureResource GetTexturetResource(RenderGraphResource res)
        {
            return ref m_TextureResources[res.handle];
        }

        internal RenderGraphResource CreateRendererList(in RendererListDesc desc)
        {
            ValidateRendererListDesc(desc);

            int newHandle = m_RendererListResources.Add(new RendererListResource(desc));
            return new RenderGraphResource(newHandle, RenderGraphResourceType.RendererList);
        }

        internal void CreateTextureForPass(RenderGraphResource res)
        {
            Debug.Assert(res.type == RenderGraphResourceType.Texture);

            ref var resource = ref m_TextureResources[res.handle];
            var desc = resource.desc;
            int hashCode = desc.GetHashCode();

            resource.rt = null;
            if (!TryGetRenderTarget(hashCode, out resource.rt))
            {
                // Note: Name used here will be the one visible in the memory profiler so it means that whatever is the first pass that actually allocate the texture will set the name.
                // TODO: Find a way to display name by pass.
                switch (desc.sizeMode)
                {
                    case TextureSizeMode.Explicit:
                        resource.rt = m_RTHandleSystem.Alloc(desc.width, desc.height, desc.slices, desc.depthBufferBits, desc.colorFormat, desc.filterMode, desc.wrapMode, desc.dimension, desc.enableRandomWrite,
                        desc.useMipMap, desc.autoGenerateMips, desc.isShadowMap, desc.anisoLevel, desc.mipMapBias, desc.msaaSamples, desc.bindTextureMS, desc.useDynamicScale, desc.xrInstancing, desc.memoryless, desc.name);
                        break;
                    case TextureSizeMode.Scale:
                        resource.rt = m_RTHandleSystem.Alloc(desc.scale, desc.slices, desc.depthBufferBits, desc.colorFormat, desc.filterMode, desc.wrapMode, desc.dimension, desc.enableRandomWrite,
                        desc.useMipMap, desc.autoGenerateMips, desc.isShadowMap, desc.anisoLevel, desc.mipMapBias, desc.enableMSAA, desc.bindTextureMS, desc.useDynamicScale, desc.xrInstancing, desc.memoryless, desc.name);
                        break;
                    case TextureSizeMode.Functor:
                        resource.rt = m_RTHandleSystem.Alloc(desc.func, desc.slices, desc.depthBufferBits, desc.colorFormat, desc.filterMode, desc.wrapMode, desc.dimension, desc.enableRandomWrite,
                        desc.useMipMap, desc.autoGenerateMips, desc.isShadowMap, desc.anisoLevel, desc.mipMapBias, desc.enableMSAA, desc.bindTextureMS, desc.useDynamicScale, desc.xrInstancing, desc.memoryless, desc.name);
                        break;
                }
            }

#if DEVELOPMENT_BUILD || UNITY_EDITOR
            if (hashCode != -1)
            {
                m_AllocatedTextures.Add((hashCode, resource.rt));
            }
#endif

            resource.cachedHash = hashCode;
        }

        internal void ReleaseTextureForPass(RenderGraphResource res)
        {
            Debug.Assert(res.type == RenderGraphResourceType.Texture);

            ref var resource = ref m_TextureResources[res.handle];
            ReleaseTextureResource(resource.cachedHash, resource.rt);
            resource.cachedHash = -1;
            resource.rt = null;
        }

        void ReleaseTextureResource(int hash, RTHandle rt)
        {
            if (!m_TexturePool.TryGetValue(hash, out var stack))
            {
                stack = new Stack<RTHandle>();
                m_TexturePool.Add(hash, stack);
            }

            stack.Push(rt);

#if DEVELOPMENT_BUILD || UNITY_EDITOR
            m_AllocatedTextures.Remove((hash, rt));
#endif
        }

        void ValidateTextureDesc(in TextureDesc desc)
        {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            if (desc.colorFormat == GraphicsFormat.None && desc.depthBufferBits == DepthBits.None)
            {
                throw new ArgumentException("Texture was created with an invalid color format.");
            }

            if (desc.dimension == TextureDimension.None)
            {
                throw new ArgumentException("Texture was created with an invalid texture dimension.");
            }

            if (desc.slices == 0)
            {
                throw new ArgumentException("Texture was created with a slices parameter value of zero.");
            }

            if (desc.sizeMode == TextureSizeMode.Explicit)
            {
                if (desc.width == 0 || desc.height == 0)
                    throw new ArgumentException("Texture using Explicit size mode was create with either width or height at zero.");
                if (desc.enableMSAA)
                    throw new ArgumentException("enableMSAA TextureDesc parameter is not supported for textures using Explicit size mode.");
            }

            if (desc.sizeMode == TextureSizeMode.Scale || desc.sizeMode == TextureSizeMode.Functor)
            {
                if (desc.msaaSamples != MSAASamples.None)
                    throw new ArgumentException("msaaSamples TextureDesc parameter is not supported for textures using Scale or Functor size mode.");
            }
#endif
        }

        void ValidateRendererListDesc(in RendererListDesc desc)
        {
#if DEVELOPMENT_BUILD || UNITY_EDITOR

            if (desc.passName != ShaderTagId.none && desc.passNames != null
                || desc.passName == ShaderTagId.none && desc.passNames == null)
            {
                throw new ArgumentException("Renderer List creation descriptor must contain either a single passName or an array of passNames.");
            }

            if (desc.renderQueueRange.lowerBound == 0 && desc.renderQueueRange.upperBound == 0)
            {
                throw new ArgumentException("Renderer List creation descriptor must have a valid RenderQueueRange.");
            }

            if (desc.camera == null)
            {
                throw new ArgumentException("Renderer List creation descriptor must have a valid Camera.");
            }
#endif
        }

        bool TryGetRenderTarget(int hashCode, out RTHandle rt)
        {
            if (m_TexturePool.TryGetValue(hashCode, out var stack) && stack.Count > 0)
            {
                rt = stack.Pop();
                return true;
            }

            rt = null;
            return false;
        }

        internal void CreateRendererLists(List<RenderGraphResource> rendererLists)
        {
            // For now we just create a simple structure
            // but when the proper API is available in trunk we'll kick off renderer lists creation jobs here.
            foreach(var rendererList in rendererLists)
            {
                Debug.Assert(rendererList.type == RenderGraphResourceType.RendererList);

                ref var rendererListResource = ref m_RendererListResources[rendererList.handle];
                ref var desc = ref rendererListResource.desc;
                RendererList newRendererList = RendererList.Create(desc);
                rendererListResource.rendererList = newRendererList;
            }
        }

        internal void Clear()
        {
            m_TextureResources.Clear();
            m_RendererListResources.Clear();

#if DEVELOPMENT_BUILD || UNITY_EDITOR
            if (m_AllocatedTextures.Count != 0)
            {
                Debug.LogWarning("RenderGraph: Not all textures were released.");
                List<(int, RTHandle)> tempList = new List<(int, RTHandle)>(m_AllocatedTextures);
                foreach (var value in tempList)
                {
                    ReleaseTextureResource(value.Item1, value.Item2);
                }
            }
#endif
        }

        internal void Cleanup()
        {
            foreach (var value in m_TexturePool)
            {
                foreach (var rt in value.Value)
                {
                    m_RTHandleSystem.Release(rt);
                }
            }
        }
        #endregion
    }
}
