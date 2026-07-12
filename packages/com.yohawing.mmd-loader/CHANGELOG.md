# Changelog

All notable changes to `com.yohawing.mmd-loader` are documented here.

## [0.1.3] - 2026-07-11

### Added

- Release Gate v2 with Runtime Verification golden-path, visual smoke, local-asset preflight, and packaged-native parity reporting.
- URP Lit material conversion with per-material PBR overrides, MME `.fx` / `.emd` mapping, and conventional normal/roughness/metallic/AO texture discovery.
- Runtime Verification viewer transport, camera controls, local file loading, and recent-file persistence.
- Basic Playback Timeline smoke coverage and explicit Humanoid clip-bake readiness diagnostics.

### Changed

- Native runtime package binary and submodule are aligned with `mmd-anim` v0.2.0 while retaining runtime ABI version 2.
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
- Explicit Humanoid AnimationClip bake path when PMX, VMD, and Humanoid setup prerequisites are ready.
- Windows x86_64 packaged native runtime binaries (mmd-anim v0.1.5).

### Known Limitations

- macOS and Linux native binaries are not distributed.
- Timeline random access keeps physics off; Physics Cache is not implemented.
- Humanoid bridge covers retarget, Timeline scrub, and Live physics; advanced features (IK override, runtime rig swap) and rayMMD compatibility, broad export workflows, experimental physics backends, and Compute Skinning are future work.
- Third-party PMX / VMD / texture / motion / audio / capture assets are not redistributed.

