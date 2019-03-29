using System.Collections.Generic;
using UnityEngine;
using UnityEditor.ShaderGraph;

namespace UnityEditor.Experimental.Rendering.HDPipeline.VFXSG
{
    class ShaderDocument : ShaderPart
    {
        public IEnumerable<PassPart> passes
        {
            get
            {
                foreach( var subShader in subShaders)
                {
                    foreach (var pass in subShader.passes)
                        yield return pass;
                }
            }
        }


        public new void ReplaceParameterVariables(Dictionary<string, string> guiVariables)
        {
            base.ReplaceParameterVariables(guiVariables);
            foreach (var subShader in subShaders)
            {
                subShader.ReplaceParameterVariables(guiVariables);
                foreach (var pass in subShader.passes)
                    pass.ReplaceParameterVariables(guiVariables);
            }
        }


        protected string name;
        List<SubShaderPart> subShaders = new List<SubShaderPart>();

        public void InsertShaderCodeInEachPass(int index,string shaderCode)
        {
            foreach (var subShader in subShaders)
                InsertShaderCodeInEachPass(index, shaderCode);
        }

        public void RemoveShaderCodeInEachPassContaining(string shaderCode)
        {
            foreach (var subShader in subShaders)
                RemoveShaderCodeInEachPassContaining(shaderCode);
        }

        public new void ReplaceInclude(string filePath, string content)
        {
            base.ReplaceInclude(filePath, content);

            foreach (var subShader in subShaders)
                subShader.ReplaceInclude(filePath, content);
        }

        public string ToString(bool includeName = true)
        {
            var sb = new ShaderStringBuilder();
            if(includeName)
                sb.AppendLine("Shader \"" +name+'"');
            sb.AppendLine("{");
            sb.IncreaseIndent();

            base.AppendContentTo(sb);

            foreach(var subShader in subShaders)
            {
                subShader.AppendTo(sb);
            }
            sb.IncreaseIndent();
            sb.AppendLine("}");

            return sb.ToString();
        }

        public int Parse(string document)
        {
            int startIndex = document.IndexOf('{');
            if (startIndex == -1)
                return -1;

            RangeInt paramName;
            RangeInt param;

            int error = ParseParameter(document, new RangeInt(0, startIndex), out paramName, out param);
            if( error != 0)
            {
                return 1000 + error;
            }
            if (!IsSame("Shader", document, paramName))
                return -2;

            name = document.Substring(param.start+1, param.length-2);

            int endIndex = document.LastIndexOf('}');
            startIndex += 1; // skip '{' itself

            while( ParseParameter(document,new RangeInt(startIndex, endIndex - startIndex),out paramName, out param) == 0)
            {
                if (IsSame("Properties", document, paramName))
                {
                    // ignore properties Block we don't need it in our case
                }
                else if( IsSame("SubShader",document,paramName))
                {
                    SubShaderPart subShader = new SubShaderPart();

                    if( subShader.Parse(document,param) == 0)
                        subShaders.Add(subShader);
                }
                else
                {
                    base.ParseContent(document, new RangeInt(startIndex, endIndex - startIndex), paramName, ref param);
                        
                }
                startIndex = param.end;
            }


            return 0;
        }

    }
}
