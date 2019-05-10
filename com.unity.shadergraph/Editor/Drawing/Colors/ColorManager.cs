using System;
using System.Collections.Generic;
using System.Linq;

namespace UnityEditor.ShaderGraph.Drawing.Colors
{
    // Use this to set colors on your node titles.
    // There are 2 methods of setting colors - direct Color objects via code (such as data saved in the node itself),
    // or setting classes on a VisualElement, allowing the colors themselves to be defined in USS. See notes on
    // IColorProvider for how to use these different methods.
    class ColorManager
    {
        public static string StyleFile = "ColorMode"; 
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

            foreach (var colorType in TypeCache.GetTypesDerivedFrom<IColorProvider>())
            {
                var provider = (IColorProvider) Activator.CreateInstance(colorType);
                m_Providers.Add(provider);
                if (provider.Title == activeColors)
                {
                    activeIndex = m_Providers.Count-1;
                }
            }
            
            m_Providers.Sort((p1, p2) =>  string.Compare(p1.Title, p2.Title, StringComparison.InvariantCulture));
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
}
