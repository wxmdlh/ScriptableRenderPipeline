using System;
using System.IO;
using Debug = UnityEngine.Debug;

namespace UnityEditor.ShaderGraph
{
    public class FileUtilities
    {
        public static bool WriteShaderGraphToDisk<T>(string path, T data, bool prettyPrint = false)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            CheckoutIfValid(path);

            try
            {
                File.WriteAllText(path, EditorJsonUtility.ToJson(data, prettyPrint));
            }
            catch (Exception e)
            {
                if (e.GetBaseException() is UnauthorizedAccessException &&
                    (File.GetAttributes(path) & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
                {
                        FileInfo fileInfo = new FileInfo(path);
                        fileInfo.IsReadOnly = false;
                        File.WriteAllText(path, EditorJsonUtility.ToJson(data, prettyPrint));
                        return true;
                }
                Debug.LogException(e);
                return false;
            }
            return true;
        }

        static void CheckoutIfValid(string path)
        {
            if (VersionControl.Provider.enabled && VersionControl.Provider.isActive)
            {
                var asset = VersionControl.Provider.GetAssetByPath(path);
                if (asset != null)
                {
                    if (VersionControl.Provider.CheckoutIsValid(asset))
                    {
                        var task = VersionControl.Provider.Checkout(asset, VersionControl.CheckoutMode.Both);
                        task.Wait();

                        if (!task.success)
                            Debug.Log(task.text + " " + task.resultCode);
                    }
                }
            }
        }
    }
}
