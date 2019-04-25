using System;
using System.Text;
using UnityEditor.Graphing;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace UnityEditor.ShaderGraph
{
    enum FloatType
    {
        Default,
        Slider,
        Integer,
        ToggleUI,
        Enum,
    }

    [Serializable]
    [FormerName("UnityEditor.ShaderGraph.FloatShaderProperty")]
    class Vector1ShaderProperty : AbstractShaderProperty<float>
    {
        public Vector1ShaderProperty()
        {
            displayName = "Vector1";
        }

        public override PropertyType propertyType
        {
            get { return PropertyType.Vector1; }
        }

        public override Vector4 defaultValue
        {
            get { return new Vector4(value, value, value, value); }
        }

        public override bool isBatchable
        {
            get { return true; }
        }

        public override bool isExposable
        {
            get { return true; }
        }

        [SerializeField]
        private FloatType m_FloatType = FloatType.Default;

        public FloatType floatType
        {
            get { return m_FloatType; }
            set
            {
                if (m_FloatType == value)
                    return;
                m_FloatType = value;
            }
        }

        [SerializeField]
        private Vector2 m_RangeValues = new Vector2(0, 1);

        public Vector2 rangeValues
        {
            get { return m_RangeValues; }
            set
            {
                if (m_RangeValues == value)
                    return;
                m_RangeValues = value;
            }
        }

        [SerializeField]
        private List<string> m_enumNames = new List<string>();
        [SerializeField]
        private List<int> m_enumValues; // default to null, so we generate a sequence from 0 to enumNames.Length for the shader

        public List<string> enumNames
        {
            get => m_enumNames;
            set => m_enumNames = value;
        }

        public List<int> enumValues
        {
            get => m_enumValues;
            set => m_enumValues = value;
        }
        
        [SerializeField]
        bool    m_Hidden = false;

        public bool hidden
        {
            get { return m_Hidden; }
            set { m_Hidden = value; }
        }

        public override string GetPropertyBlockString()
        {
            var result = new StringBuilder();
            if (hidden)
                result.Append("[HideInInspector] ");
            if (floatType == FloatType.ToggleUI)
                result.Append("[ToggleUI] ");
            if (floatType == FloatType.Enum && enumNames?.Count > 0)
            {
                // TODO: values
                result.Append("[Enum(");
                result.Append(enumNames.Aggregate((s, e) => s + ", " + e));
                result.Append(")] ");
            }
            result.Append(referenceName);
            result.Append("(\"");
            result.Append(displayName);
            switch (floatType)
            {
                case FloatType.Slider:
                    result.Append("\", Range(");
                    result.Append(NodeUtils.FloatToShaderValue(m_RangeValues.x) + ", " + NodeUtils.FloatToShaderValue(m_RangeValues.y));
                    result.Append(")) = ");
                    break;
                case FloatType.Integer:
                case FloatType.ToggleUI: // We assume that toggle UI properties must be saved as int
                    result.Append("\", Int) = ");
                    break;
                case FloatType.Enum:
                default:
                    result.Append("\", Float) = ");
                    break;
            }
            result.Append(NodeUtils.FloatToShaderValue(value));
            return result.ToString();
        }

        public override string GetPropertyDeclarationString(string delimiter = ";")
        {
            return string.Format("float {0}{1}", referenceName, delimiter);
        }

        public override PreviewProperty GetPreviewMaterialProperty()
        {
            return new PreviewProperty(PropertyType.Vector1)
            {
                name = referenceName,
                floatValue = value
            };
        }

        public override AbstractMaterialNode ToConcreteNode()
        {
            switch (m_FloatType)
            {
                case FloatType.Slider:
                    return new SliderNode { value = new Vector3(value, m_RangeValues.x, m_RangeValues.y) };
                case FloatType.Integer:
                    return new IntegerNode { value = (int)value };
                // TODO: Toggle node creation
                // TODO: Enum node creation
                default:
                    var node = new Vector1Node();
                    node.FindInputSlot<Vector1MaterialSlot>(Vector1Node.InputSlotXId).value = value;
                    return node;
            }
        }

        public override AbstractShaderProperty Copy()
        {
            var copied = new Vector1ShaderProperty();
            copied.displayName = displayName;
            copied.value = value;
            return copied;
        }
    }
}
