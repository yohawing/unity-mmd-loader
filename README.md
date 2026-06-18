# MMD Loader

Unity package for importing, placing, and playing MMD assets as ordinary Unity
assets.

[日本語](./docs/README.ja.md) / [How to use](./docs/HOW_TO_USE.md)

MMD Loader targets the practical Unity workflow: drag a PMX model into a
project, place it in a scene, add VMD motion to Timeline, and keep the result on
Unity's standard authoring rails instead of a separate viewer-only runtime.

The default runtime is backed by [yohawing/mmd-anim](https://github.com/yohawing/mmd-anim)
through the `native/mmd-anim` submodule.

## Compatibility Matrix

### Formats

| Format | Import | Runtime apply |
| --- | --- | --- |
| PMX (model) | Supported | Supported |
| VMD (motion) | Supported | Supported |
| VMD Camera / Light | Supported | Supported |
| PMD (model) | Not supported | Not supported |
| VPD (pose) | Not supported | Not supported |
| PMM (project) | Not supported | Not supported |
| .x / .vac (accessory) | Not supported | Not supported |
| .emm / .emd / .fx (MME effects) | Not supported | Not supported |

### Features

| Feature | Status |
| --- | --- |
| Unity version | Unity 6000.4 or newer |
| Package name | `com.yohawing.mmd-loader` |
| PMX / VMD import | `ScriptedImporter` assets for `.pmx` and `.vmd` |
| Scene placement | PMX drag-and-drop and selected-asset scene loading |
| Timeline | VMD Timeline clip workflow |
| Runtime playback | PMX/VMD playback controller with native runtime handoff |
| Physics | Live physics for Play Mode forward playback; Timeline edit scrubbing keeps physics off |
| Rendering | URP baseline toon material, alpha handling, texture diagnostics, and material order handoff |
| Humanoid | Metadata/setup boundary only; full Humanoid bridge is future work |
| Bake/export | Explicit export surfaces are limited; AnimationClip writer output is future work |
| Platforms | Windows x86_64 native binary is packaged; other native platforms are not release-ready |

## Install

Add the package from a Git URL:

```text
https://github.com/yohawing/unity-mmd-loader.git?path=packages/com.yohawing.mmd-loader
```

In Unity:

1. Open **Window > Package Manager**.
2. Select **Add package from git URL**.
3. Paste the URL above.

For local development inside this repository, the package root is:

```text
packages/com.yohawing.mmd-loader
```

If another Unity project sits next to this repository, add a local file
dependency to that project's `Packages/manifest.json`:

```json
{
  "dependencies": {
    "com.yohawing.mmd-loader": "file:../../packages/com.yohawing.mmd-loader"
  }
}
```

Adjust the relative path for your project layout.

## Usage - Golden Path

1. Import a `.pmx` file into the Unity Project window.
2. Drag the imported PMX asset into the Scene or Hierarchy.
3. Import a `.vmd` file.
4. Bind the scene playback object to Timeline and create a VMD Timeline clip.
5. Enter Play Mode for forward playback with Live physics, or scrub in Edit
   Mode for animation-only Timeline preview.

The package also exposes explicit editor actions under **Tools > MMD Loader**
for scene loading, diagnostics, and authoring workflows.

## Usage - Raw PMX/VMD Playback

For local diagnostics or tool-driven workflows, `MmdRuntimeImporterComponent`
can load PMX/VMD paths directly. This path is intended for development and
local verification; normal project authoring should prefer imported assets and
Timeline bindings.

Local third-party MMD assets, textures, motions, audio, and captures are not
redistributed by this repository. Keep local licensed references outside the
package.

## Native Runtime

The packaged Windows x86_64 runtime DLL is produced from the `native/mmd-anim`
submodule and included under the package plugin folder. Other native platforms
are not release-ready yet.

Native rebuild automation is currently repository-local and Windows-oriented,
so it is intentionally not part of the public package surface.

## Development

This repository is organized around the distributable UPM package under
`packages/com.yohawing.mmd-loader`. Local validation scripts, generated logs,
screenshots, test reports, local datasets, and the consumer Unity project are
development artifacts and are kept out of the public package-first history.

## Acknowledgements

This project was developed with reference to:

- [Babylon-MMD](https://github.com/noname0310/babylon-mmd)
- [saba](https://github.com/benikabocha/saba)
- [nanoem](https://github.com/hkrn/nanoem)
- [MMD for Unity](https://github.com/mmd-for-unity-proj/mmd-for-unity)
