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
    /// Declares the material properties and render-state capabilities owned by a material mapper.
    /// Empty properties explicitly mean that the mapped shader does not support that write target.
    /// </summary>
    public sealed class MmdMaterialRenderingTargets
    {
        public static MmdMaterialRenderingTargets BuiltIn { get; } = new MmdMaterialRenderingTargets(
            validatePropertyPresence: false);

        private readonly IReadOnlyList<string> _unsupportedFeatures;

        public string BaseColorProperty { get; }

        public string ColorProperty { get; }

        public string AmbientColorProperty { get; }

        public string AlphaProperty { get; }

        public string AlphaClipThresholdProperty { get; }

        public string ShadowAlphaClipThresholdProperty { get; }

        public string TextureAlphaOutputWeightProperty { get; }

        public string CullProperty { get; }

        public string SurfaceProperty { get; }

        public string BlendProperty { get; }

        public string SourceBlendProperty { get; }

        public string DestinationBlendProperty { get; }

        public string ZWriteProperty { get; }

        public string OutlineColorProperty { get; }

        public string OutlineWidthProperty { get; }

        public string OutlineVisibleProperty { get; }

        public string OutlineScreenSpaceWeightProperty { get; }

        public string OutlineZTestProperty { get; }

        public bool SupportsRenderQueue { get; }

        /// <summary>
        /// Feature identifiers that the mapper intentionally does not implement for this shader.
        /// The loader preserves these declarations in per-material binding diagnostics so a mixed
        /// mapper set can explain which MMD features were dropped.
        /// </summary>
        public IReadOnlyList<string> UnsupportedFeatures => _unsupportedFeatures;

        /// <summary>
        /// Enables diagnostics when a non-empty declared rendering property is absent from the
        /// resolved shader. Built-in targets disable this because their optional property aliases
        /// intentionally cover several shader families.
        /// </summary>
        public bool ValidatePropertyPresence { get; }

        public MmdMaterialRenderingTargets(
            string? baseColorProperty = "_BaseColor",
            string? colorProperty = "_Color",
            string? ambientColorProperty = "_AmbientColor",
            string? alphaProperty = "_Alpha",
            string? alphaClipThresholdProperty = "_AlphaClipThreshold",
            string? shadowAlphaClipThresholdProperty = "_ShadowAlphaClipThreshold",
            string? textureAlphaOutputWeightProperty = "_TextureAlphaOutputWeight",
            string? cullProperty = "_Cull",
            string? surfaceProperty = "_Surface",
            string? blendProperty = "_Blend",
            string? sourceBlendProperty = "_SrcBlend",
            string? destinationBlendProperty = "_DstBlend",
            string? zWriteProperty = "_ZWrite",
            string? outlineColorProperty = "_OutlineColor",
            string? outlineWidthProperty = "_OutlineWidth",
            string? outlineVisibleProperty = "_OutlineVisible",
            string? outlineScreenSpaceWeightProperty = "_OutlineScreenSpaceWeight",
            string? outlineZTestProperty = "_OutlineZTest",
            bool supportsRenderQueue = true,
            IEnumerable<string>? unsupportedFeatures = null,
            bool validatePropertyPresence = true)
        {
            BaseColorProperty = NormalizeOptionalProperty(baseColorProperty);
            ColorProperty = NormalizeOptionalProperty(colorProperty);
            AmbientColorProperty = NormalizeOptionalProperty(ambientColorProperty);
            AlphaProperty = NormalizeOptionalProperty(alphaProperty);
            AlphaClipThresholdProperty = NormalizeOptionalProperty(alphaClipThresholdProperty);
            ShadowAlphaClipThresholdProperty = NormalizeOptionalProperty(shadowAlphaClipThresholdProperty);
            TextureAlphaOutputWeightProperty = NormalizeOptionalProperty(textureAlphaOutputWeightProperty);
            CullProperty = NormalizeOptionalProperty(cullProperty);
            SurfaceProperty = NormalizeOptionalProperty(surfaceProperty);
            BlendProperty = NormalizeOptionalProperty(blendProperty);
            SourceBlendProperty = NormalizeOptionalProperty(sourceBlendProperty);
            DestinationBlendProperty = NormalizeOptionalProperty(destinationBlendProperty);
            ZWriteProperty = NormalizeOptionalProperty(zWriteProperty);
            OutlineColorProperty = NormalizeOptionalProperty(outlineColorProperty);
            OutlineWidthProperty = NormalizeOptionalProperty(outlineWidthProperty);
            OutlineVisibleProperty = NormalizeOptionalProperty(outlineVisibleProperty);
            OutlineScreenSpaceWeightProperty = NormalizeOptionalProperty(outlineScreenSpaceWeightProperty);
            OutlineZTestProperty = NormalizeOptionalProperty(outlineZTestProperty);
            SupportsRenderQueue = supportsRenderQueue;
            _unsupportedFeatures = Array.AsReadOnly(
                (unsupportedFeatures ?? Array.Empty<string>())
                    .Where(feature => !string.IsNullOrWhiteSpace(feature))
                    .Select(feature => feature.Trim())
                    .Distinct(StringComparer.Ordinal)
                    .ToArray());
            ValidatePropertyPresence = validatePropertyPresence;
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
            MmdMaterialTextureTargets.BuiltIn,
            MmdMaterialRenderingTargets.BuiltIn);

        public MmdMaterialMapper DefaultMapper { get; }

        public MmdMaterialTextureTargets DefaultTextureTargets { get; }

        public MmdMaterialRenderingTargets DefaultRenderingTargets { get; }

        public MmdMaterialMapperSet(MmdMaterialMapper defaultMapper)
            : this(defaultMapper, MmdMaterialTextureTargets.BuiltIn)
        {
        }

        public MmdMaterialMapperSet(
            MmdMaterialMapper defaultMapper,
            MmdMaterialTextureTargets defaultTextureTargets)
            : this(defaultMapper, defaultTextureTargets, MmdMaterialRenderingTargets.BuiltIn)
        {
        }

        public MmdMaterialMapperSet(
            MmdMaterialMapper defaultMapper,
            MmdMaterialTextureTargets defaultTextureTargets,
            MmdMaterialRenderingTargets defaultRenderingTargets)
            : this(
                defaultMapper,
                defaultTextureTargets,
                defaultRenderingTargets,
                new Dictionary<int, MmdMaterialMapperRegistration>())
        {
        }

        private MmdMaterialMapperSet(
            MmdMaterialMapper defaultMapper,
            MmdMaterialTextureTargets defaultTextureTargets,
            MmdMaterialRenderingTargets defaultRenderingTargets,
            Dictionary<int, MmdMaterialMapperRegistration> materialOverrides)
        {
            DefaultMapper = defaultMapper ?? throw new ArgumentNullException(nameof(defaultMapper));
            DefaultTextureTargets = defaultTextureTargets ?? throw new ArgumentNullException(nameof(defaultTextureTargets));
            DefaultRenderingTargets = defaultRenderingTargets ?? throw new ArgumentNullException(nameof(defaultRenderingTargets));
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

            return WithMaterialOverride(materialIndex, mapper, DefaultTextureTargets, DefaultRenderingTargets);
        }

        public MmdMaterialMapperSet WithMaterialOverride(
            int materialIndex,
            MmdMaterialMapper mapper,
            MmdMaterialTextureTargets textureTargets)
        {
            return WithMaterialOverride(materialIndex, mapper, textureTargets, DefaultRenderingTargets);
        }

        public MmdMaterialMapperSet WithMaterialOverride(
            int materialIndex,
            MmdMaterialMapper mapper,
            MmdMaterialTextureTargets textureTargets,
            MmdMaterialRenderingTargets renderingTargets)
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

            if (renderingTargets == null)
            {
                throw new ArgumentNullException(nameof(renderingTargets));
            }

            var overrides = new Dictionary<int, MmdMaterialMapperRegistration>(_materialOverrides)
            {
                [materialIndex] = new MmdMaterialMapperRegistration(mapper, textureTargets, renderingTargets)
            };
            return new MmdMaterialMapperSet(DefaultMapper, DefaultTextureTargets, DefaultRenderingTargets, overrides);
        }

        internal MmdMaterialMapperRegistration Resolve(int materialIndex)
        {
            return _materialOverrides.TryGetValue(materialIndex, out MmdMaterialMapperRegistration registration)
                ? registration
                : new MmdMaterialMapperRegistration(DefaultMapper, DefaultTextureTargets, DefaultRenderingTargets);
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
            MmdMaterialTextureTargets textureTargets,
            MmdMaterialRenderingTargets renderingTargets)
        {
            Mapper = mapper;
            TextureTargets = textureTargets;
            RenderingTargets = renderingTargets;
        }

        public MmdMaterialMapper Mapper { get; }

        public MmdMaterialTextureTargets TextureTargets { get; }

        public MmdMaterialRenderingTargets RenderingTargets { get; }
    }
}
