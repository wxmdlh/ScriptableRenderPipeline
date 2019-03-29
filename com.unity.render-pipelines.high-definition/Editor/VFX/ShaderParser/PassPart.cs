using UnityEngine;
using UnityEditor.ShaderGraph;

namespace UnityEditor.Experimental.Rendering.HDPipeline.VFXSG
{
    class PassPart : ShaderPart
    {
        public string name;
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
                if( IsSame("name",document,paramName))
                {
                    name = document.Substring(param.start + 1, param.length - 2);
                }
                else
                    base.ParseContent(document, new RangeInt(startIndex, endIndex - startIndex), paramName, ref param);
                startIndex = param.end;
            }


            return 0;
        }
        public void AppendTo(ShaderStringBuilder sb)
        {
            sb.AppendLine("Pass");
            sb.AppendLine("{");
            sb.IncreaseIndent();
            sb.AppendLine("name \"{0}\"",name);
            base.AppendContentTo(sb);
            sb.DecreaseIndent();
            sb.AppendLine("}");
        }
        public override string shaderStartTag
        {
            get { return "HLSLPROGRAM"; }
        }
    }
}
