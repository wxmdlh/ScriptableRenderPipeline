using System.Collections.Generic;
using UnityEngine;

namespace UnityEditor.ShaderGraph
{
    internal static class ReplacementProcessor
    {
        internal static void Precision(AbstractMaterialNode node, List<string> snippets)
        {
            if(node == null)
                return;

            for (int i = 0; i < snippets.Count; i++)
            {
                snippets[i] = snippets[i].Replace("$precision", node.concretePrecision.ToShaderString());
            }
        }
    }
}
