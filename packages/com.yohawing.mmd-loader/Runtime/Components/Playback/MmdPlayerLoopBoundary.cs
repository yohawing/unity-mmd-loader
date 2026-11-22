#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine.LowLevel;
using UnityEngine.PlayerLoop;

namespace Mmd.UnityIntegration
{
    /// <summary>
    /// Pure PlayerLoop tree transformation shared by the worker schedulers. Marker systems are
    /// placed immediately before and/or after one exact anchor under <see cref="Update"/>. The
    /// input tree is never mutated, and a tree that cannot be patched exactly is rejected instead
    /// of being partially patched.
    /// </summary>
    internal static class MmdPlayerLoopBoundary
    {
        internal readonly struct Spec
        {
            internal Spec(
                Type anchorType,
                Type? beforeMarkerType,
                PlayerLoopSystem.UpdateFunction? beforeUpdate,
                Type? afterMarkerType,
                PlayerLoopSystem.UpdateFunction? afterUpdate)
            {
                if (anchorType == null)
                {
                    throw new ArgumentNullException(nameof(anchorType));
                }

                if (beforeMarkerType == null && afterMarkerType == null)
                {
                    throw new ArgumentException("At least one boundary marker is required.");
                }

                if ((beforeMarkerType == null) != (beforeUpdate == null) ||
                    (afterMarkerType == null) != (afterUpdate == null))
                {
                    throw new ArgumentException("Each boundary marker requires exactly one update delegate.");
                }

                AnchorType = anchorType;
                BeforeMarkerType = beforeMarkerType;
                BeforeUpdate = beforeUpdate;
                AfterMarkerType = afterMarkerType;
                AfterUpdate = afterUpdate;
            }

            internal Type AnchorType { get; }

            internal Type? BeforeMarkerType { get; }

            internal PlayerLoopSystem.UpdateFunction? BeforeUpdate { get; }

            internal Type? AfterMarkerType { get; }

            internal PlayerLoopSystem.UpdateFunction? AfterUpdate { get; }
        }

        internal static bool TryPatch(
            PlayerLoopSystem current,
            in Spec spec,
            out PlayerLoopSystem patched)
        {
            if (current.type == typeof(Update))
            {
                patched = current;
                if (!TryPatchUpdate(current.subSystemList, spec, out PlayerLoopSystem[] children))
                {
                    return false;
                }

                patched.subSystemList = children;
                return true;
            }

            PlayerLoopSystem[]? currentChildren = current.subSystemList;
            if (currentChildren != null)
            {
                for (int i = 0; i < currentChildren.Length; i++)
                {
                    if (!TryPatch(currentChildren[i], spec, out PlayerLoopSystem childPatched))
                    {
                        continue;
                    }

                    var children = (PlayerLoopSystem[])currentChildren.Clone();
                    children[i] = childPatched;
                    patched = current;
                    patched.subSystemList = children;
                    return true;
                }
            }

            patched = current;
            return false;
        }

        internal static bool IsInstalled(PlayerLoopSystem current, in Spec spec)
        {
            if (current.type == typeof(Update))
            {
                PlayerLoopSystem[]? children = current.subSystemList;
                if (children == null)
                {
                    return false;
                }

                int anchorIndex = FindType(children, spec.AnchorType);
                if (anchorIndex < 0)
                {
                    return false;
                }

                if (spec.BeforeMarkerType != null)
                {
                    int beforeIndex = FindType(children, spec.BeforeMarkerType);
                    if (beforeIndex < 0 || beforeIndex + 1 != anchorIndex ||
                        children[beforeIndex].updateDelegate == null)
                    {
                        return false;
                    }
                }

                if (spec.AfterMarkerType != null)
                {
                    int afterIndex = FindType(children, spec.AfterMarkerType);
                    if (afterIndex < 0 || afterIndex != anchorIndex + 1 ||
                        children[afterIndex].updateDelegate == null)
                    {
                        return false;
                    }
                }

                return true;
            }

            PlayerLoopSystem[]? currentChildren = current.subSystemList;
            if (currentChildren == null)
            {
                return false;
            }

            for (int i = 0; i < currentChildren.Length; i++)
            {
                if (IsInstalled(currentChildren[i], spec))
                {
                    return true;
                }
            }

            return false;
        }

        internal static int FindType<T>(IReadOnlyList<PlayerLoopSystem> systems)
        {
            return FindType(systems, typeof(T));
        }

        internal static int FindType(IReadOnlyList<PlayerLoopSystem> systems, Type? type)
        {
            if (type == null)
            {
                return -1;
            }

            for (int i = 0; i < systems.Count; i++)
            {
                if (systems[i].type == type)
                {
                    return i;
                }
            }

            return -1;
        }

        private static bool TryPatchUpdate(
            PlayerLoopSystem[]? current,
            in Spec spec,
            out PlayerLoopSystem[] patched)
        {
            patched = Array.Empty<PlayerLoopSystem>();
            if (current == null)
            {
                return false;
            }

            int beforeIndex = FindType(current, spec.BeforeMarkerType);
            int afterIndex = FindType(current, spec.AfterMarkerType);
            int anchorIndex = FindType(current, spec.AnchorType);
            if (anchorIndex < 0)
            {
                return false;
            }

            if (beforeIndex >= 0 || afterIndex >= 0)
            {
                // An existing installation is only repaired when every marker is still exactly
                // adjacent to the anchor; any other shape is rejected rather than guessed at.
                if (spec.BeforeMarkerType != null && (beforeIndex < 0 || beforeIndex + 1 != anchorIndex))
                {
                    return false;
                }

                if (spec.AfterMarkerType != null && (afterIndex < 0 || afterIndex != anchorIndex + 1))
                {
                    return false;
                }

                var repaired = (PlayerLoopSystem[])current.Clone();
                if (beforeIndex >= 0)
                {
                    repaired[beforeIndex].updateDelegate = spec.BeforeUpdate;
                }

                if (afterIndex >= 0)
                {
                    repaired[afterIndex].updateDelegate = spec.AfterUpdate;
                }

                patched = repaired;
                return true;
            }

            var children = new List<PlayerLoopSystem>(current);
            if (spec.BeforeMarkerType != null)
            {
                children.Insert(
                    anchorIndex,
                    new PlayerLoopSystem
                    {
                        type = spec.BeforeMarkerType,
                        updateDelegate = spec.BeforeUpdate
                    });
                anchorIndex++;
            }

            if (spec.AfterMarkerType != null)
            {
                children.Insert(
                    anchorIndex + 1,
                    new PlayerLoopSystem
                    {
                        type = spec.AfterMarkerType,
                        updateDelegate = spec.AfterUpdate
                    });
            }

            patched = children.ToArray();
            return true;
        }
    }
}
