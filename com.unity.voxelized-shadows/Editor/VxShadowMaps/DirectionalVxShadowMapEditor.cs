using UnityEditor;

namespace UnityEngine.Experimental.VoxelizedShadows
{
    [CustomEditor(typeof(DirectionalVxShadowMap))]
    public class DirectionalVxShadowMapEditor : Editor
    {
        private void OnEnable()
        {

        }

        public override void OnInspectorGUI()
        {
            var vxsm = target as DirectionalVxShadowMap;

            var shadowsBlendOptions = new string[]
            {
                ShadowsBlendMode.OnlyVxShadows.ToString(),
                ShadowsBlendMode.BlendDynamicShadows.ToString(),
            };

            EditorGUILayout.Space();
            var result0 = EditorGUILayout.Popup("Shadows Blend", (int)vxsm.ShadowsBlend, shadowsBlendOptions);
            vxsm.ShadowsBlend = (ShadowsBlendMode)result0;

            vxsm.VolumeScale = EditorGUILayout.FloatField("Volume Scale", vxsm.VolumeScale);

            bool validVxsm = vxsm.index >= 0 && vxsm.index < vxsm.DataList.Count;
            if (validVxsm)
            {
                float indexFloat = (float)vxsm.index;
                float maxIndexFloat = (float)vxsm.DataList.Count - 1;

                float sizeInMBytes = (float)vxsm.DataList[vxsm.index].SizeInBytes / (1024.0f * 1024.0f);
                EditorGUILayout.FloatField("Size(MB)", sizeInMBytes);

                string indexString = "Index(" + vxsm.DataList.Count + ")";
                var result1 = EditorGUILayout.Slider(indexString, indexFloat, 0.0f, maxIndexFloat);
                vxsm.index = (int)result1;
            }
        }

        [DrawGizmo(GizmoType.Selected|GizmoType.Active)]
        static void DrawGizmosSelected(DirectionalVxShadowMap vxsm, GizmoType gizmoType)
        {
            var light = vxsm.GetComponent<Light>();
            var volumeScale = vxsm.VolumeScale;

            if (light != null)
            {
                Gizmos.color = new Color(1.0f, 0.0f, 0.0f, 0.3f);
                Gizmos.matrix = light.transform.localToWorldMatrix;
                Gizmos.DrawCube(Vector3.zero, new Vector3(volumeScale, volumeScale, volumeScale));
            }
        }
    }
}
