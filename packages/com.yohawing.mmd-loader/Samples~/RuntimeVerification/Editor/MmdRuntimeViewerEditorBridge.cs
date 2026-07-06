#nullable enable

using UnityEditor;

namespace Mmd.Samples.RuntimeVerification.Editor
{
    [InitializeOnLoad]
    static class MmdRuntimeViewerEditorBridge
    {
        static MmdRuntimeViewerEditorBridge()
        {
            MmdRuntimeViewerController.BrowseFileOverride = (title, extension) =>
                EditorUtility.OpenFilePanel(title, string.Empty, extension);
        }
    }
}