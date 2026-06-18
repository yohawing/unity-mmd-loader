# MMD Loader

Unity package for importing, placing, and playing MMD assets as ordinary Unity
assets.

MMD Loader targets the practical Unity workflow: drag a PMX model into a
project, place it in a scene, add VMD motion to Timeline, and keep the result on
Unity's standard authoring rails instead of a separate viewer-only runtime.

The package is distributed as `com.yohawing.mmd-loader` and targets Unity 6000.4
or newer.

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

For local development, add a file dependency to your Unity project's
`Packages/manifest.json`:

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
redistributed by this package. Keep local licensed references outside the
package.

## Native Runtime

The packaged Windows x86_64 runtime DLL is produced from the `native/mmd-anim`
submodule and included under the package plugin folder. Other native platforms
are not release-ready yet.

Native rebuild automation is repository-local and platform-specific, so it is
not part of the public package surface.

## Tests

Use Unity Test Runner for package EditMode and PlayMode tests. The package tests
cover import contracts, runtime evaluation, scene binding, Timeline behavior,
rendering handoff, and Live physics boundaries without requiring licensed
third-party MMD assets.

## License Boundary

Do not commit or redistribute licensed PMX/VMD files, textures, motions, audio,
or captures unless redistribution provenance is explicitly cleared. Committed
fixtures in this package are intended to be redistribution-safe; third-party MMD
content is not included.

## Acknowledgements

This project was developed with reference to:

- [Babylon-MMD](https://github.com/noname0310/babylon-mmd)
- [saba](https://github.com/benikabocha/saba)
- [nanoem](https://github.com/hkrn/nanoem)
- [MMD for Unity](https://github.com/mmd-for-unity-proj/mmd-for-unity)
