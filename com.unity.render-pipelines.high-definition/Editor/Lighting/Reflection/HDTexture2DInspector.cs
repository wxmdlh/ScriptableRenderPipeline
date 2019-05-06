using UnityEngine;
using UnityEngine.Experimental.Rendering.HDPipeline;

namespace UnityEditor.Experimental.Rendering.HDPipeline
{
    [CustomEditorForRenderPipeline(typeof(Texture2D), typeof(HDRenderPipelineAsset))]
    class HDTexture2DInspector : Editor
    {
        static GUIContent s_MipMapLow, s_MipMapHigh, s_ExposureLow;
        static GUIStyle s_PreLabel;

        Material m_ReflectiveMaterial;

        public float previewExposure = 0f;
        public float mipLevelPreview = 0f;

        void Awake()
        {
            m_ReflectiveMaterial = new Material(Shader.Find("Debug/Texture2DPreview"))
            {
                hideFlags = HideFlags.HideAndDontSave
            };
        }

        void OnEnable()
        {
            m_ReflectiveMaterial.SetTexture("_MainTex", target as Texture);
        }

        public override bool HasPreviewGUI()
        {
            return true;
        }

        public override void OnPreviewGUI(Rect r, GUIStyle background)
        {
            if (m_ReflectiveMaterial != null)
            {
                m_ReflectiveMaterial.SetFloat("_Exposure", previewExposure);
                m_ReflectiveMaterial.SetFloat("_MipLevel", mipLevelPreview);
            }

            if (Event.current.type != EventType.Repaint)
                return;

            var texture = (Texture) target;

            Graphics.DrawTexture(r, texture, m_ReflectiveMaterial);
        }

        public override void OnPreviewSettings()
        {
            if (s_MipMapLow == null)
                InitIcons();

            var mipmapCount = 0;
            var tex2D = target as Texture2D;
            var rt = target as RenderTexture;
            if (tex2D != null)
                mipmapCount = tex2D.mipmapCount;
            if (rt != null)
                mipmapCount = rt.useMipMap
                    ? (int)(Mathf.Log(Mathf.Max(rt.width, rt.height)) / Mathf.Log(2))
                    : 1;

            GUI.enabled = true;

            GUILayout.Box(s_ExposureLow, s_PreLabel, GUILayout.MaxWidth(20));
            GUI.changed = false;
            previewExposure = GUILayout.HorizontalSlider(previewExposure, -20f, 20f, GUILayout.MaxWidth(80));
            GUILayout.Space(5);
            GUILayout.Box(s_MipMapHigh, s_PreLabel, GUILayout.MaxWidth(20));
            GUI.changed = false;
            mipLevelPreview = GUILayout.HorizontalSlider(mipLevelPreview, 0, mipmapCount, GUILayout.MaxWidth(80));
            GUILayout.Box(s_MipMapLow, s_PreLabel, GUILayout.MaxWidth(20));
        }

        static void InitIcons()
        {
            s_MipMapLow = EditorGUIUtility.IconContent("PreTextureMipMapLow");
            s_MipMapHigh = EditorGUIUtility.IconContent("PreTextureMipMapHigh");
            s_ExposureLow = EditorGUIUtility.IconContent("SceneViewLighting");
            s_PreLabel = "preLabel";
        }
    }
}
