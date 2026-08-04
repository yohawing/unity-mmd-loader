# Changelog

All notable changes to `com.yohawing.mmd-loader` are documented here.

## [Unreleased]

## [0.4.0] - 2026-08-04

### Added

- Added native VMD summary and raw-track readback for source-backed import and playback, including bone, morph, camera, light, and self-shadow channels.
- Added fail-closed diagnostics and contract coverage for native runtime unavailability, invalid motion bytes, ABI mismatch, and retained native-handle lifetime.

### Changed

- Routed source-backed PMX/VMD playback, Timeline evaluation, post-physics pose handoff, and Generic/Humanoid AnimationClip baking through the native `mmd-anim` runtime.
- Updated the packaged Windows native runtime and `native/mmd-anim` pin to `mmd-anim` `v0.4.0` / remote `main` commit `22e7d7b`, retaining runtime ABI version 3.
- Reduced fast-playback overhead by reusing native pose worksets and avoiding repeated per-frame source fingerprint scans.

### Fixed

- Fixed fast playback material preset rebinding and preserved the native post-physics pose handoff for deform-after-physics models.

### Removed

- Removed the obsolete managed VMD JSON parser/evaluator, source-less frame and trace evaluation stack, managed physics/IK/pose compatibility paths, legacy camera keyframe shims, and direct Timeline `ProcessFrame` compatibility path.
- Removed the legacy PMX buffer fallback; native parser failures now remain explicit instead of silently switching to a managed fallback.

### Known Limitations

- Runtime evaluation requires the bundled native runtime; unavailable or incompatible native paths report diagnostics and do not fall back to the removed managed evaluator.
- macOS and Linux native binaries are not distributed in the package.
- Timeline random access keeps physics off; Live physics is limited to Play Mode forward playback.
- Raw VMD Timeline clips use deterministic hard-cut selection rather than weighted blending.
- Humanoid bake does not include Live physics, facial morphs, or native MMD IK/helper behavior.
- The Unity Toon Shader adapter remains optional and conservative; it falls back to MMD Toon when UTS is absent or its shader schema is unsupported.

## [0.3.0] - 2026-07-30

### Added

- Added an opt-in MMD Toon Lit material profile with dedicated inspectors and authoring for shade colors, toon boundaries and bands, stylized specular, rim lighting, HDR emission, cutout surfaces, ambient SH, fog, realtime shadows, SSAO, and reflection probes.
- Added an optional Unity Toon Shader adapter sample with fail-closed shader capability diagnostics, custom material profile support, generated-material comparison coverage, and a self-contained demo scene.
- Added extensible material mapper/profile contracts, per-shader property targets, material-morph routing, unsupported-feature diagnostics, external PMX material extraction/remaps, and automatic MME normal-map import.
- Added a clean Unity 6000.0 LTS consumer compatibility gate.

### Changed

- Updated the packaged Windows native runtime and `native/mmd-anim` pin to `mmd-anim` `v0.3.3` / remote `main` commit `25c956d`, retaining runtime ABI version 3.
- Lowered the supported Unity floor to Unity 6000.0 LTS while retaining current Unity 6000.4 compatibility.
- Migrated reduced-pose consumption to the runtime-neutral ABI 3 curve contract and aligned Unity handedness/tangent conversion with the native source.
- Improved runtime throughput by reusing native playback sessions, compiled pose/IK topology, physics readback buffers, decoded textures, and unchanged material-morph state, while using the PMX geometry handle to reduce peak parser materialization.
- Extracted imported PMX materials beside the source asset and persisted them through Unity material remaps instead of embedding them in the generated prefab.

### Fixed

- Managed VMD sampling now uses MMD's registered interpolation layout and fixed-axis rotation projection when paired with a PMX model, matching the packaged native playback path.
- Fixed fast runtime playback for deform-after-physics bones and preserved Humanoid root height after Timeline evaluation.
- Fixed Toon Lit deferred/clustered-light routing, directional-light color, cast/received shadow policy, UTS alpha-cutout textures, public Toon texture rebinding, and MME normal-map import.
- Hardened performance report validation, Unity project guards, and missing Windows common-application-data environment initialization used by UPM.

### Removed

- Removed the experimental Editable Rig post-processing layer and its public authoring types.
- Removed unused PMX/VMD Inspector readiness helpers, the unconnected Generic AnimationClip parity verifier, and the SelfShadow RendererFeature setup warning UI.
- Removed automatic generated-model playback fallback, its transient runtime marker, and the public `allowRuntimeFallback` overload parameters; playback now requires a matching scene `SkinnedMeshRenderer`.

### Known Limitations

- macOS and Linux native binaries are not distributed in the package.
- Timeline random access keeps physics off; Live physics is limited to Play Mode forward playback.
- Raw VMD Timeline clips use deterministic hard-cut selection rather than weighted blending.
- Humanoid bake does not include Live physics, facial morphs, or native MMD IK/helper behavior.
- The Unity Toon Shader adapter remains optional and conservative; it falls back to MMD Toon when UTS is absent or its shader schema is unsupported.

## [0.2.0] - 2026-07-17

### Added

- Explicit Generic and Humanoid AnimationClip bake workflows for imported PMX/VMD assets, including frame-range controls, batched native sampling, sparse reduced curves, parity checks, and safe project-relative output paths.
- Humanoid bake round-trip coverage and Timeline authoring support that synchronizes directly assigned Humanoid clips to their AnimationClip duration.
- Bounded texture decoding and adversarial import coverage for PNG, JPEG, BMP, DDS, and TGA inputs.

### Changed

- Lowered the minimum supported Unity version to Unity 6000.0 LTS while retaining compatibility with current Unity 6000.4 editors.
- Humanoid AnimationClip bake now uses only the Avatar and retarget mapping persisted by PMX Humanoid import; the duplicate setup-asset workflow and creation UI have been removed. The obsolete `MmdHumanoidSetupAsset` type, preset enum, serialized field layout, original MonoScript GUID, and builder signatures remain as read-only/source-compatible bridges for existing assets and integrations. The builder no longer creates assets and instead directs callers to reimport with `Animation Type = Humanoid`.
- Live physics now uses the bundled `mmd-anim` Bullet runtime, with the package native runtime aligned to `mmd-anim` `v0.3.0` / remote `main` commit `c3a35e0`.
- PMX and VMD inspectors now expose a smaller, asset-focused action surface, and no-op importer settings and duplicate scene-action buttons have been removed.
- Imported playback reconfiguration now treats borrowed scene objects, preview visibility, SelfShadow targets, and transient runtime instances as explicit ownership boundaries.

### Fixed

- Unity launch scripts now restore missing Windows common-application-data environment values before starting UPM, avoiding misleading local-package `path ... undefined` failures.
- Generic and Humanoid AnimationClip writers reject oversized dense bake ranges before allocating unbounded managed key buffers, and failed Humanoid writes now release unreturned clips.
- Generic sparse AnimationClip bake now preserves Unity coordinate conversion and accepts Euler rotation curves in parity checks.
- Humanoid AnimationClip bake now preserves frame-wise body pose and root-motion fidelity.
- PMX importer failures roll back generated Unity objects instead of leaving partial imported state.
- Runtime texture references are sandboxed to approved asset roots, and oversized or malformed texture inputs are rejected before unbounded decode allocation.
- Playback rebind paths restore borrowed scene state and dispose owned runtime instances.
- PMX draw-edge materials render outlines again after the material-policy refactor.

### Known Limitations

- macOS and Linux native binaries are not distributed in the package.
- Timeline random access keeps physics off; Live physics is limited to Play Mode forward playback.
- Raw VMD Timeline clips use deterministic hard-cut selection rather than weighted blending.
- Humanoid bake does not include Live physics, facial morphs, or native MMD IK/helper behavior.

## [0.1.3] - 2026-07-11

### Added

- Release Gate v2 with golden-path coverage, local-asset preflight, and packaged-native parity reporting.
- URP Lit material conversion with per-material PBR overrides, MME `.fx` / `.emd` mapping, and conventional normal/roughness/metallic/AO texture discovery.
- Basic Playback Timeline smoke coverage and explicit Humanoid clip-bake readiness diagnostics.

### Changed

- Native runtime package binary and submodule are aligned with `mmd-anim` remote `main` commit `d82a022` while retaining runtime ABI version 2.
- VMD Timeline overlaps use deterministic single-winner arbitration; weighted pose blending is not advertised.
- Runtime, Timeline, editable-rig, morph, and Live-physics tests now use native-backed fixtures across the release path.
- SelfShadow setup diagnostics and troubleshooting now distinguish static RendererFeature readiness from runtime binding/pass state.

### Fixed

- DX12 SelfShadow receiver toggling no longer corrupts instancing-buffer rendering.
- PlayMode fixtures provide the native PMX/VMD source bytes required by current runtime evaluation.

### Known Limitations

- macOS and Linux native binaries are not distributed in the package.
- Timeline random access keeps physics off; Live physics is limited to Play Mode forward playback.
- Raw VMD Timeline clips use deterministic hard-cut selection rather than weighted blending.

## [0.1.2] - 2026-07-04

### Added

- MMD SelfShadow rendering path with dedicated URP RendererFeature, character bounds collection, scene binding state, R32F map sampling, and diagnostics.
- Runtime Verification viewer mode for fixture-manifest-driven PMX/VMD playback case switching.
- VMD camera/light native track sampling through the `mmd-anim` v0.1.9 runtime surface.
- PMX scene placement now instantiates the imported prefab hierarchy path instead of rebuilding a separate scene hierarchy.
- Public SelfShadow setup and troubleshooting documentation.

### Changed

- MMD toon materials no longer receive Unity/URP standard main-light shadows as their character self-shadow source; MMD SelfShadow is explicit and isolated from scene-wide lighting mutation.
- Native runtime package binary is aligned with `native/mmd-anim` v0.1.9.
- Backface-culled materials keep outline visibility disabled to match culling policy.

### Known Limitations

- SelfShadow visual parity is still a fidelity backlog item; it is not a release blocker for the PMX -> Scene -> VMD Timeline -> Play Mode golden path.
- macOS and Linux native binaries are not distributed.
- Timeline random access keeps physics off; Physics Cache is not implemented.

## [0.1.1] - 2026-06-26

### Added

- Basic Playback sample now includes redistributable PMX/VMD assets for the release golden path.
- URP outline rendering is exposed through `MmdOutlineRendererFeature`, with release readiness surfaced by the PMX inspector.
- Runtime Verification sample is listed in package metadata for direct PMX/VMD parse, playback, Timeline drive, and JSON diagnostics.

### Changed

- Package metadata now declares the URP dependency required by the release rendering path.
- README roadmap now treats Humanoid AnimationClip bake and outline RendererFeature as existing release surfaces, not future work.

## [0.1.0] - 2026-06-22

### Added

- PMX import and scene placement through Unity asset workflows.
- VMD import and Timeline clip workflow.
- VMD camera and directional light runtime binding.
- Play Mode forward playback with Live physics on Windows x86_64.
- Edit Mode Timeline scrub as animation-only random access with physics off.
- URP baseline toon rendering, alpha handling, texture diagnostics, and material order handoff.
- Default PMX import scale of 0.1 for human-friendly meter-scale models, with import-scale-aware Live physics and VMD camera framing.
- Humanoid rig setup at import (Animator plus a persistent proxy control rig) with retargeted playback that drives the real MMD bones and append (付与) transforms and steps Live physics.
- Unified MmdHumanoidAnimationTrack: a single Timeline track poses the Humanoid avatar and drives the native MMD model via retarget side-effect, replacing the former two-track setup.
- Explicit Humanoid AnimationClip bake path when PMX, VMD, and imported Humanoid prerequisites are ready.
- Windows x86_64 packaged native runtime binaries (mmd-anim v0.1.5).

### Known Limitations

- macOS and Linux native binaries are not distributed.
- Timeline random access keeps physics off; Physics Cache is not implemented.
- Humanoid bridge covers retarget, Timeline scrub, and Live physics; advanced features (IK override, runtime rig swap) and rayMMD compatibility, broad export workflows, experimental physics backends, and Compute Skinning are future work.
- Third-party PMX / VMD / texture / motion / audio / capture assets are not redistributed.

