# Unity Toon Shader Adapter (optional)

This sample converts an already-created set of MMD Toon materials to Unity Toon Shader (UTS) materials. It has no compile-time dependency on UTS. At runtime it resolves `Shader.Find("Toon/Toon")`, or accepts an injected `Shader` for deterministic tests and custom loading.

The adapter targets the property/pass schema shipped by **Unity Toon Shader 0.14.1-preview**. It validates the complete schema before creating any replacement. If UTS is absent, its schema differs, any input is invalid, or a conversion fails, the returned materials are the original MMD Toon materials and no input material is modified. Generated UTS materials are also cleaned up on failure.

## Usage

```csharp
var diagnostics = new List<UnityToonShaderDiagnostic>();
if (UnityToonShaderAdapter.TryConvertMaterials(
        instance.RenderingDescriptor.materials,
        instance.Materials,
        containsMaterialMorphs: true,
        out Material[] utsMaterials,
        diagnostics))
{
    // Replace every slot in one assignment only after conversion succeeds.
    instance.SkinnedMeshRenderer.sharedMaterials = utsMaterials;
}
else
{
    // utsMaterials contains the unchanged original material references.
    Debug.LogWarning(string.Join("\n", diagnostics));
}
```

The caller owns successful replacement materials and must destroy them when they are no longer used. Do not destroy the loader-owned original materials.

## Mapping and non-parity scope

- PMX diffuse color and base texture map to UTS Base Color/Base Map.
- PMX alpha classification, culling, render queue and Z-write are preserved conservatively.
- `.spa`/additive sphere maps and `.sph`/multiply sphere maps map to UTS MatCap. UTS has no exact MMD multiply-sphere equation, so `.sph` is an approximation.
- Opaque PMX edge color/size map to UTS outline color/width. PMX screen-pixel width and UTS outline width use different spaces, so width is an approximation. UTS Inspector normalization disables color writes for transparent outlines; the adapter reports this instead of promising a visible transparent edge.
- MMD toon ramps are not transferred. UTS shade colors use a conservative diffuse-derived approximation.
- MMD's dedicated self-shadow path is not available on UTS materials; UTS/URP shadows are used instead.
- MMD material morph writes target MMD shader properties and are incompatible with these UTS replacements.

This is an optional interoperability example, not a first-party MMD-look or animation parity promise. Review representative materials and fixed-frame screenshots before adopting it in production.
