using System;
using System.Text;
using UnityEditor.Graphing;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace UnityEditor.ShaderGraph
{
    public enum EnumType
    {
        Enum,
        KeywordEnum,
    }

    [Serializable]
    class EnumShaderProperty : AbstractShaderProperty<float>
    {
        public EnumShaderProperty()
        {
            displayName = "Enum";
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
        private EnumType m_EnumType = EnumType.Enum;

        public EnumType enumType
        {
            get { return m_EnumType; }
            set
            {
                if (m_EnumType == value)
                    return;
                m_EnumType = value;
            }
        }

        [SerializeField]
        private List<string> m_EnumNames = new List<string>();
        [SerializeField]
        private List<int> m_EnumValues; // default to null, so we generate a sequence from 0 to enumNames.Length for the shader

        public List<string> enumNames
        {
            get => m_EnumNames;
            set => m_EnumNames = value;
        }

        public List<int> enumValues
        {
            get => m_EnumValues;
            set => m_EnumValues = value;
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

            string enumValuesString = ""; // TODO
            if (enumType == EnumType.KeywordEnum || enumValues == null || enumValues.Count != enumNames.Count)
                enumValuesString = enumNames.Aggregate((s, e) => s + ", " + e);
            else
            {
                for (int i = 0; i < enumNames.Count; i++)
                    enumValuesString += (enumNames[i] + " = " + enumValues[i] + ((i != enumNames.Count - 1) ? ", " : ""));
            }
            
            result.Append($"[{enumType}({enumValuesString})] {referenceName}(\"{displayName}\", Float) = {NodeUtils.FloatToShaderValue(value)}");
            // if (enumType == EnumType.Enum && enumNames?.Count > 0)
            // {
            //     // TODO: values
            //     result.Append("[" + enumType + "(");
            //     result.Append(enumNames.Aggregate((s, e) => s + ", " + e));
            //     result.Append(")] ");
            // }
            // result.Append(referenceName);
            // result.Append("(\"");
            // result.Append(displayName);
            // result.Append("\", Float) = ");
            // result.Append();
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
            throw new Exception("Enums as node are not yet supported");
        }

        public override AbstractShaderProperty Copy()
        {
            var copied = new EnumShaderProperty();
            copied.displayName = displayName;
            copied.value = value;
            copied.enumValues = enumValues;
            copied.enumNames = enumNames;
            return copied;
        }
    }
}