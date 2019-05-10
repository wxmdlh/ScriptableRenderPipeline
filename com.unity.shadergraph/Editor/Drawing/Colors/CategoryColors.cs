using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityEditor.ShaderGraph.Drawing.Colors
{
    class CategoryColors : IColorProvider
    {
        public string Title => "Category";
        public bool AllowCustom => false;

        public Color? GetColor(AbstractMaterialNode node)
        {
            return null;
        }

        public bool ApplyColorTo(AbstractMaterialNode node, VisualElement el)
        {
            if (!(node.GetType().GetCustomAttributes(typeof(TitleAttribute), false).FirstOrDefault() is TitleAttribute title))
                return true;

            var cat = title.title[0];
            
            if (string.IsNullOrEmpty(cat))
                return true;
            
            el.AddToClassList(cat);
            return true;
        }
    }
}