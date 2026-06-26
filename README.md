# unity-mmd-loader

![unity-mmd-loader](https://raw.githubusercontent.com/yohawing/unity-mmd-loader/main/docs/assets/main-image.png)

> Credits — Model: [Sour](https://bowlroll.net/file/146103) / Motion: [mobiusP](https://www.nicovideo.jp/watch/sm42576784) / Camera motion: [koko](https://bowlroll.net/file/305434)

unity-mmd-loader is a plugin for bringing PMX / VMD into Unity.

It is designed to deliver a natural import experience that takes advantage of modern Unity, letting you work with PMX models and VMD motions directly in Unity's Project / Scene / Timeline / Runtime.

[日本語](https://github.com/yohawing/unity-mmd-loader/blob/main/docs/README.ja.md) / [How to use](https://github.com/yohawing/unity-mmd-loader/blob/main/docs/HOW_TO_USE.md)

## Features

- **Treat MMD files as standard Unity assets** — PMX / VMD go through Unity's import pipeline instead of a dedicated viewer. Just drop a `.pmx` into your project and it becomes a prefab, with materials and textures set up automatically.
- **An import experience just like FBX** — Drag and drop into the Project window, then place it into the Scene / Hierarchy. There are no special MMD-specific steps to learn.
- **Secondary (cloth) physics out of the box** — Imports the rigid bodies and joints defined in the PMX and simulates them in real time with MMD's physics engine during Play Mode forward playback.
- **Edit VMD on the Timeline** — VMD motions are treated as Timeline clips, so you can use Unity's Timeline directly for blending multiple motions and scene direction. Camera and light VMD are also supported.
- **MMD-style toon rendering** — URP-based toon shading approximates MMD-style rendering, including edges, alpha, and textures.

## Support Status

| Item | Status |
| --- | --- |
| Target Unity | Currently verified only on Unity 6000.4 (Windows) |
| PMX | Import and scene placement supported |
| VMD | Import and Timeline clip supported. Motion playback is evaluated at runtime by [mmd-anim](https://github.com/yohawing/mmd-anim). Camera motion supported |
| Morph | Vertex (blend shape) / UV / material / bone / group morphs supported |
| Physics | Real-time physics during Play Mode forward playback |
| Rendering | URP-based toon, outline RendererFeature, alpha, texture diagnostics, material order handoff |
| Humanoid | Animator and proxy rig auto-setup at import. A unified Timeline track drives both the Humanoid avatar and the native MMD model, with Live physics during retargeted playback |

## Roadmap

Items we plan to work on in future releases (scope and priority may change).

| Item | Plan |
| --- | --- |
| Bake workflow polish | Expand the explicit Humanoid AnimationClip bake UX and output options |
| Timeline enhancements | Expand Timeline editing and direction features, including audio (music) synced playback |
| Runtime MMD Rig | Runtime MMD rig support (IK, append parent, axis limits, etc.) |
| Higher rendering fidelity | Improved outline fidelity and self-shadow (ShadowCaster pass + shadow sampling) |
| Broader URP pipeline support | Build on the existing outline RendererFeature and verify additional paths such as Forward+ / Deferred and Render Pipeline Asset / Volume integration |
| Runtime loading | An API to load PMX / VMD dynamically at runtime |
| macOS / Linux native | Distribute native binaries for each platform |
| Broader Unity support | Verify on Unity versions other than 6000.4 |

## Install

Add the following URL through Unity Package Manager **Add package from git URL**.

```text
https://github.com/yohawing/unity-mmd-loader.git?path=packages/com.yohawing.mmd-loader#v0.1.1
```

When referencing a local checkout, add a file dependency to the Unity project's `Packages/manifest.json`.

```json
{
  "dependencies": {
    "com.yohawing.mmd-loader": "file:../../packages/com.yohawing.mmd-loader"
  }
}
```

Adjust the relative path for your Unity project layout.

## Basic Flow

1. Import a `.pmx` file into the Unity Project.
2. Drag the imported PMX asset into the Scene or Hierarchy.
3. Import a `.vmd` file.
4. Bind the scene playback object to Timeline and create a VMD Timeline clip.
5. In Play Mode, check forward playback and real-time physics.
6. In Edit Mode, Timeline scrub is treated as physics-off animation preview.

For detailed steps, see [HOW_TO_USE.md](https://github.com/yohawing/unity-mmd-loader/blob/main/docs/HOW_TO_USE.md).

## License Boundary

This repository does not redistribute third-party PMX / VMD / texture / motion / audio / capture assets.

When using MMD assets, users must confirm the license and redistribution terms themselves. Local verification assets, generated logs, screenshots, and test artifacts are not intended to be included in package commits.

## Development Notes

The public `main` branch is kept package-first.

- The distributable package is centered on `packages/com.yohawing.mmd-loader/`.
- `native/` remains as native source / rebuild reference.
- Local scripts, the Unity consumer project, validation artifacts, and AI work notes are not public package surfaces.
- Release preparation and branch policy are centralized in [RELEASE.md](https://github.com/yohawing/unity-mmd-loader/blob/main/docs/RELEASE.md).
