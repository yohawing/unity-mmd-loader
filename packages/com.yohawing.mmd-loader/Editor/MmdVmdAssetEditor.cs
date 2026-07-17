#nullable enable

using UnityEditor;

namespace Mmd.Editor
{
    [CustomEditor(typeof(MmdVmdAsset))]
    public sealed class MmdVmdAssetEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            // ScriptedImporterEditor owns the user-facing VMD UI. Keeping the
            // imported object inspector empty avoids a duplicate, read-only surface.
        }
    }
}
