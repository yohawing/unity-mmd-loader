#if UNITY_2021_1_OR_NEWER || UNITY_EDITOR
using UnityEngine;
#else
using System.Text.Json;
using System.Text.Json.Serialization;
#endif
using System;

namespace Mmd
{
    public static class MmdPlaybackSnapshotDumper
    {
        public static string ToJson(MmdPlaybackSnapshot snapshot, bool prettyPrint = true)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

#if UNITY_2021_1_OR_NEWER || UNITY_EDITOR
            return JsonUtility.ToJson(snapshot, prettyPrint);
#else
            var options = new JsonSerializerOptions
            {
                WriteIndented = prettyPrint,
                IncludeFields = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };
            return JsonSerializer.Serialize(snapshot, options);
#endif
        }
    }
}
