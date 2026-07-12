# MMD Self Shadow

Updated: 2026-07-09

This document defines the current MMD self-shadow diagnostic boundary. The feature is release-adjacent quality work, not a golden-path release blocker.

## Ownership

MMD self-shadow is explicit scene/render state:

- `MmdSceneEnvironmentBinding` records VMD/default self-shadow state.
- `MmdSelfShadowTarget` marks the character roots that may receive and cast the dedicated MMD shadow.
- `MmdSelfShadowRendererFeature` and `MmdSelfShadowRenderPass` render the dedicated shadow map.

This path must not mutate Unity `Light.shadows`, `RenderSettings`, `QualitySettings.shadowDistance`, URP assets, or Materials at runtime. Renderer setup remains an explicit user/project configuration step.

## Diagnostic Layers

The same `MmdSceneSelfShadowDiagnosticStatus` enum is used across three layers. `Active` does not mean the same thing at every layer.

| Layer | API | `Active` means | Does not prove |
| --- | --- | --- | --- |
| Binding state | `MmdSceneEnvironmentBinding.EvaluateSelfShadowDiagnosticStatus()` / `LastSelfShadowDiagnosticStatus` | A VMD/default self-shadow state is recorded and its projection policy is active. | A character target exists, the URP renderer feature is enabled, bounds exist, or a caster pass exists. |
| Target path | `MmdSelfShadowTarget.EvaluateSelfShadowDiagnosticStatus()` | Binding state is active, the receiver gate is available, bounds exist, and at least one material exposes `MmdSelfShadowCaster`. | Which URP Renderer Data owns the renderer feature, or whether a later camera rendered the pass. |
| Render pass | `MmdSelfShadowRenderPass.LastDiagnosticStatus` | The most recent pass setup result. | Current Inspector truth. This value is global, camera/renderer agnostic, and has no freshness marker. |

Do not present binding-local `Active` as "self-shadow is rendering". It only means the scene binding has usable MMD self-shadow state.

## Status Meaning

| Status | Typical layer | Meaning | User action |
| --- | --- | --- | --- |
| `NoSelfShadowState` | Binding / target | No VMD/default self-shadow state has been recorded. | Evaluate playback or ensure the binding can record its default state. |
| `Active` | All layers | Layer-local success only. | Check which layer produced it before reporting success. |
| `ModeDisabled` | Binding / target | VMD mode or binding policy disables self-shadow. | Enable Self Shadow on the binding, or use a VMD self-shadow mode that casts. |
| `NoCharacterRoots` | Target / pass | No enabled character target is available. | Place the PMX/playback root in the scene so the hidden target can exist. |
| `NoBounds` | Target / pass | No visible renderer bounds were collected. | Check active hierarchy/renderers and bounds. |
| `NoCasterPass` | Target / pass | Renderers exist, but no material has `MmdSelfShadowCaster`. | Use generated `MMD Basic URP Toon` materials or a compatible replacement shader. |
| `AmbiguousEnvironment` | Target | An unbound target found multiple active scene environment bindings. | Keep one active binding, or explicitly assign the target's environment. |
| `ReceiverGateOff` | Target | Receiver property blocks are disabled even though the target exists. | Re-render with an active feature; investigate if the renderer feature disabled gates. |
| `NoRendererFeature` | Target / pass | No active MMD self-shadow renderer feature is driving the pass. | Add and enable `MmdSelfShadowRendererFeature` on the Renderer Data actually used by the active URP Asset and Quality level. |

## Inspector Policy

Do not add a normal Inspector field that displays only `MmdSceneEnvironmentBinding.LastSelfShadowDiagnosticStatus` or `MmdSelfShadowRenderPass.LastDiagnosticStatus`.

An Inspector diagnostic is unsafe when it:

- labels binding-local `Active` as rendered/working;
- shows `LastDiagnosticStatus` without freshness, camera, and renderer source;
- merges binding state, target path, and render pass results into one unlabeled value;
- shows success when the renderer feature, target, bounds, or caster pass is missing;
- exposes hidden `MmdSelfShadowTarget` as a normal product surface;
- writes Light, RenderSettings, QualitySettings, URP asset, or Material state while diagnosing.

A future product diagnostic should separate state recording, target readiness, static Renderer Data setup, and last rendered pass result. Static Renderer Data setup should be inspected from the active URP Asset / Renderer Data, not inferred from the global last render-pass status.

## Troubleshooting Order

Use this order when a model renders but MMD self-shadow is missing:

1. Confirm the active URP Asset and Quality level use a Renderer Data asset that contains an enabled `MmdSelfShadowRendererFeature`.
2. Confirm the placed PMX uses generated `MMD Basic URP Toon` materials, or replacement shaders with an `MmdSelfShadowCaster` pass.
3. Confirm the scene has one active `MmdSceneEnvironmentBinding`. Multiple active bindings can produce `AmbiguousEnvironment`.
4. Confirm `Self Shadow Enabled` is on. Binding state alone does not prove the render path is active.
5. Confirm the character hierarchy is active and has visible renderer bounds.
6. If the VMD self-shadow mode disables casting, expect `ModeDisabled`.

