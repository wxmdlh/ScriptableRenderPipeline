using System;
using UnityEngine;
using UnityEngine.Experimental.Rendering.HDPipeline;

namespace UnityEditor.Experimental.Rendering.HDPipeline
{
    /// <summary>
    /// GUI for HDRP Shadergraph Decal materials
    /// </summary>
    class DecalGUI : HDShaderGUI
    {
        [Flags]
        enum Expandable : uint
        {
            Sorting = 1 << 0,
            ShaderGraph = 1 << 1,
        }

        MaterialUIBlockList uiBlocks = new MaterialUIBlockList
        {
            new ShaderGraphUIBlock((MaterialUIBlock.Expandable)Expandable.ShaderGraph, ShaderGraphUIBlock.Features.None),
            new DecalSortingInputsUIBlock((MaterialUIBlock.Expandable)Expandable.Sorting),
        };

        public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] props)
        {
            // always instanced
            SerializedProperty instancing = materialEditor.serializedObject.FindProperty("m_EnableInstancingVariants");
            instancing.boolValue = true;

            using (var changed = new EditorGUI.ChangeCheckScope())
            {
                uiBlocks.OnGUI(materialEditor, props);
            }
        }

        protected override void SetupMaterialKeywordsAndPassInternal(Material material) => DecalUI.SetupMaterialKeywordsAndPass(material);
    }
}
