using UnityEngine;
using UnityEngine.UIElements;

namespace UnityEditor.ShaderGraph.Drawing.Colors
{
    class UserColors : IColorProvider 
    {
        string m_Title = "User Defined";
        public bool AllowCustom => true;

        public UserColors() {}
        
        public UserColors(string title)
        {
            m_Title = title;
        }

        public void ChangeTitle(string newTitle)
        {
            m_Title = newTitle;
        }
        
        public string Title => m_Title;

        public Color? GetColor(AbstractMaterialNode node)
        {
            return node.GetColor(m_Title);
        }

        public bool ApplyColorTo(AbstractMaterialNode node, VisualElement el)
        {
            return false;
        }
    }
}