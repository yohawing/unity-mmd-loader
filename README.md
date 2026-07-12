# unity-mmd-loader

![unity-mmd-loader](https://raw.githubusercontent.com/yohawing/unity-mmd-loader/main/docs/assets/main-image.png)

> Credits — Model: [Sour](https://bowlroll.net/file/146103) / Motion: [mobiusP](https://www.nicovideo.jp/watch/sm42576784) / Camera motion: [koko](https://bowlroll.net/file/305434) / Background: [Tojiru](https://seiga.nicovideo.jp/seiga/im11796453)

unity-mmd-loader is a plugin for importing PMX / VMD into Unity.
It provides a native-feel PMX importer, a Timeline-integrated VMD importer, and custom URP-based MMD shaders.

[日本語](https://github.com/yohawing/unity-mmd-loader/blob/main/docs/README.ja.md) / [Detailed guide](https://github.com/yohawing/unity-mmd-loader/blob/main/docs/HOW_TO_USE.md)

## Features

- **Treat MMD files as ordinary Unity assets** — PMX / VMD go through Unity's import pipeline instead of a dedicated viewer. Just drop a `.pmx` into the project and it becomes a prefab, with materials and textures set up automatically.
- **Edit VMD on the Timeline** — VMD motion is handled as Timeline clips. In addition to Humanoid motion, camera and light VMD are also supported.
- **MMD-style toon rendering** — A URP-based MMD shader brings edges, alpha, and textures closer to the MMD look.

## Requirements and support

| Item | Current support |
| --- | --- |
| Target environment | Unity 6000.4 / Windows x86_64 / URP |
| Models | PMX import and scene placement (PMD is not supported) |
| VMD | Import and Timeline clips. Motion playback is runtime evaluation by [mmd-anim](https://github.com/yohawing/mmd-anim). Camera motion supported |
| Morphs | Vertex (BlendShape), UV, material, bone, and group morphs |
| Physics | Real-time physics during normal Play Mode playback |
| Rendering | URP-based toon, outline, transparent material draw order, and self-shadow |
| Humanoid | Animator and proxy rig set up automatically on import. Existing motion assets can be retargeted. |

## Install

In Unity Package Manager, choose **Add package from git URL**:

```text
https://github.com/yohawing/unity-mmd-loader.git?path=packages/com.yohawing.mmd-loader
```
