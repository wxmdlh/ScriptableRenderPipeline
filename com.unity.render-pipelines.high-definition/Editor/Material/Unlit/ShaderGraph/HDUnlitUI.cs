using UnityEngine;
using UnityEngine.Experimental.Rendering.HDPipeline;

namespace UnityEditor.Experimental.Rendering.HDPipeline
{
    public class HDUnlitGUI : ShaderGUI
    {
        // For surface option shader graph we only want all features but alpha threshold and shadow threshold that can be written into the master node
        const SurfaceOptionUIBlock.Features   surfaceOptionFeatures = SurfaceOptionUIBlock.Features.All ^ (SurfaceOptionUIBlock.Features.AlphaCutoffShadowThreshold | SurfaceOptionUIBlock.Features.AlphaCutoffThreshold);
        
        MaterialUIBlockList uiBlocks = new MaterialUIBlockList()
        {
            new SurfaceOptionUIBlock(MaterialUIBlock.Expandable.Base, surfaceOptionFeatures),
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
                        UnlitGUI.SetupMaterialKeywordsAndPass(material);
                }
            }
            
            materialEditor.PropertiesDefaultGUI(props);
            if (materialEditor.EmissionEnabledProperty())
            {
                materialEditor.LightmapEmissionFlagsProperty(MaterialEditor.kMiniTextureFieldLabelIndentLevel, true, true);
            }

            // Make sure all selected materials are initialized.
            string materialTag = "MotionVector";
            foreach (var obj in materialEditor.targets)
            {
                var material = (Material)obj;
                string tag = material.GetTag(materialTag, false, "Nothing");
                if (tag == "Nothing")
                {
                    material.SetShaderPassEnabled(HDShaderPassNames.s_MotionVectorsStr, false);
                    material.SetOverrideTag(materialTag, "User");
                }
            }
            // UnlitGUI.MaterialPropertiesGUI(materialEditor.target as Material);

            {
                // If using multi-select, apply toggled material to all materials.
                bool enabled = ((Material)materialEditor.target).GetShaderPassEnabled(HDShaderPassNames.s_MotionVectorsStr);
                EditorGUI.BeginChangeCheck();
                enabled = EditorGUILayout.Toggle("Motion Vector For Vertex Animation", enabled);
                if (EditorGUI.EndChangeCheck())
                {
                    foreach (var obj in materialEditor.targets)
                    {
                        var material = (Material)obj;
                        material.SetShaderPassEnabled(HDShaderPassNames.s_MotionVectorsStr, enabled);
                    }
                }
            }
        }
    }
}
