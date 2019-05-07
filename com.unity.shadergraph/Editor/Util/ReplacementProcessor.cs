using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace UnityEditor.ShaderGraph
{
    internal static class ReplacementProcessor
    {
        internal static void Precision(object source, List<string> snippets)
        {
            if(source is PropertyNode propertyNode)
            {
                var property = propertyNode.owner.properties.FirstOrDefault(x => x.guid == propertyNode.propertyGuid);
                for (int i = 0; i < snippets.Count; i++)
                    snippets[i] = snippets[i].Replace("$precision", property.concretePrecision.ToShaderString());
                return;
            }

            if(source is AbstractMaterialNode node)
            {
                for (int i = 0; i < snippets.Count; i++)
                    snippets[i] = snippets[i].Replace("$precision", node.concretePrecision.ToShaderString());
                return;
            }

            if(source is AbstractShaderProperty prop)
            {
                for (int i = 0; i < snippets.Count; i++)
                    snippets[i] = snippets[i].Replace("$precision", prop.concretePrecision.ToShaderString());
                return;
            }
        }
    }
}
