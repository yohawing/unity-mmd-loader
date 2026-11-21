#nullable enable

using System.Collections.Generic;
using UnityEngine;

namespace Mmd.Tests
{
    /// <summary>
    /// Test-only holder for <see cref="MmdMorphDescriptorMovedFromCompatibilityTests"/>.
    /// The file name must match the class name so Unity resolves it to a MonoScript.
    /// </summary>
    public sealed class MmdMovedFromSerializeReferenceHolder : ScriptableObject
    {
        [SerializeReference]
        public List<object?> values = new();
    }
}
