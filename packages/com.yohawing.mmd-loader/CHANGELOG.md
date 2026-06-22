# Changelog

All notable changes to `com.yohawing.mmd-loader` are documented here.

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
- Windows x86_64 packaged native runtime binaries.

### Known Limitations

- macOS and Linux native binaries are not distributed.
- Timeline random access keeps physics off; Physics Cache is not implemented.
- Humanoid bridge covers retarget, Timeline scrub, and Live physics; advanced features (IK override, runtime rig swap) and rayMMD compatibility, broad export workflows, experimental physics backends, and Compute Skinning are future work.
- Third-party PMX / VMD / texture / motion / audio / capture assets are not redistributed.

