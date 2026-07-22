#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
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
    /// Declares the texture properties owned by a material mapper. Empty optional targets explicitly
    /// mean that the mapped shader does not support that MMD texture feature.
    /// </summary>
    public sealed class MmdMaterialTextureTargets
    {
        private readonly IReadOnlyList<string> _diffuseTextureProperties;

        public static MmdMaterialTextureTargets BuiltIn { get; } = new MmdMaterialTextureTargets(
            new[] { "_BaseMap", "_MainTex" },
            sphereTextureProperty: "_SphereMap",
            toonTextureProperty: "_ToonMap",
            diffuseTextureBoundProperty: "_BaseMapBound",
            sphereModeProperty: "_SphereMode",
            toonTextureBoundProperty: "_ToonMapBound");

        public IReadOnlyList<string> DiffuseTextureProperties => _diffuseTextureProperties;

        public string SphereTextureProperty { get; }

        public string ToonTextureProperty { get; }

        public string DiffuseTextureBoundProperty { get; }

        public string SphereModeProperty { get; }

        public string ToonTextureBoundProperty { get; }

        public MmdMaterialTextureTargets(
            IEnumerable<string>? diffuseTextureProperties,
            string? sphereTextureProperty = null,
            string? toonTextureProperty = null,
            string? diffuseTextureBoundProperty = null,
            string? sphereModeProperty = null,
            string? toonTextureBoundProperty = null)
        {
            _diffuseTextureProperties = Array.AsReadOnly(
                (diffuseTextureProperties ?? Array.Empty<string>())
                    .Where(property => !string.IsNullOrWhiteSpace(property))
                    .Distinct(StringComparer.Ordinal)
                    .ToArray());
            SphereTextureProperty = NormalizeOptionalProperty(sphereTextureProperty);
            ToonTextureProperty = NormalizeOptionalProperty(toonTextureProperty);
            DiffuseTextureBoundProperty = NormalizeOptionalProperty(diffuseTextureBoundProperty);
            SphereModeProperty = NormalizeOptionalProperty(sphereModeProperty);
            ToonTextureBoundProperty = NormalizeOptionalProperty(toonTextureBoundProperty);
        }

        private static string NormalizeOptionalProperty(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value;
        }
    }

    /// <summary>
    /// Immutable material mapper selection with a default mapper and optional material-index overrides.
    /// Material creation and texture destinations are mapper-owned; texture loading remains loader-owned.
    /// </summary>
    public sealed class MmdMaterialMapperSet
    {
        private static readonly MmdMaterialMapper BuiltInMapper = CreateBuiltInMaterial;
        private readonly Dictionary<int, MmdMaterialMapperRegistration> _materialOverrides;

        public static MmdMaterialMapperSet BuiltIn { get; } = new MmdMaterialMapperSet(
            BuiltInMapper,
            MmdMaterialTextureTargets.BuiltIn);

        public MmdMaterialMapper DefaultMapper { get; }

        public MmdMaterialTextureTargets DefaultTextureTargets { get; }

        public MmdMaterialMapperSet(MmdMaterialMapper defaultMapper)
            : this(defaultMapper, MmdMaterialTextureTargets.BuiltIn)
        {
        }

        public MmdMaterialMapperSet(
            MmdMaterialMapper defaultMapper,
            MmdMaterialTextureTargets defaultTextureTargets)
            : this(
                defaultMapper,
                defaultTextureTargets,
                new Dictionary<int, MmdMaterialMapperRegistration>())
        {
        }

        private MmdMaterialMapperSet(
            MmdMaterialMapper defaultMapper,
            MmdMaterialTextureTargets defaultTextureTargets,
            Dictionary<int, MmdMaterialMapperRegistration> materialOverrides)
        {
            DefaultMapper = defaultMapper ?? throw new ArgumentNullException(nameof(defaultMapper));
            DefaultTextureTargets = defaultTextureTargets ?? throw new ArgumentNullException(nameof(defaultTextureTargets));
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

            return WithMaterialOverride(materialIndex, mapper, DefaultTextureTargets);
        }

        public MmdMaterialMapperSet WithMaterialOverride(
            int materialIndex,
            MmdMaterialMapper mapper,
            MmdMaterialTextureTargets textureTargets)
        {
            if (materialIndex < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(materialIndex));
            }

            if (mapper == null)
            {
                throw new ArgumentNullException(nameof(mapper));
            }

            if (textureTargets == null)
            {
                throw new ArgumentNullException(nameof(textureTargets));
            }

            var overrides = new Dictionary<int, MmdMaterialMapperRegistration>(_materialOverrides)
            {
                [materialIndex] = new MmdMaterialMapperRegistration(mapper, textureTargets)
            };
            return new MmdMaterialMapperSet(DefaultMapper, DefaultTextureTargets, overrides);
        }

        internal MmdMaterialMapperRegistration Resolve(int materialIndex)
        {
            return _materialOverrides.TryGetValue(materialIndex, out MmdMaterialMapperRegistration registration)
                ? registration
                : new MmdMaterialMapperRegistration(DefaultMapper, DefaultTextureTargets);
        }

        private static Material CreateBuiltInMaterial(
            MmdMaterialDescriptor descriptor,
            Shader resolvedDefaultShader)
        {
            return new Material(resolvedDefaultShader);
        }
    }

    internal readonly struct MmdMaterialMapperRegistration
    {
        public MmdMaterialMapperRegistration(
            MmdMaterialMapper mapper,
            MmdMaterialTextureTargets textureTargets)
        {
            Mapper = mapper;
            TextureTargets = textureTargets;
        }

        public MmdMaterialMapper Mapper { get; }

        public MmdMaterialTextureTargets TextureTargets { get; }
    }
}
