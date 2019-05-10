using System.Collections.Generic;
using UnityEditor.Graphing;
using UnityEngine.Rendering;
using UnityEditor.ShaderGraph.Drawing.Inspector;

namespace UnityEditor.ShaderGraph
{
    interface IMasterNode
    {
        string GetShader(GenerationMode mode, string name, out List<PropertyCollector.TextureInfo> configuredTextures, List<string> sourceAssetDependencyPaths = null);
        bool IsPipelineCompatible(RenderPipelineAsset renderPipelineAsset);
        ISubShader GetActiveSubShader();
        void SetPreviewView(MasterPreviewView previewView);
    }
}
