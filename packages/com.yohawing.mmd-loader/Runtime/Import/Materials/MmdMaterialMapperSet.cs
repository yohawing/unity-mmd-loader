#nullable enable

using System;
using System.Collections.Generic;
using Mmd.Rendering;
using UnityEngine;

namespace Mmd.UnityIntegration
{
    /// <summary>
    /// Creates one runtime-owned Unity material for an MMD material slot.
    /// The returned material is configured further by the loader and is destroyed with the model instance.
    /// </summary>
    public delegate Material MmdMaterialMapper(MmdMaterialDescriptor descriptor, Shader resolvedDefaultShader);

    /// <summary>
    /// Immutable material mapper selection with a default mapper and optional material-index overrides.
    /// This seam currently controls material creation only; texture and material-morph bindings remain loader-owned.
    /// </summary>
    public sealed class MmdMaterialMapperSet
    {
        private static readonly MmdMaterialMapper BuiltInMapper = CreateBuiltInMaterial;
        private readonly Dictionary<int, MmdMaterialMapper> _materialOverrides;

        public static MmdMaterialMapperSet BuiltIn { get; } = new MmdMaterialMapperSet(BuiltInMapper);

        public MmdMaterialMapper DefaultMapper { get; }

        public MmdMaterialMapperSet(MmdMaterialMapper defaultMapper)
            : this(defaultMapper, new Dictionary<int, MmdMaterialMapper>())
        {
        }

        private MmdMaterialMapperSet(
            MmdMaterialMapper defaultMapper,
            Dictionary<int, MmdMaterialMapper> materialOverrides)
        {
            DefaultMapper = defaultMapper ?? throw new ArgumentNullException(nameof(defaultMapper));
            _materialOverrides = materialOverrides;
        }

        public MmdMaterialMapperSet WithMaterialOverride(int materialIndex, MmdMaterialMapper mapper)
        {
            if (materialIndex < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(materialIndex));
            }

            if (mapper == null)
            {
                throw new ArgumentNullException(nameof(mapper));
            }

            var overrides = new Dictionary<int, MmdMaterialMapper>(_materialOverrides)
            {
                [materialIndex] = mapper
            };
            return new MmdMaterialMapperSet(DefaultMapper, overrides);
        }

        internal MmdMaterialMapper Resolve(int materialIndex)
        {
            return _materialOverrides.TryGetValue(materialIndex, out MmdMaterialMapper mapper)
                ? mapper
                : DefaultMapper;
        }

        private static Material CreateBuiltInMaterial(
            MmdMaterialDescriptor descriptor,
            Shader resolvedDefaultShader)
        {
            return new Material(resolvedDefaultShader);
        }
    }
}
