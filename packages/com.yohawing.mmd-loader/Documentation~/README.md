# MMD Loader for Unity

Import PMX models and VMD motions as ordinary Unity assets, place them in a scene, and drive them from Timeline. This documentation describes the `v0.1.3` release candidate for Unity 6000.4 URP on Windows x86_64.

## Quick start

1. Import the `Basic Playback` sample from Package Manager.
2. Import a `.pmx` with its referenced textures and drag the imported PMX asset into the Scene or Hierarchy.
3. Import a `.vmd`, create an `MmdVmdTimelineTrack`, bind the placed playback object, and add the VMD clip.
4. Scrub in Edit Mode for physics-off animation preview.
5. Play Timeline forward in Play Mode for Live physics.
6. For a Unity Humanoid clip, configure the PMX Rig as Humanoid and explicitly bake from a Humanoid Setup Asset.

For the complete workflow, see the repository [HOW_TO_USE guide](https://github.com/yohawing/unity-mmd-loader/blob/main/docs/HOW_TO_USE.md).

## Requirements and support

| Item | Current support |
| --- | --- |
| Unity | Unity 6000.4, Windows x86_64, Universal Render Pipeline 17 |
| Models | PMX import and scene placement; PMD is not supported |
| Motion | Native `mmd-anim` VMD evaluation, Timeline scrub/playback, camera and directional-light motion |
| Physics | Live physics in Play Mode forward playback; Edit Mode scrub and random access are physics-off |
| Rendering | URP MMD Toon and URP Lit presets, outline, transparent material order, optional SelfShadow |
| Humanoid | Rig mapping and Avatar import, explicit Setup Asset and AnimationClip bake |

## Samples

- `Basic Playback`: redistributable PMX/VMD assets for the normal import → Scene → Timeline path.
- `Runtime Verification`: standalone local-asset viewer and CLI diagnostics player for load, seek/playback, camera VMD, audio/background references, JSON reports, and screenshots. It is not the primary authoring workflow.

## Known limitations

- Overlapping VMD clips select one winner; weighted pose blending is not supported.
- PMD import and packaged macOS/Linux native binaries are not supported.
- Live physics is not evaluated during Edit Mode scrub or baked into Humanoid clips.
- Directional light is the release light path; unsupported projection/light cases report not-ready diagnostics.
- SelfShadow requires explicit URP RendererFeature setup. Exact MMD/ray-mmd visual parity is not guaranteed.
- Runtime raw-path loading is intended for diagnostics; imported assets are the normal authoring path.

## Install

Use Unity Package Manager **Add package from git URL**:

```text
https://github.com/yohawing/unity-mmd-loader.git?path=packages/com.yohawing.mmd-loader
```

## License boundary

The package does not redistribute third-party PMX, VMD, texture, motion, audio, or capture assets. Verify their licenses and keep local verification assets and generated artifacts outside package commits.
