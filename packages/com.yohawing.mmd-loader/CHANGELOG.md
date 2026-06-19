# Changelog

All notable changes to `com.yohawing.mmd-loader` are documented here.

## [0.1.0] - Unreleased

### Added

- PMX import and scene placement through Unity asset workflows.
- VMD import and Timeline clip workflow.
- VMD camera and directional light runtime binding.
- Play Mode forward playback with Live physics on Windows x86_64.
- Edit Mode Timeline scrub as animation-only random access with physics off.
- URP baseline toon rendering, alpha handling, texture diagnostics, and material order handoff.
- Explicit Humanoid AnimationClip bake path when PMX, VMD, and Humanoid setup prerequisites are ready.
- Windows x86_64 packaged native runtime binaries.

### Known Limitations

- macOS and Linux native binaries are not distributed.
- Timeline random access keeps physics off; Physics Cache is not implemented.
- Full Humanoid bridge, rayMMD compatibility, broad export workflows, experimental physics backends, and Compute Skinning are future work.
- Third-party PMX / VMD / texture / motion / audio / capture assets are not redistributed.

