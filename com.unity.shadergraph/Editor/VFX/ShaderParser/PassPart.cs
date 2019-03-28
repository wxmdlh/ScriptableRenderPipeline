using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Globalization;
using System.Text;
using UnityEditor.Graphing;
using UnityEditor.Graphing.Util;
using UnityEngine;
using UnityEngine.Rendering;

namespace UnityEditor.ShaderGraph.VFX
{
    class PassPart : ShaderPart
    {
        protected string name;
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
