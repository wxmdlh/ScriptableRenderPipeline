using UnityEngine;
using UnityEngine.UIElements;

namespace UnityEditor.ShaderGraph.Drawing.Colors
{
    // Implement this to provide colors based on whatever factor you want
    interface IColorProvider
    {
        string Title { get; }
        
        bool AllowCustom { get; }

        // If your color must be set programatically, return it here.
        // If your colors are in USS and set via classes, return null here.
        Color? GetColor(AbstractMaterialNode node);
        
        // If your color is defined in USS and set via classes, set them on the element here and return true.
        // If your color must be set programatically, return false here.
        bool ApplyColorTo(AbstractMaterialNode node, VisualElement el);
    }
}