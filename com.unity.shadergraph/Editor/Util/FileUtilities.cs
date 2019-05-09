using System;
using System.IO;
using Debug = UnityEngine.Debug;

namespace UnityEditor.ShaderGraph
{
    public class FileUtilities
    {
        public static bool WriteShaderGraphToDisk<T>(string path, T data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            try
            {
                File.WriteAllText(path, EditorJsonUtility.ToJson(data, true));
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                return false;
            }
            return true;
        }
    }
}
