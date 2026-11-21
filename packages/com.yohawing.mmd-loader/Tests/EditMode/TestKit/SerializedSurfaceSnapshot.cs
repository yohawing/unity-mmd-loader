#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEditor;
using UnityEngine;
using UnityEngine.Serialization;

namespace Mmd.Tests
{
    /// <summary>
    /// Deterministic text rendering of what Unity persists for an assembly's
    /// types: every UnityEngine.Object-derived class and every [Serializable]
    /// class/struct (public or not), with the fields Unity would serialize.
    /// Lines are prefixed with <c>[Assembly]</c> because [SerializeReference]
    /// data stores the assembly name. Field selection approximates Unity's
    /// rules: instance, non-const, non-readonly, and either public without
    /// [NonSerialized], or [SerializeField], or [SerializeReference]. Whether
    /// the field's type is itself serializable is not checked, so the snapshot
    /// errs on the side of pinning more fields.
    /// </summary>
    internal static class SerializedFieldSnapshot
    {
        private const BindingFlags DeclaredInstanceFields =
            BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        internal static List<string> Build(IEnumerable<Assembly> assemblies)
        {
            var lines = new List<string>();
            foreach (Assembly assembly in assemblies)
            {
                string prefix = "[" + assembly.GetName().Name + "] ";
                foreach (Type type in PublicApiSnapshot.LoadTypes(assembly))
                {
                    string? kind = SerializedKind(type);
                    if (kind == null)
                    {
                        continue;
                    }

                    string owner = prefix + PublicApiSnapshot.TypeDefinitionName(type) + " :: ";
                    string baseName = type.BaseType != null ? PublicApiSnapshot.FormatType(type.BaseType) : "none";
                    lines.Add(owner + "(type) " + kind + " base=" + baseName);

                    foreach (FieldInfo field in type.GetFields(DeclaredInstanceFields))
                    {
                        if (!IsSerializedField(field))
                        {
                            continue;
                        }

                        lines.Add(owner + field.Name + " : " + PublicApiSnapshot.FormatType(field.FieldType) + FieldAttributes(field));
                    }
                }
            }

            lines.Sort(StringComparer.Ordinal);
            return lines.Distinct(StringComparer.Ordinal).ToList();
        }

        /// <summary>
        /// Returns <c>UnityEngine.Object</c> or <c>Serializable</c> for types whose
        /// fields Unity persists, or null for every other type.
        /// </summary>
        internal static string? SerializedKind(Type type)
        {
            if (type.IsDefined(typeof(CompilerGeneratedAttribute), false) ||
                type.IsInterface || type.IsEnum || typeof(Delegate).IsAssignableFrom(type))
            {
                return null;
            }

            if (type.IsClass && typeof(UnityEngine.Object).IsAssignableFrom(type))
            {
                return "UnityEngine.Object";
            }

            bool isClassOrStruct = type.IsClass || (type.IsValueType && !type.IsPrimitive);
            return isClassOrStruct && (type.Attributes & TypeAttributes.Serializable) != 0
                ? "Serializable"
                : null;
        }

        private static bool IsSerializedField(FieldInfo field)
        {
            // [NonSerialized] wins over [SerializeField]/[SerializeReference] in Unity.
            if (field.IsStatic || field.IsLiteral || field.IsInitOnly || field.IsNotSerialized)
            {
                return false;
            }

            return field.IsPublic ||
                   field.IsDefined(typeof(SerializeReference), false) ||
                   field.IsDefined(typeof(SerializeField), false);
        }

        private static string FieldAttributes(FieldInfo field)
        {
            string result = string.Empty;
            if (field.IsDefined(typeof(SerializeReference), false))
            {
                result += " [SerializeReference]";
            }

            List<string> formerNames = field.GetCustomAttributes<FormerlySerializedAsAttribute>(false)
                .Select(attribute => attribute.oldName)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToList();
            if (formerNames.Count > 0)
            {
                result += " [FormerlySerializedAs=" + string.Join(",", formerNames) + "]";
            }

            return result;
        }
    }

    /// <summary>
    /// Pins the GUID of every MonoScript asset under the package's Runtime/ and
    /// Editor/ folders whose class resolves into one of the requested
    /// assemblies. Scenes, prefabs and assets reference scripts by this GUID,
    /// so moving or renaming a type must keep its MonoScript GUID.
    /// Lines render as <c>guid | type</c>. The asset path is deliberately not
    /// pinned: moving a script together with its .meta keeps every reference.
    /// </summary>
    internal static class MonoScriptGuidSnapshot
    {
        private static readonly string[] ScannedFolders = { "Runtime", "Editor" };

        internal static string PackageAssetPath
        {
            get
            {
                UnityEditor.PackageManager.PackageInfo? package =
                    UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(MonoScriptGuidSnapshot).Assembly);
                return package != null && !string.IsNullOrWhiteSpace(package.assetPath)
                    ? package.assetPath
                    : "Packages/com.yohawing.mmd-loader";
            }
        }

        internal static List<string> Build(IReadOnlyCollection<string> assemblyNames)
        {
            var lines = new List<string>();
            foreach ((string guid, string _, Type type) in EnumerateScripts(assemblyNames))
            {
                lines.Add(guid + " | " + PublicApiSnapshot.TypeDefinitionName(type));
            }

            lines.Sort(StringComparer.Ordinal);
            return lines.Distinct(StringComparer.Ordinal).ToList();
        }

        /// <summary>
        /// UnityEngine.Object-derived types in <paramref name="assemblies"/> that no
        /// MonoScript under Runtime/ or Editor/ resolves to (file name differs from
        /// the class name, nested, or generic). Diagnostic only.
        /// </summary>
        internal static List<string> FindUnityObjectTypesWithoutScript(IReadOnlyCollection<Assembly> assemblies)
        {
            var assemblyNames = new HashSet<string>(assemblies.Select(assembly => assembly.GetName().Name), StringComparer.Ordinal);
            var mapped = new HashSet<Type>(EnumerateScripts(assemblyNames).Select(entry => entry.Type));
            return assemblies
                .SelectMany(assembly => PublicApiSnapshot.LoadTypes(assembly)
                    .Where(type => SerializedFieldSnapshot.SerializedKind(type) == "UnityEngine.Object" && !mapped.Contains(type))
                    .Select(type => "[" + assembly.GetName().Name + "] " + PublicApiSnapshot.TypeDefinitionName(type)))
                .OrderBy(line => line, StringComparer.Ordinal)
                .ToList();
        }

        private static IEnumerable<(string Guid, string RelativePath, Type Type)> EnumerateScripts(IReadOnlyCollection<string> assemblyNames)
        {
            string packagePath = PackageAssetPath;
            string[] folders = ScannedFolders.Select(folder => packagePath + "/" + folder).ToArray();
            foreach (string guid in AssetDatabase.FindAssets("t:MonoScript", folders))
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                MonoScript? script = AssetDatabase.LoadAssetAtPath<MonoScript>(assetPath);
                Type? type = script != null ? script.GetClass() : null;
                if (type == null || !assemblyNames.Contains(type.Assembly.GetName().Name))
                {
                    continue;
                }

                yield return (guid, assetPath.Substring(packagePath.Length + 1), type);
            }
        }
    }
}
