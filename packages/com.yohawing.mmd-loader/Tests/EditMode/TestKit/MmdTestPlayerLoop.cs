#nullable enable

using System;
using UnityEngine.LowLevel;

namespace Mmd.Tests
{
    /// <summary>
    /// PlayerLoop search helpers shared by the worker scheduler tests.
    /// </summary>
    internal static class MmdTestPlayerLoop
    {
        internal static PlayerLoopSystem FindNode(PlayerLoopSystem loop, Type type)
        {
            if (loop.type == type)
            {
                return loop;
            }

            PlayerLoopSystem[]? children = loop.subSystemList;
            if (children != null)
            {
                for (int i = 0; i < children.Length; i++)
                {
                    PlayerLoopSystem result = FindNode(children[i], type);
                    if (result.type == type)
                    {
                        return result;
                    }
                }
            }

            return default;
        }

        internal static int FindType(PlayerLoopSystem loop, Type type)
        {
            PlayerLoopSystem[] children = loop.subSystemList!;
            for (int i = 0; i < children.Length; i++)
            {
                if (children[i].type == type)
                {
                    return i;
                }
            }

            return -1;
        }

        internal static int CountType(PlayerLoopSystem loop, Type type)
        {
            int count = loop.type == type ? 1 : 0;
            PlayerLoopSystem[]? children = loop.subSystemList;
            if (children == null)
            {
                return count;
            }

            for (int i = 0; i < children.Length; i++)
            {
                count += CountType(children[i], type);
            }

            return count;
        }
    }
}
