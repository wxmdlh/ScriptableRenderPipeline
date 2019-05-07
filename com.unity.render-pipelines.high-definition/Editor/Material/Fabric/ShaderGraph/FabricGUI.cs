using UnityEngine;
using UnityEngine.Experimental.Rendering.HDPipeline;
using System.Linq;

namespace UnityEditor.Experimental.Rendering.HDPipeline
{
    /// <summary>
    /// GUI for HDRP Fabric shader graphs
    /// </summary>
    class FabricGUI : HDShaderGUI
    {
        // For surface option shader graph we only want all unlit features but alpha clip
        const SurfaceOptionUIBlock.Features   surfaceOptionFeatures = SurfaceOptionUIBlock.Features.Unlit ^ SurfaceOptionUIBlock.Features.AlphaCutoff;
        
        MaterialUIBlockList uiBlocks = new MaterialUIBlockList
        {
            new SurfaceOptionUIBlock(MaterialUIBlock.Expandable.Base, features: surfaceOptionFeatures),
            new ShaderGraphUIBlock(MaterialUIBlock.Expandable.ShaderGraph),
        };

        public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] props)
        {
            using (var changed = new EditorGUI.ChangeCheckScope())
            {
                uiBlocks.OnGUI(materialEditor, props);

                // Apply material keywords and pass:
                if (changed.changed)
                {
                    foreach (var material in uiBlocks.materials)
                        SetupMaterialKeywordsAndPassInternal(material);
                }
            }
        }

        // Currently Lit material keyword setup is enough for fabric so we don't have a function for it
        protected override void SetupMaterialKeywordsAndPassInternal(Material material) => LitGUI.SetupMaterialKeywordsAndPass(material);
    }
}
