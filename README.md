# unity-mmd-loader

![unity-mmd-loader](https://raw.githubusercontent.com/yohawing/unity-mmd-loader/main/docs/assets/main-image.png)

> Credits — Model: [Sour](https://bowlroll.net/file/146103) / Motion: [mobiusP](https://www.nicovideo.jp/watch/sm42576784) / Camera motion: [koko](https://bowlroll.net/file/305434) / Background: [Tojiru](https://seiga.nicovideo.jp/seiga/im11796453)

Import PMX models and VMD motions as ordinary Unity assets, place them in a scene, and drive them from Timeline. The current package is the `v0.1.3` release candidate for Unity 6000.4 URP on Windows x86_64.

[日本語](https://github.com/yohawing/unity-mmd-loader/blob/main/docs/README.ja.md) / [Detailed guide](https://github.com/yohawing/unity-mmd-loader/blob/main/docs/HOW_TO_USE.md)

## Quick start

1. Install the package and import the `Basic Playback` sample from Package Manager.
2. Import a `.pmx` and its referenced textures, then drag the imported PMX asset into the Scene or Hierarchy.
3. Import a `.vmd` and add it to an `MmdVmdTimelineTrack` bound to the placed playback object.
4. Scrub Timeline in Edit Mode for animation-only preview. Physics is intentionally off while editing.
5. Enter Play Mode and play Timeline forward to run Live physics.
6. If a Unity Humanoid clip is needed, configure the PMX Rig as Humanoid and explicitly create the clip from a Humanoid Setup Asset.

The sample contains redistributable PMX/VMD assets. See the [step-by-step guide](https://github.com/yohawing/unity-mmd-loader/blob/main/docs/HOW_TO_USE.md) for authoring and troubleshooting.

## Requirements and support

| Item | Current support |
| --- | --- |
| Unity | Unity 6000.4, Windows x86_64, Universal Render Pipeline 17 |
| Models | PMX import and scene placement. PMD is not supported |
| Motion | VMD import, native `mmd-anim` evaluation, Timeline scrub/playback, camera and directional-light motion |
| Morphs | Vertex/BlendShape, UV, material, bone, group, flip, and supported runtime morph evaluation |
| Physics | Live physics during Play Mode forward playback; Edit Mode scrub and random access are physics-off |
| Rendering | URP MMD Toon and URP Lit presets, textures, outline, transparent material order, optional SelfShadow |
| Humanoid | Rig mapping and Avatar import, explicit Humanoid Setup Asset and AnimationClip bake |

## Known limitations

- Overlapping VMD clips use deterministic single-winner hard cuts. Weighted pose blending is not advertised.
- PMD import and packaged macOS/Linux native binaries are not supported.
- Live physics is not evaluated by Edit Mode Timeline scrub and is not baked into Humanoid AnimationClips.
- Camera supports the documented VMD camera path; directional light is the release light path. Unsupported projection/light cases report not-ready diagnostics.
- SelfShadow requires explicit URP RendererFeature setup. Exact MMD/ray-mmd visual parity is not a release guarantee.
- Runtime raw-path loading is available for diagnostics; imported PMX/VMD assets are the normal authoring path.

## Install

In Unity Package Manager, choose **Add package from git URL**:

```text
https://github.com/yohawing/unity-mmd-loader.git?path=packages/com.yohawing.mmd-loader
```

## License boundary

This repository does not redistribute third-party PMX, VMD, texture, motion, audio, or capture assets. Confirm each asset's license and redistribution terms. Local verification assets and generated artifacts must not be committed.
