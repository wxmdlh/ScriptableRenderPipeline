using System;
using System.Linq;
using UnityEngine;
using UnityEngine.Experimental.Rendering.HDPipeline;
using UnityEditor.Rendering;

namespace UnityEditor.Experimental.Rendering.HDPipeline
{
    public abstract class MaterialUIBlock
    {
        protected MaterialEditor      materialEditor;
        protected Material[]          materials;
        protected MaterialProperty[]  properties;

        //Be sure to end before after last LayeredLitGUI.LayerExpendable
        [Flags]
        public enum Expandable : uint
        {
            Base = 1<<0,
            Input = 1<<1,
            Tesselation = 1<<2,
            Transparency = 1<<3,
            VertexAnimation = 1<<4,
            Detail = 1<<5,
            Emissive = 1<<6,
            Advance = 1<<7,
            Other = 1 << 8
        }

        public void         Initialize(MaterialEditor materialEditor, MaterialProperty[] properties)
        {
            this.materialEditor = materialEditor;
            this.properties = properties;
            materials = materialEditor.targets.Select(target => target as Material).ToArray();

            materialEditor.InitExpandableState();
            LoadMaterialKeywords();
        }

        protected MaterialProperty FindProperty(string propertyName, bool isMandatory = true) //  TODO: set isMandatory to false when shader graphs will be generated correctly
        {
            // ShaderGUI.FindProperty is a protected member of ShaderGUI so we can't call it here:
            // return ShaderGUI.FindProperty(propertyName, properties, isMandatory);

            foreach (var prop in properties)
                if (prop.name == propertyName)
                    return prop;

            if (isMandatory)
                throw new ArgumentException("Could not find MaterialProperty: '" + propertyName + "', Num properties: " + properties.Length);
            return null;
        }
        
        public abstract void LoadMaterialKeywords();
        public abstract void OnGUI();

        // TODO: move this to another file
        protected struct HeaderScope : IDisposable
        {
            public readonly bool expanded;
            private bool spaceAtEnd;

            public HeaderScope(string title, uint bitExpanded, MaterialEditor materialEditor, bool spaceAtEnd = true, Color colorDot = default(Color), bool subHeader = false)
            {
                bool beforeExpended = materialEditor.GetExpandedAreas(bitExpanded);

                this.spaceAtEnd = spaceAtEnd;
                if (!subHeader)
                    CoreEditorUtils.DrawSplitter();
                GUILayout.BeginVertical();

                bool saveChangeState = GUI.changed;
                if (colorDot != default(Color))
                    title = "   " + title;
                expanded = subHeader
                    ? CoreEditorUtils.DrawSubHeaderFoldout(title, beforeExpended)
                    : CoreEditorUtils.DrawHeaderFoldout(title, beforeExpended);
                if (colorDot != default(Color))
                {
                    Color previousColor = GUI.contentColor;
                    GUI.contentColor = colorDot;
                    Rect headerRect = GUILayoutUtility.GetLastRect();
                    headerRect.xMin += 16f;
                    EditorGUI.LabelField(headerRect, "■");
                    GUI.contentColor = previousColor;
                }
                if (expanded ^ beforeExpended)
                {
                    materialEditor.SetExpandedAreas((uint)bitExpanded, expanded);
                    saveChangeState = true;
                }
                GUI.changed = saveChangeState;

                if (expanded)
                    ++EditorGUI.indentLevel;
            }

            void IDisposable.Dispose()
            {
                if (expanded)
                {
                    if (spaceAtEnd && (Event.current.type == EventType.Repaint || Event.current.type == EventType.Layout))
                        EditorGUILayout.Space();
                    --EditorGUI.indentLevel;
                }
                GUILayout.EndVertical();
            }
        }
    }
}