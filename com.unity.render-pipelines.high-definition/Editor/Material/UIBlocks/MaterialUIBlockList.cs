using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering.HDPipeline;
using System.Linq;

namespace UnityEditor.Experimental.Rendering.HDPipeline
{
    public class MaterialUIBlockList : List<MaterialUIBlock>
    {
        [System.NonSerialized]
        bool        m_Initialized = false;

        Material[]  m_Materials;

        public Material[] materials => m_Materials;

        public void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
        {
            if (!m_Initialized)
            {
                foreach (var uiBlock in this)
                    uiBlock.Initialize(materialEditor, properties);

                m_Materials = materialEditor.targets.Select(target => target as Material).ToArray();

                m_Initialized = true;
            }
            foreach (var uiBlock in this)
                uiBlock.OnGUI();
        }
    }
}