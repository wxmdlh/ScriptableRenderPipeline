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
    class SubShaderPart : ShaderPart
    {
        List<PassPart> passes = new List<PassPart>();

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

        public void AppendTo(StringBuilder sb)
        {
            sb.AppendLine("SubShader");
            sb.AppendLine("{");
            base.AppendContentTo(sb);
            foreach(var pass in passes)
            {
                pass.AppendTo(sb);
            }
            sb.AppendLine("}");
        }
    }
}
