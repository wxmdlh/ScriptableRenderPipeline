using System;
using UnityEditor;
using UnityEditor.Networking.PlayerConnection;

[InitializeOnLoad]
public static class ImageHandlerRegister
{
    private static readonly Guid s_MessageId = new Guid("40c7a8e2-ad5d-475f-8119-af022a13b84c");

    static ImageHandlerRegister()
    {
        EditorConnection.instance.Initialize();
        EditorConnection.instance.Register(s_MessageId, ImageHandler.instance.HandleFailedImageEvent);

        AssemblyReloadEvents.beforeAssemblyReload += Unregister;
    }

    private static void Unregister()
    {
        EditorConnection.instance.Unregister(s_MessageId, ImageHandler.instance.HandleFailedImageEvent);
        AssemblyReloadEvents.beforeAssemblyReload -= Unregister;
    }
}
