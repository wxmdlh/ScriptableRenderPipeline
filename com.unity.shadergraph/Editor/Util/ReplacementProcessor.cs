using System.Collections.Generic;
using UnityEngine;

namespace UnityEditor.ShaderGraph
{
    internal static class ReplacementProcessor
    {
        internal static void Precision(AbstractMaterialNode node, List<string> snippets)
        {
            for (int i = 0; i < snippets.Count; i++)
            {
                // TODO: Add proper precision > string extensions
                snippets[i] = snippets[i].Replace("$precision", "float");
            }
        }
    }
}
