using System;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor.Graphing.Util;
using UnityEditor.ShaderGraph;
using UnityEditor.ShaderGraph.Drawing;
using UnityEditor.ShaderGraph.Drawing.Controls;
using UnityEditor.Experimental.Rendering.HDPipeline;
using UnityEngine.Experimental.Rendering.HDPipeline;

namespace UnityEditor.Experimental.Rendering.HDPipeline.Drawing
{
    class HDUnlitSettingsView : VisualElement
    {
        HDUnlitMasterNode m_Node;

        IntegerField m_SortPiorityField;

        Label CreateLabel(string text, int indentLevel)
        {
            string label = "";
            for (var i = 0; i < indentLevel; i++)
            {
                label += "    ";
            }
            return new Label(label + text);
        }

        public HDUnlitSettingsView(HDUnlitMasterNode node)
        {
            int indentLevel = 0;

            m_Node = node;
            PropertySheet ps = new PropertySheet();
            
            ps.Add(new PropertyRow(new Label("Double-Sided")), (row) =>
            {
                row.Add(new Toggle(), (toggle) =>
                {
                    toggle.value = m_Node.doubleSided.isOn;
                    toggle.OnToggleChanged(ChangeDoubleSided);
                });
            });

            // TODO: move this too ?
            ps.Add(new PropertyRow(CreateLabel("Alpha Clipping", indentLevel)), (row) =>
            {
                row.Add(new Toggle(), (toggle) =>
                {
                    toggle.value = m_Node.alphaTest.isOn;
                    toggle.OnToggleChanged(ChangeAlphaTest);
                });
            });

            Add(ps);
        }

        void ChangeDoubleSided(ChangeEvent<bool> evt)
        {
            m_Node.owner.owner.RegisterCompleteObjectUndo("Double-Sided Change");
            ToggleData td = m_Node.doubleSided;
            td.isOn = evt.newValue;
            m_Node.doubleSided = td;
        }

        void ChangeAlphaTest(ChangeEvent<bool> evt)
        {
            m_Node.owner.owner.RegisterCompleteObjectUndo("Alpha Test Change");
            ToggleData td = m_Node.alphaTest;
            td.isOn = evt.newValue;
            m_Node.alphaTest = td;
        }
    }
}
