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

        public override bool isRenamable
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
        private List<int> m_EnumValues = new List<int>();

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
            if (enumType == EnumType.KeywordEnum)
                enumValuesString = enumNames.Aggregate((s, e) => s + ", " + e);
            else
            {
                for (int i = 0; i < enumNames.Count; i++)
                {
                    int value = (i < enumValues.Count) ? enumValues[i] : i;
                    enumValuesString += (enumNames[i] + ", " + value + ((i != enumNames.Count - 1) ? ", " : ""));
                }
            }
            
            result.Append($"[{enumType}({enumValuesString})] {referenceName}(\"{displayName}\", Float) = {NodeUtils.FloatToShaderValue(value)}");

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