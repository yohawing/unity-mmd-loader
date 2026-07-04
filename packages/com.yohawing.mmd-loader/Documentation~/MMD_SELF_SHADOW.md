# MMD SelfShadow Implementation Notes

Last updated: 2026-07-03 JST

This note records the current MMD SelfShadow contract and the main pitfalls found
while comparing Unity rendering with MikuMikuDance v9.32 x64 traces.

## Contract

- SelfShadow is not Unity/URP standard shadow receive.
- VMD self-shadow frames preserve only `frame`, `mode`, and `distance`.
- Timeline records sampled self-shadow state into `MmdSceneEnvironmentBinding`.
- `MmdSelfShadowTarget` is an internal/hidden character-root shim for bounds and
  receiver gating. Normal users should not add it by hand.
- `MmdSelfShadowRendererFeature` creates a dedicated `_MmdSelfShadowMap`.
- MMD toon ForwardLit ignores URP main-light shadow attenuation.
- SelfShadow must not mutate `Light.shadows`, `RenderSettings`,
  `QualitySettings.shadowDistance`, URP assets, or Materials.

## Reference Behavior

The MMD reference path observed by apitrace is:

1. Render a `2048x2048` `D3DFMT_R32F` render target.
2. In the shadow pass, write light clip `z / w` into R.
3. In the main pass, sample that R32F texture.
4. Compare receiver `z / w` with sampled depth.
5. Combine shadow visibility with toon visibility, then blend `ToonColor`
   toward white.

The high-level main pass shape is:

```hlsl
float toonVisibility = min(shadowVisibility, saturate(dot(N, -LightDir) * 3));
rgb = diffuseBase * lerp(ToonColor.rgb, 1.0.xxx, toonVisibility)
    + specular * toonVisibility;
```

In the traced MMD shader, `ToonColor` is a pixel shader constant. The shader did
not directly sample a toon ramp for this color, and the darkening multiply is
shader arithmetic rather than D3D fixed-function multiply blending. The traced
fixture recovers `ToonColor` from that fixture's toon image bottom band. That
does not mean every Unity `_ToonMap` should sample `v = 0.0`: the built-in MMD
shared toon strips in this package place their very dark band at `v = 0.0`, so a
global bottom-row sample collapses shared-toon materials toward black. Unity
keeps the existing NdotL soft toon path when self-shadow is disabled or the
fragment is not shadowed, then blends the dedicated self-shadow ToonColor branch
only in the darker direction when the dedicated self-shadow map actually shadows
the fragment. Unity still treats the complete CPU-side selection rule as an
explicit follow-up.

## Shadow Map Rules

The dedicated map is an R32F color map, not a regular depth shadow map.

- `MmdSelfShadowCaster` must write MMD-style `z / w` into color R.
- Do not fallback to ordinary `ShadowCaster` inside the self-shadow pass.
  URP/standard `ShadowCaster` is depth-only / `ColorMask 0`, so it does not write
  the sampled R32F value.
- If the map appears empty or nearly constant, inspect clear values and caster
  pass selection before tuning bias.

## Clear Values

Depth attachment clear uses the conventional API value `1.0` (`1 = far`).
Unity handles reversed-Z conversion internally for the depth attachment. Passing
`usesReversedZBuffer ? 0 : 1` double-inverts the clear and can reject all caster
fragments, leaving the map empty.

The R32F color map stores the sampled MMD depth value itself. Its clear color is
therefore chosen as the far value in the sampled range:

- reversed-Z: black
- non-reversed-Z: white

## Target / Environment Resolution

`MmdSelfShadowTarget` resolves scene state as follows:

1. Explicit `target.SceneEnvironment` wins.
2. If unbound, exactly one active `MmdSceneEnvironmentBinding` with
   `Recorded` or `Disabled` self-shadow state may be auto-followed.
3. Zero or multiple matching environments means inactive.

This avoids accidentally applying one Timeline/environment's self-shadow state
to all models in scenes with multiple bindings.

## Direction Resolution

Self-shadow direction is resolved in this order:

1. Active target's self-shadow light direction from `MmdSceneEnvironmentBinding`
2. `RenderSettings.sun`
3. RendererFeature serialized fallback direction
4. Built-in default direction

The RendererFeature direction is only a fallback, not a persistent scene light
override.

## Bias / Bounds / Acne

`ShadowDepthBias` is world-space meters and is normalized by the fitted character
depth range in the render pass. The current default is `0.02m`.

Fine stripe artifacts seen from the opposite side should be treated as
self-shadow acne first, not dithering. If changing bias has no effect, the
shadow map is probably empty, clipped, or filled with an incorrect clear value.

Bounds are fitted to active character targets. If hair, sleeves, or skirts are
missing from the map, inspect target bounds and padding before changing the
compare formula.

## Debugging Checklist

1. Renderer data includes `MmdSelfShadowRendererFeature`.
2. `MmdSceneEnvironmentBinding.LastSelfShadowApplyStatus` is `Recorded`.
3. The target has an explicit environment, or there is exactly one recorded
   environment in the scene.
4. Receiver renderer property block has `_MmdSelfShadowReceive = 1`.
5. Frame Debugger shows `MMD Self Shadow Pass`.
6. The R32F map output is not a constant far value.
7. Draws use `MmdSelfShadowCaster`, not ordinary `ShadowCaster`.
8. Character bounds include moving hair/cloth parts.
9. Bias changes affect acne/peter-panning.
10. Toon-map-less materials remain flat instead of receiving dirty dark bands.

Depth preview alone may not show receiver-side visibility. For hard cases, add a
temporary shader probe for sampled depth or final visibility; do not keep it as
normal product UI.

## Open Questions

- Exact formula for MMD `distance` -> `matLightViewProj`.
- Complete mapping of `mode=1/2` to shader boolean branches.
- Complete CPU-side rule for deriving `ToonColor` from toon textures beyond the
  traced bottom-band fixture.
- Full transparent/sphere/toon-disabled shader variant behavior.
- Remaining projection parity between Unity's fitted matrix and MMD's projective
  light matrix.
