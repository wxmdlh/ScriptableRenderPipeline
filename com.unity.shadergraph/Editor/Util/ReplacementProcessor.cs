using System;
using System.Text;
using System.Linq;
using UnityEngine;

namespace UnityEditor.ShaderGraph
{
    internal static class ReplacementProcessor
    {
        internal static void CalculateReplacements(ShaderStringBuilder builder)
        {
            ReplacePrecision(builder);
        }

        private static void ReplacePrecision(ShaderStringBuilder builder)
        {
            ConcretePrecision GetPrecision()
            {
                if(builder.currentSource is AbstractMaterialNode node)
                {
                    return node.concretePrecision;
                }

                if(builder.currentSource is AbstractShaderProperty prop)
                {
                    return prop.concretePrecision;
                }

                return ConcretePrecision.Float;
            }

            builder.ReplaceInCurrentMapping("$precision", GetPrecision().ToShaderString());
        }
    }
}
