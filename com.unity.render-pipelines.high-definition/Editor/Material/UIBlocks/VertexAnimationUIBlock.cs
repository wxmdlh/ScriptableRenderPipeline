using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering.HDPipeline;
using System.Linq;

// Include material common properties names
using static UnityEngine.Experimental.Rendering.HDPipeline.HDMaterialProperties;

namespace UnityEditor.Experimental.Rendering.HDPipeline
{
    public class VertexAnimationUIBlock : MaterialUIBlock
    {
        public class Styles
        {
            public const string header = "Vertex Animation";

            public static GUIContent enableMotionVectorForVertexAnimationText = new GUIContent("MotionVector For Vertex Animation", "When enabled, HDRP processes an object motion vector pass for this material.");
        }

        Expandable  m_ExpandableBit;

        MaterialProperty enableMotionVectorForVertexAnimation = null;
        const string kEnableMotionVectorForVertexAnimation = "_EnableMotionVectorForVertexAnimation";
        // Wind
        MaterialProperty windEnable = null;
        const string kWindEnabled = "_EnableWind";

        public VertexAnimationUIBlock(Expandable expandableBit)
        {
            m_ExpandableBit = expandableBit;
        }

        public override void LoadMaterialProperties()
        {
            windEnable = FindProperty(kWindEnabled, false);
            enableMotionVectorForVertexAnimation = FindProperty(kEnableMotionVectorForVertexAnimation);
        }

        public override void OnGUI()
        {
            using (var header = new MaterialHeaderScope(Styles.header, (uint)m_ExpandableBit, materialEditor))
            {
                if (header.expanded)
                {
                    DrawVertexAnimationGUI();
                }
            }
        }

        void DrawVertexAnimationGUI()
        {
            // Does not works with multi-selection but will get deprecated
            bool windEnabled = materials[0].HasProperty(kWindEnabled) && materials[0].GetFloat(kWindEnabled) > 0.0f;

            if (windEnable != null)
            {
                // Hide wind option. Wind is deprecated and will be remove in the future. Use shader graph instead
                /*
                m_MaterialEditor.ShaderProperty(windEnable, StylesBaseLit.windText);
                if (!windEnable.hasMixedValue && windEnable.floatValue > 0.0f)
                {
                    EditorGUI.indentLevel++;
                    m_MaterialEditor.ShaderProperty(windInitialBend, StylesBaseLit.windInitialBendText);
                    m_MaterialEditor.ShaderProperty(windStiffness, StylesBaseLit.windStiffnessText);
                    m_MaterialEditor.ShaderProperty(windDrag, StylesBaseLit.windDragText);
                    m_MaterialEditor.ShaderProperty(windShiverDrag, StylesBaseLit.windShiverDragText);
                    m_MaterialEditor.ShaderProperty(windShiverDirectionality, StylesBaseLit.windShiverDirectionalityText);
                    EditorGUI.indentLevel--;
                }
                */
            }

            if (enableMotionVectorForVertexAnimation != null)
                materialEditor.ShaderProperty(enableMotionVectorForVertexAnimation, Styles.enableMotionVectorForVertexAnimationText);
        }
    }
}