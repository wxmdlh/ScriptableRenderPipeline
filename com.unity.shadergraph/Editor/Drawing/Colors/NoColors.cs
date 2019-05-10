using UnityEngine;
using UnityEngine.UIElements;

namespace UnityEditor.ShaderGraph.Drawing.Colors
{
    class NoColors : IColorProvider
    {
        public static string NoColorTitle = "<None>";
        public string Title => NoColorTitle;
        public bool AllowCustom => false;

        public Color? GetColor(AbstractMaterialNode node)
        {
            return null;
        }

        public bool ApplyColorTo(AbstractMaterialNode node, VisualElement el)
        {
            return true;
        }
    }
}