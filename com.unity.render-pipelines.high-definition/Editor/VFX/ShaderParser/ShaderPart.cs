using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System.Text.RegularExpressions;
using UnityEditor.ShaderGraph;

namespace UnityEditor.Experimental.Rendering.HDPipeline.VFXSG
{

    struct StencilParameters
    {
        public Dictionary<string, string> parameters;

        public static readonly string[] s_StencilParameters = { "ReadMask", "WriteMask", "Ref", "Comp", "Pass", "Fail", "ZFail" };
    }

    [Flags]
    enum ColorMask
    {
        R = 1 << 0,
        G = 1 << 1,
        B = 1 << 2,
        A = 1 << 3,
        RGB = R | G | B,
        All = R | G | B | A
    }

    struct ShaderParameters
    {
        public Dictionary<string, string> tags;
        public Dictionary<string, string> parameters;

        public static readonly string[] s_ShaderParameters = {"Cull", "ZTest", "ZWrite", "ZClip","ColorMask"};

        public StencilParameters stencilParameters;
    }
    class ShaderPart
    {
        public List<string> shaderCode;
        public ShaderParameters parameters;

        public void AddTag(string key,string value)
        {
            parameters.tags[key] = value;
        }

        public void ReplaceParameterVariables(Dictionary<string, string> guiVariables)
        {
            if (parameters.parameters != null)
            {
                foreach (var parameter in parameters.parameters.ToList())
                {
                    foreach (var variable in guiVariables)
                        if (parameter.Value == "[" + variable.Key + "]")
                        {
                            parameters.parameters[parameter.Key] = variable.Value;
                        }
                }
            }
            if (parameters.stencilParameters.parameters != null)
            {
                foreach (var parameter in parameters.stencilParameters.parameters.ToList())
                {
                    foreach (var variable in guiVariables)
                        if (parameter.Value == "[" + variable.Key + "]")
                        {
                            parameters.stencilParameters.parameters[parameter.Key] = variable.Value;
                        }
                }
            }
        }

        public int IndexOfLineMatching(string str)
        {
            Regex rx = new Regex(str,RegexOptions.Compiled);

            return shaderCode.FindIndex(t => rx.IsMatch(t));
        }

        public void InsertShaderLine(int index,string line)
        {
            if( index >= 0)
                shaderCode.Insert(index, line);
            else
                shaderCode.Add(line);
        }

        public void InsertShaderCode(int index, string newShaderCode)
        {
            if( index >= 0)
                shaderCode.InsertRange(index, UnIndent(newShaderCode));
            else
                shaderCode.AddRange(UnIndent(newShaderCode));
        }

        public void RemoveShaderCodeContaining(string shaderLine)
        {
            shaderCode.RemoveAll(t => t.Contains(shaderLine));
        }

        const string includeCommand = "#include";

        bool IsInclude(string line,string filePath)
        {
            int pos = 0;
            int length = line.Length;
            int filePathLength = filePath.Length;

            if (length < includeCommand.Length + 2 /* for quotes*/ + filePathLength)
                return false;

            while( pos < length - filePathLength - 2)
            {
                if (!char.IsWhiteSpace(line[pos]))
                    break;
                ++pos;
            }

            if (pos + includeCommand.Length >= length - filePathLength - 2)
                return false;

            if (string.Compare(line, pos, includeCommand, 0, includeCommand.Length, true) != 0)
                return false;

            pos += includeCommand.Length;

            while (pos < length)
            {
                if (!char.IsWhiteSpace(line[pos]))
                    break;
                ++pos;
            }

            if (line[pos] != '"')
                return false;
            int startPath = ++pos;

            while (pos < length)
            {
                if (line[pos] == '"')
                    break;
                ++pos;
            }

            if (pos >= length)
                return false;

            if (string.Compare(line, startPath, filePath, 0, filePath.Length) != 0)
                return false;

            return true;
        }


        public void ReplaceInclude(string filePath, string content)
        {
            if(shaderCode != null)
            {
                int includeIndex = shaderCode.FindIndex(t => IsInclude(t, filePath));

                if (includeIndex >= 0)
                {
                    shaderCode.RemoveAt(includeIndex);

                    shaderCode.InsertRange(includeIndex, UnIndent(content));
                }
            }
        }

        public virtual string shaderStartTag
        {
            get { return "HLSLINCLUDE"; }
        }
        protected static bool IsSame(string paramName, string document, RangeInt range)
        {
            return paramName.Length == range.length && string.Compare(document, range.start, paramName, 0, range.length, StringComparison.InvariantCultureIgnoreCase) == 0;
        }

        protected static bool IsAny(IEnumerable<string> paramNames, string document, RangeInt range)
        {
            return paramNames.Any(t => IsSame(t, document, range));
        }

        protected static int ParseParameter(string statement, RangeInt totalRange, out RangeInt nameRange, out RangeInt range)
        {
            range.start = 0;
            range.length = 0;
            nameRange.start = 0;
            nameRange.length = 0;
            int startIndex = totalRange.start;
            foreach (var charac in statement.Take(totalRange.end).Skip(totalRange.start))
            {
                if (!char.IsWhiteSpace(charac))
                    break;
                ++startIndex;
            }

            if (totalRange.end < startIndex + 1)
                return -2;

            int localEndIndex = startIndex + 1;

            foreach (var charac in statement.Take(totalRange.end).Skip(localEndIndex))
            {
                if (char.IsWhiteSpace(charac) || charac == '{' || charac == '"' || charac == '[')
                    break;
                ++localEndIndex;
            }

            if (totalRange.end < localEndIndex)
                return -2;

            nameRange.start = startIndex;
            nameRange.length = localEndIndex - startIndex;
            string name = statement.Substring(nameRange.start, nameRange.length);

            //This is a comment line starting with two slashes : skip the line
            if (nameRange.length >= 2 && statement[startIndex] == '/' && statement[startIndex + 1] == '/')
            {
                int lineStartIndex = localEndIndex+1;
                int lineEndIndex = lineStartIndex;
                foreach (var charac in statement.Take(totalRange.end).Skip(lineStartIndex))
                {
                    lineEndIndex++;
                    if (charac == '\n')
                        break;
                }
                range.start = lineStartIndex;
                range.length = lineEndIndex - lineStartIndex;
                return 0;
            }

            if( IsAny(startShaderName,statement,nameRange))
            {
                range.start = localEndIndex + 1;
                return 0;
            }
                    

            bool wasEscapeChar = false;
            bool inQuotes = false;
            bool noSurrounding = false;
            int bracketLevel = 0;

            int valueStartIndex = localEndIndex;
            int valueEndIndex = valueStartIndex;
            int dataStartIndex = 0;

            foreach ( var charac in statement.Take(totalRange.end).Skip(valueStartIndex))
            {
                if (noSurrounding && char.IsWhiteSpace(charac))
                    break;
                valueEndIndex++;

                if (inQuotes)
                {
                    if (charac == '\\')
                        wasEscapeChar = true;
                    else if (!wasEscapeChar && charac == '"')
                    {
                        inQuotes = false;
                        if (bracketLevel == 0)
                        {
                            break;
                        }
                    }
                }
                else if (charac == '"')
                {
                    if(dataStartIndex== 0)
                    {
                        dataStartIndex = valueEndIndex-1;
                    }
                    inQuotes = true;
                }
                if( ! inQuotes)
                {
                    if (charac == '{')
                    {
                        if (dataStartIndex == 0 && bracketLevel == 0)
                        {
                            dataStartIndex = valueEndIndex-1;
                        }
                        bracketLevel++;
                    }
                    if (charac == '}')
                    {
                        bracketLevel--;
                        if (bracketLevel == 0)
                            break;
                    }
                }
                if (!noSurrounding && !inQuotes && bracketLevel == 0 && !char.IsWhiteSpace(charac)) // Parameter without quote or bracket ( Cull ... )
                {
                    dataStartIndex = valueEndIndex - 1;
                    noSurrounding = true;
                }
            }

            range.start = dataStartIndex;
            range.length = valueEndIndex - dataStartIndex;

            return 0;
        }

        static protected List<string> UnIndent(string code)
        {
            //TODO rewrite in a non allocating way
            string[] lines = code.Split('\n');

            for(int i = 0; i < lines.Length; ++ i)
            {
                if( lines[i].IndexOf('\t') >= 0)
                    lines[i] = lines[i].Replace('\t', ' ');
            }

            int cptIndent = int.MaxValue;
            foreach (var line in lines)
            {
                int lineCptIndent = 0;
                if (line.Length == 0)
                    lineCptIndent = int.MaxValue;
                else
                    foreach (var charac in line)
                    {
                        if (charac == ' ')
                            lineCptIndent += 1;
                        else break;
                    }

                if (lineCptIndent < line.Length && lineCptIndent < cptIndent)
                    cptIndent = lineCptIndent;
            }
            if( cptIndent > 0)
            {
                for (int i = 0; i < lines.Length; ++i)
                {
                    if( lines[i].Length > cptIndent)
                        lines[i] = lines[i].Substring(cptIndent).TrimEnd();
                }
            }

            return lines.ToList();
        }


        protected int ParseContent(string document,RangeInt totalRange, RangeInt paramName,ref RangeInt param)
        {
            if (IsAny(startShaderName, document, paramName)) //START OF SHADER, end is not param but must find line with endShaderName
            {
                //Look for line with one of endShaderName

                int nextLine = paramName.end;
                bool found = false;
                int endLine = 0;
                int startLine = 0;
                do
                {
                    foreach (var charac in document.Take(totalRange.end).Skip(nextLine))
                    {
                        nextLine++;
                        if (charac == '\n')
                            break;
                    }
                    startLine = nextLine;
                    foreach (var charac in document.Take(totalRange.end).Skip(nextLine))
                    {
                        if (!char.IsWhiteSpace(charac))
                            break;

                        ++startLine;
                    }

                    endLine = startLine + 1;

                    foreach (var charac in document.Take(totalRange.end).Skip(endLine))
                    {
                        if (char.IsWhiteSpace(charac))
                            break;
                        endLine++;
                    }

                    if (IsAny(endShaderName, document, new RangeInt(startLine, endLine - startLine)))
                    {
                        found = true;
                        break;
                    }
                    nextLine = endLine;
                    foreach (var charac in document.Take(totalRange.end).Skip(nextLine))
                    {
                        if (charac == '\n')
                            break;
                        nextLine++;
                    }

                }
                while (!found);
                shaderCode = UnIndent(document.Substring(paramName.end, startLine - paramName.end));
                param.length = endLine - param.start;
            }
            else if (IsSame("Tags", document, paramName))
            {

                int error = ParseTags(document, param);
                if( error != 0)
                {
                    return 100 + error;
                }
            }
            else if( IsSame("Stencil",document,paramName))
            {
                int startIndex = document.IndexOf('{',param.start) + 1;
                if (startIndex == -1)
                    return -1;

                RangeInt stencilParamName;
                RangeInt stencilParam;

                int endIndex = param.end;
                while (document[endIndex] != '}')
                    endIndex--;
                endIndex--;

                while (ParseParameter(document, new RangeInt(startIndex, endIndex - startIndex), out stencilParamName, out stencilParam) == 0)
                {
                    foreach (var parameter in StencilParameters.s_StencilParameters)
                    {
                        if (IsSame(parameter, document, stencilParamName))
                        {
                            if (parameters.stencilParameters.parameters == null)
                                parameters.stencilParameters.parameters = new Dictionary<string, string>();
                            parameters.stencilParameters.parameters[parameter] = document.Substring(stencilParam.start, stencilParam.length);
                            break;
                        }
                    }
                    startIndex = stencilParam.end;
                }
            }
            else
            {
                foreach (var parameter in ShaderParameters.s_ShaderParameters)
                {
                    if (IsSame(parameter, document, paramName))
                    {
                        if (parameters.parameters == null)
                            parameters.parameters = new Dictionary<string, string>();
                        parameters.parameters[parameter] = document.Substring(param.start, param.length);
                        break;
                    }
                }
            }

            return 0;
        }

        int ParseTags(string document,RangeInt param)
        {
            if (document[param.start] == '{')
            {
                param.start++;
                param.length--;
            }
            if (document[param.start + param.length] == '}')
                param.length--;

            string key = null;

            int pos = param.start;

            while(pos < param.end)
            {
                foreach (char charac in document.Skip(pos).Take(param.length))
                {
                    if (!char.IsWhiteSpace(charac))
                        break;
                    ++pos;
                }
                if (document[pos] == '"')
                    pos++;
                int startSentence = pos;
                foreach (char charac in document.Skip(pos).Take(param.length))
                {
                    if (char.IsWhiteSpace(charac) || charac == '=')
                        break;
                    ++pos;
                }
                int endSentence = pos;
                if (document[endSentence-1] == '"')
                    endSentence--;

                if ( key == null)
                {
                    key = document.Substring(startSentence, endSentence - startSentence);

                    foreach (char charac in document.Skip(pos).Take(param.length))
                    {
                        ++pos;
                        if (charac == '=' )
                            break;
                    }
                }
                else
                {
                    if(parameters.tags == null)
                    {
                        parameters.tags = new Dictionary<string, string>();
                    }
                    parameters.tags[key] = document.Substring(startSentence, endSentence - startSentence);
                    key = null;
                }
            }

            return key == null ? 0 : -1;
        }

        static readonly string[] startShaderName = { "HLSLINCLUDE", "HLSLPROGRAM", "CGINCLUDE", "CGPROGRAM" };
        static readonly string[] endShaderName = { "ENDHLSL", "ENDCG" };


        protected void AppendContentTo(ShaderStringBuilder sb)
        {
            if(parameters.tags != null)
            {
                sb.AppendLine("Tags {"+ parameters.tags.Select(t=>string.Format("\"{0}\" = \"{1}\" ", t.Key, t.Value)).Aggregate((s,t)=> s + t) + '}');
            }

            if(parameters.parameters != null)
            {
                foreach (var parameter in ShaderParameters.s_ShaderParameters)
                {
                    string value;
                    if (parameters.parameters.TryGetValue(parameter, out value))
                    {
                        sb.AppendLine("{0} {1}", parameter, value);
                    }
                }
            }

            if (parameters.stencilParameters.parameters != null)
            {
                sb.AppendLine("Stencil");
                sb.AppendLine("{");
                sb.IncreaseIndent();
                foreach (var parameter in StencilParameters.s_StencilParameters)
                {
                    string value;
                    if (parameters.stencilParameters.parameters.TryGetValue(parameter, out value))
                    {
                        sb.AppendLine("{0} {1}", parameter, value);
                    }
                }
                sb.DecreaseIndent();
                sb.AppendLine("}");
            }

            if (shaderCode != null)
            {
                sb.AppendLine(shaderStartTag);
                foreach( var line in shaderCode)
                {
                    sb.AppendLine(line);
                }
                sb.AppendLine("ENDHLSL");
            }
        }
    }


}
