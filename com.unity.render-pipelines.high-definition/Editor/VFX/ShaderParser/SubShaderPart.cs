using System.Collections.Generic;
using UnityEngine;
using UnityEditor.ShaderGraph;

namespace UnityEditor.Experimental.Rendering.HDPipeline.VFXSG
{
    class SubShaderPart : ShaderPart
    {
        public List<PassPart> passes = new List<PassPart>();

        public new void ReplaceInclude(string filePath, string content)
        {
            base.ReplaceInclude(filePath, content);

            foreach (var pass in passes)
                pass.ReplaceInclude(filePath, content);
        }
        void InsertShaderCodeInEachPass(int index, string shaderCode)
        {
            foreach (var pass in passes)
                InsertShaderCode(index, shaderCode);
        }
        public void RemoveShaderCodeInEachPassContaining(string shaderCode)
        {
            foreach (var pass in passes)
                RemoveShaderCodeContaining(shaderCode);
        }

            public int Parse(string document, RangeInt totalRange)
        {
            int startIndex = document.IndexOf('{',totalRange.start,totalRange.length);
            if (startIndex == -1)
                return -1;

            RangeInt paramName;
            RangeInt param;

            int endIndex = document.LastIndexOf('}',totalRange.end,totalRange.length);
            startIndex += 1; // skip '{' itself

            while (ParseParameter(document, new RangeInt(startIndex, endIndex - startIndex), out paramName, out param) == 0)
            {
                if (IsSame("Pass", document, paramName))
                {
                    PassPart pass = new PassPart();

                    if( pass.Parse(document,param) == 0)
                        passes.Add(pass);
                }
                else
                {
                    base.ParseContent(document, new RangeInt(startIndex, endIndex - startIndex), paramName, ref param);
                }
                startIndex = param.end;
            }


            return 0;
        }

        public void AppendTo(ShaderStringBuilder sb)
        {
            sb.AppendLine("SubShader");
            sb.AppendLine("{");
            sb.IncreaseIndent();
            base.AppendContentTo(sb);
            foreach(var pass in passes)
            {
                pass.AppendTo(sb);
            }
            sb.DecreaseIndent();
            sb.AppendLine("}");
        }
    }
}
