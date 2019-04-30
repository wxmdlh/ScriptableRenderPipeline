using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering.HDPipeline;

namespace UnityEditor.Experimental.Rendering.HDPipeline
{
    public class ShaderGraphUIBlock : MaterialUIBlock
    {
        protected static class Styles
        {
            public static readonly string header = "Shader Graph";
        }

        Expandable m_ExpandableBit;

        public ShaderGraphUIBlock(Expandable expandableBit = Expandable.ShaderGraph)
        {
            m_ExpandableBit = expandableBit;
        }

        public override void LoadMaterialKeywords() {}

        public override void OnGUI()
        {
            using (var header = new HeaderScope(Styles.header, (uint)m_ExpandableBit, materialEditor))
            {
                if (header.expanded)
                    DrawShaderGraphGUI();
            }
        }

        void DrawShaderGraphGUI()
        {
            materialEditor.PropertiesDefaultGUI(properties);
            if (materialEditor.EmissionEnabledProperty())
            {
                materialEditor.LightmapEmissionFlagsProperty(MaterialEditor.kMiniTextureFieldLabelIndentLevel, true, true);
            }

            // I absolutely don't know what this is meant to do
            const string materialTag = "MotionVector";
            foreach (var material in materials)
            {
                string tag = material.GetTag(materialTag, false, "Nothing");
                if (tag == "Nothing")
                {
                    material.SetShaderPassEnabled(HDShaderPassNames.s_MotionVectorsStr, false);
                    material.SetOverrideTag(materialTag, "User");
                }
            }

            DrawMotionVectorToggle();
        }

        void DrawMotionVectorToggle()
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