#nullable enable

using UnityEditor;

namespace Mmd.Editor
{
    [CustomEditor(typeof(MmdPmxAsset))]
    public sealed class MmdPmxAssetEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            // ScriptedImporterEditor owns the user-facing PMX UI. Keeping the
            // imported object inspector empty avoids a duplicate, read-only surface.
        }
    }
}
