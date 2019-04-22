using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityEditor.ShaderGraph.Drawing.Colors
{
    // Use this to set colors on your node titles.
    // There are 2 methods of setting colors - direct Color objects via code (such as data saved in the node itself),
    // or setting classes on a VisualElement, allowing the colors themselves to be defined in USS. See notes on
    // IColorProvider for how to use these different methods.
    class ColorManager
    {
        static string DefaultProvider = NoColors.NoColorTitle;
    
        List<IColorProvider> m_Providers;
        
        int m_ActiveIndex = 0;
        public int activeIndex
        {
            get => m_ActiveIndex;
            set
            {
                if (value < 0 || value >= m_Providers.Count)
                    return;
                
                m_ActiveIndex = value;
            }
        }

        public ColorManager(string activeColors)
        {
            m_Providers = new List<IColorProvider>();

            if (string.IsNullOrEmpty(activeColors))
                activeColors = DefaultProvider;

            foreach (var colorType in UnityEditor.TypeCache.GetTypesDerivedFrom<IColorProvider>())
            {
                var provider = (IColorProvider) Activator.CreateInstance(colorType);
                m_Providers.Add(provider);
                if (provider.Title == activeColors)
                {
                    activeIndex = m_Providers.Count-1;
                }
            }
        }

        public void SetColor(IShaderNodeView nodeView)
        {
            var curProvider = m_Providers[m_ActiveIndex];
            nodeView.colorElement.ClearClassList();
            if (curProvider.ApplyColorTo(nodeView.node, nodeView.colorElement))
            {
                nodeView.SetColor(null);
                return;
            }
            
            nodeView.SetColor(curProvider.GetColor(nodeView.node));
        }

        public IEnumerable<string> providerNames
        {
            get => m_Providers.Select(p => p.Title);
        }

        public string activeProviderName
        {
            get => m_Providers[activeIndex].Title;
        }

        public bool activeSupportsCustom
        {
            get => m_Providers[activeIndex].AllowCustom;
        }
    }

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
