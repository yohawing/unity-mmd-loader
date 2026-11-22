#nullable enable

using System;
using UnityEngine;
using Mmd.UnityIntegration;

namespace Mmd
{
    [Serializable]
    public sealed class MmdMaterialProfileTextureTargets
    {
        public string[] diffuseTextureProperties = new[] { "_BaseMap" };
        public string sphereTextureProperty = string.Empty;
        public string toonTextureProperty = string.Empty;
        public string diffuseTextureBoundProperty = string.Empty;
        public string sphereTextureBoundProperty = string.Empty;
        public string sphereModeProperty = string.Empty;
        public string toonTextureBoundProperty = string.Empty;

        internal MmdMaterialTextureTargets ToRuntimeTargets()
        {
            // Named arguments: a swapped string property cannot be caught by the compiler.
            return new MmdMaterialTextureTargets(
                diffuseTextureProperties: diffuseTextureProperties,
                sphereTextureProperty: sphereTextureProperty,
                toonTextureProperty: toonTextureProperty,
                diffuseTextureBoundProperty: diffuseTextureBoundProperty,
                sphereModeProperty: sphereModeProperty,
                toonTextureBoundProperty: toonTextureBoundProperty,
                sphereTextureBoundProperty: sphereTextureBoundProperty);
        }
    }

    [Serializable]
    public sealed class MmdMaterialProfileRenderingTargets
    {
        public string baseColorProperty = "_BaseColor";
        public string colorProperty = "_Color";
        public string ambientColorProperty = "_AmbientColor";
        public string alphaProperty = "_Alpha";
        public string alphaClipThresholdProperty = "_AlphaClipThreshold";
        public string shadowAlphaClipThresholdProperty = "_ShadowAlphaClipThreshold";
        public string textureAlphaOutputWeightProperty = "_TextureAlphaOutputWeight";
        public string textureAlphaClipMaskProperty = string.Empty;
        public string alphaClipModeProperty = string.Empty;
        public string cullProperty = "_Cull";
        public string surfaceProperty = "_Surface";
        public string blendProperty = "_Blend";
        public string sourceBlendProperty = "_SrcBlend";
        public string destinationBlendProperty = "_DstBlend";
        public string zWriteProperty = "_ZWrite";
        public string outlineColorProperty = "_OutlineColor";
        public string outlineWidthProperty = "_OutlineWidth";
        public string outlineVisibleProperty = "_OutlineVisible";
        public string outlineScreenSpaceWeightProperty = "_OutlineScreenSpaceWeight";
        public string outlineZTestProperty = "_OutlineZTest";
        public bool supportsRenderQueue = true;
        public string[] unsupportedFeatures = Array.Empty<string>();
        public bool validatePropertyPresence = true;
        public string[] requiredKeywords = Array.Empty<string>();
        public string[] requiredPasses = Array.Empty<string>();
        public bool supportsMaterialMorphs = true;

        internal MmdMaterialRenderingTargets ToRuntimeTargets()
        {
            // Named arguments: the constructor takes 26 mostly-string parameters, so a positional
            // swap would compile. MmdMaterialProfileAssetTests round-trips every member.
            return new MmdMaterialRenderingTargets(
                baseColorProperty: baseColorProperty,
                colorProperty: colorProperty,
                ambientColorProperty: ambientColorProperty,
                alphaProperty: alphaProperty,
                alphaClipThresholdProperty: alphaClipThresholdProperty,
                shadowAlphaClipThresholdProperty: shadowAlphaClipThresholdProperty,
                textureAlphaOutputWeightProperty: textureAlphaOutputWeightProperty,
                cullProperty: cullProperty,
                surfaceProperty: surfaceProperty,
                blendProperty: blendProperty,
                sourceBlendProperty: sourceBlendProperty,
                destinationBlendProperty: destinationBlendProperty,
                zWriteProperty: zWriteProperty,
                outlineColorProperty: outlineColorProperty,
                outlineWidthProperty: outlineWidthProperty,
                outlineVisibleProperty: outlineVisibleProperty,
                outlineScreenSpaceWeightProperty: outlineScreenSpaceWeightProperty,
                outlineZTestProperty: outlineZTestProperty,
                supportsRenderQueue: supportsRenderQueue,
                unsupportedFeatures: unsupportedFeatures,
                validatePropertyPresence: validatePropertyPresence,
                requiredKeywords: requiredKeywords,
                requiredPasses: requiredPasses,
                supportsMaterialMorphs: supportsMaterialMorphs,
                textureAlphaClipMaskProperty: textureAlphaClipMaskProperty,
                alphaClipModeProperty: alphaClipModeProperty);
        }
    }

    [CreateAssetMenu(
        fileName = "MmdMaterialProfile",
        menuName = "MMD/Material Profile")]
    public sealed class MmdMaterialProfileAsset : ScriptableObject
    {
        public const int CurrentSchemaVersion = 1;

        public int schemaVersion = CurrentSchemaVersion;
        public Shader? shader;
        public MmdMaterialProfileTextureTargets textureTargets = new();
        public MmdMaterialProfileRenderingTargets renderingTargets = new();

        public bool TryCreateMapperSet(
            out MmdMaterialMapperSet? mapperSet,
            out string reason)
        {
            mapperSet = null;
            if (schemaVersion != CurrentSchemaVersion)
            {
                reason = $"profile-schema-version-unsupported:{schemaVersion}";
                return false;
            }

            if (shader == null)
            {
                reason = "profile-shader-missing";
                return false;
            }

            if (textureTargets == null || renderingTargets == null)
            {
                reason = "profile-targets-missing";
                return false;
            }

            Shader profileShader = shader;
            mapperSet = new MmdMaterialMapperSet(
                (descriptor, resolvedDefaultShader) => new Material(profileShader),
                textureTargets.ToRuntimeTargets(),
                renderingTargets.ToRuntimeTargets());
            reason = string.Empty;
            return true;
        }
    }
}
