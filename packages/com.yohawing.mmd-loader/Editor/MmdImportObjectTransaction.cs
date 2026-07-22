#nullable enable

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Mmd.Editor
{
    internal sealed class MmdImportObjectTransaction : IDisposable
    {
        private readonly Dictionary<int, Object> pending = new Dictionary<int, Object>();
        private readonly HashSet<int> hierarchyRoots = new HashSet<int>();
        private readonly HashSet<int> transferred = new HashSet<int>();
        private bool completed;

        internal void Track(Object? value, bool hierarchyRoot = false)
        {
            if (value == null)
            {
                return;
            }

            if (EditorUtility.IsPersistent(value))
            {
                throw new InvalidOperationException(
                    $"Importer transactions cannot own persistent project assets: {value.name}.");
            }

            int instanceId = value.GetInstanceID();
            pending[instanceId] = value;
            if (hierarchyRoot)
            {
                hierarchyRoots.Add(instanceId);
            }
        }

        internal void AdoptIntoHierarchy(GameObject childRoot)
        {
            if (childRoot == null)
            {
                return;
            }

            int instanceId = childRoot.GetInstanceID();
            pending.Remove(instanceId);
            hierarchyRoots.Remove(instanceId);
            transferred.Remove(instanceId);
        }

        internal void Discard(Object? value)
        {
            if (value == null)
            {
                return;
            }

            int instanceId = value.GetInstanceID();
            if (!pending.TryGetValue(instanceId, out Object trackedValue))
            {
                throw new InvalidOperationException(
                    $"Object '{value.name}' must be tracked before it can be discarded.");
            }
            if (transferred.Contains(instanceId))
            {
                throw new InvalidOperationException(
                    $"Object '{value.name}' cannot be discarded after it was transferred to an import context.");
            }

            pending.Remove(instanceId);
            hierarchyRoots.Remove(instanceId);
            transferred.Remove(instanceId);
            if (trackedValue != null)
            {
                Object.DestroyImmediate(trackedValue);
            }
        }

        internal void TransferToContext(AssetImportContext context, string identifier, Object value)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            int instanceId = value.GetInstanceID();
            if (!pending.ContainsKey(instanceId) || transferred.Contains(instanceId))
            {
                throw new InvalidOperationException(
                    $"Object '{value.name}' must be tracked exactly once before it is added to an import context.");
            }

            context.AddObjectToAsset(identifier, value);
            transferred.Add(instanceId);
        }

        internal void Complete()
        {
            if (pending.Count != transferred.Count)
            {
                throw new InvalidOperationException(
                    $"Importer transaction completed with {pending.Count - transferred.Count} object(s) that were not transferred.");
            }

            pending.Clear();
            hierarchyRoots.Clear();
            transferred.Clear();
            completed = true;
        }

        public void Dispose()
        {
            if (completed)
            {
                return;
            }

            foreach (int instanceId in hierarchyRoots)
            {
                if (pending.TryGetValue(instanceId, out Object root) && root != null)
                {
                    Object.DestroyImmediate(root);
                }
                pending.Remove(instanceId);
            }

            foreach (Object value in pending.Values)
            {
                if (value != null)
                {
                    Object.DestroyImmediate(value);
                }
            }

            pending.Clear();
            hierarchyRoots.Clear();
            transferred.Clear();
        }
    }

    internal enum MmdPmxImportStage
    {
        AssetCacheCreated,
        ProjectTexturesBound,
        ImportedAssetCreated,
        HumanoidAvatarCreated,
        HumanoidProxyParented,
        GenericAvatarCreated,
        HierarchyConfigured,
        BeforeSubAssetRegistration,
        PmxSubAssetRegistered,
        MaterialsRegistered,
        HierarchyRegistered,
        MainObjectSet
    }

    internal static class MmdPmxImportFaultInjection
    {
        private static string targetAssetPath = string.Empty;
        private static MmdPmxImportStage? targetStage;

        internal static IDisposable FailAt(string assetPath, MmdPmxImportStage stage)
        {
            if (targetStage.HasValue)
            {
                throw new InvalidOperationException("A PMX importer fault injection scope is already active.");
            }

            targetAssetPath = assetPath ?? string.Empty;
            targetStage = stage;
            return new Scope();
        }

        internal static void ThrowIfRequested(string assetPath, MmdPmxImportStage stage)
        {
            if (targetStage == stage && string.Equals(targetAssetPath, assetPath, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"Injected PMX importer failure at stage '{stage}'.");
            }
        }

        private sealed class Scope : IDisposable
        {
            public void Dispose()
            {
                targetStage = null;
                targetAssetPath = string.Empty;
            }
        }
    }
}
