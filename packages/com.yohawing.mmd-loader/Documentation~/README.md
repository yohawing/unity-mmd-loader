# unity-mmd-loader

![unity-mmd-loader](https://raw.githubusercontent.com/yohawing/unity-mmd-loader/main/docs/assets/main-image.png)

> Credits — Model: [Sour](https://bowlroll.net/file/146103) / Motion: [mobiusP](https://www.nicovideo.jp/watch/sm42576784) / Camera motion: [koko](https://bowlroll.net/file/305434) / Background: [Tojiru](https://seiga.nicovideo.jp/seiga/im11796453)

unity-mmd-loader is a plugin for importing PMX / VMD into Unity.

It is designed to provide a natural import experience for modern Unity, letting you work with PMX models and VMD motions directly in Unity's Project / Scene / Timeline / Runtime workflows.

[日本語](https://github.com/yohawing/unity-mmd-loader/blob/main/docs/README.ja.md) / [How to use](https://github.com/yohawing/unity-mmd-loader/blob/main/docs/HOW_TO_USE.md)

## Features

- **Treat MMD files as standard Unity assets** — PMX / VMD files go through Unity's import pipeline instead of a dedicated viewer. Drop a `.pmx` into your project and it becomes a prefab with materials and textures set up automatically.
- **Edit VMD on the Timeline** — VMD motions are treated as Timeline clips, so Unity Timeline can be used for combining motions and directing scenes. Camera and light VMD are also supported.
- **MMD-style toon rendering** — A URP-based MMD-oriented toon shader approximates MMD-style rendering, including edges, alpha, and textures.

## Support Status

| Item | Status |
| --- | --- |
| Target environment | Unity 6000.4 / Windows x86_64 / URP |
| Model | PMX import and scene placement supported. PMD is not supported |
| VMD | Import and Timeline clip supported. Motion playback is evaluated at runtime by [mmd-anim](https://github.com/yohawing/mmd-anim). Camera motion supported |
| Morph | Vertex (blend shape) / UV / material / bone / group morphs supported |
| Physics | Real-time physics during Play Mode forward playback |
| Rendering | URP-based toon, outline RendererFeature, alpha, texture diagnostics, material order handoff |
| Humanoid | Animator and proxy rig are set up automatically on import. Existing Humanoid motion assets can be retargeted |

## Roadmap

Items we plan to work on in future releases (scope and priority may change).

| Item | Plan |
| --- | --- |
| Timeline enhancements | Expand Timeline editing and direction features, including audio/music-synced playback |
| Runtime MMD Rig | Support runtime MMD rig features such as IK, append parent, and axis limits |
| Higher rendering fidelity | Tune and verify outline fidelity, self-shadow visual quality, and additional real-model rendering cases |
| Broader URP pipeline support | Build on the existing outline RendererFeature and verify additional paths such as Forward+ / Deferred and Render Pipeline Asset / Volume integration |
| Runtime loading | Add an API to load PMX / VMD dynamically at runtime |
| macOS / Linux native | Distribute native binaries for each platform |
| Broader Unity support | Verify on Unity versions other than 6000.4 |

## Install

Add the following URL through Unity Package Manager **Add package from git URL**.

```text
https://github.com/yohawing/unity-mmd-loader.git?path=packages/com.yohawing.mmd-loader
```

## Basic Flow

1. Import a `.pmx` file and its textures into Unity.
2. Drag the imported PMX asset into the Scene or Hierarchy.
3. Import a `.vmd` file.
4. Bind the scene playback object to Timeline and create a VMD Timeline clip.
5. In Play Mode, check forward playback and real-time physics.
6. In Edit Mode, Timeline scrub is treated as physics-off animation preview.

For detailed steps, see [HOW_TO_USE.md](https://github.com/yohawing/unity-mmd-loader/blob/main/docs/HOW_TO_USE.md).

Rendering implementation notes:

- [MMD SelfShadow Implementation Notes](MMD_SELF_SHADOW.md)

## License Boundary

This repository does not redistribute third-party PMX / VMD / texture / motion / audio / capture assets.

When using MMD assets, users must confirm the license and redistribution terms themselves. Local verification assets, generated logs, screenshots, and test artifacts are not intended to be included in package commits.
