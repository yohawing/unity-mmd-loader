# Runtime Verification Sample

This sample builds a small standalone player that verifies runtime PMX/VMD loading
without using the ScriptedImporter path. It is intended for local validation with
licensed MMD assets that are not included in this package.

## Assets

No PMX, VMD, texture, or other licensed MMD asset is bundled with this sample.
Pass local file paths at runtime.

## Build

From the repository root:

```powershell
.\scripts\build-runtime-verification-player.ps1
```

The script copies this sample into the ignored Unity consumer project path
`unity-mmd/Assets/RuntimeVerification`, then writes logs and player output under
`artifacts/runtime-verification`.

## Run

Single Timeline-driven runtime check:

```powershell
.\artifacts\runtime-verification\MmdRuntimeVerification.exe `
  --pmx F:\MMD\model.pmx `
  --vmd F:\MMD\motion.vmd `
  --out F:\Develop\MMDDev\unity-mmd-loader\artifacts\runtime-verification\report.json `
  --duration 3 `
  --frame-rate 30 `
  --drive timeline
```

Controller-driven check with the managed path forced after configuration:

```powershell
.\artifacts\runtime-verification\MmdRuntimeVerification.exe `
  --pmx F:\MMD\model.pmx `
  --vmd F:\MMD\motion.vmd `
  --out F:\Develop\MMDDev\unity-mmd-loader\artifacts\runtime-verification\managed-report.json `
  --drive controller `
  --fast-runtime off
```

URP Lit material preset visual smoke:

```powershell
.\artifacts\runtime-verification\MmdRuntimeVerification.exe `
  --pmx F:\MMD\model.pmx `
  --vmd F:\MMD\motion.vmd `
  --out F:\Develop\MMDDev\unity-mmd-loader\artifacts\runtime-verification\urp-lit-report.json `
  --drive timeline `
  --material-preset urp-lit
```

Physics max-substep A/B diagnostic:

```powershell
.\artifacts\runtime-verification\MmdRuntimeVerification.exe `
  --pmx F:\MMD\model.pmx `
  --vmd F:\MMD\motion.vmd `
  --out F:\Develop\MMDDev\unity-mmd-loader\artifacts\runtime-verification\physics-candidate.json `
  --sample-frames 0,30,60,90 `
  --dump-bones `
  --drive controller `
  --physics-max-substep-fixed-step 1/120
```

Directory sweep:

```powershell
.\artifacts\runtime-verification\MmdRuntimeVerification.exe `
  --dir F:\MMD\SomeFolder `
  --out F:\Develop\MMDDev\unity-mmd-loader\artifacts\runtime-verification\sweep.json
```

`--dir` by itself is a parse-only PMX/VMD sweep. Use `--dir` with `--vmd` to
run each PMX in the directory against one VMD, or `--dir` with `--pmx` to run
one PMX against each VMD in the directory.

Fixture manifest playback sweep:

```powershell
.\artifacts\runtime-verification\MmdRuntimeVerification.exe `
  --fixture-manifest F:\Develop\MMDDev\data\fixtures.local.json `
  --out F:\Develop\MMDDev\unity-mmd-loader\artifacts\runtime-verification\manifest-report.json `
  --duration 3
```

`--fixture-manifest` reads the shared `fixtures.local.json`
`paths.playbackSmoke.cases` format used by three-mmd-loader. It resolves PMX
model and VMD motion keys through `paths.releaseSmoke.byExtension`. The
`MMD_RUNTIME_VIEWER_FIXTURES` environment variable can provide the manifest path
when the command-line option is omitted. Optional viewer fields such as camera,
audio, audio offset, and background are resolved and kept for the viewer UI
path, but this verification player only drives PMX + VMD playback for now.
Cases with `skipReason` are skipped by the verification sweep while remaining
available to the viewer descriptor list. Test-only fields such as oracle, watch
bones, epsilon, and frame lists are ignored. A case may set `materialPreset` to
`MmdToon` or `UrpLit`; `expectedFeatures` can include `urp-lit-preset` to fail
the gate unless the report captured the URP Lit shader binding.

Interactive runtime viewer:

```powershell
.\artifacts\runtime-verification\MmdRuntimeVerification.exe `
  --viewer `
  --fixture-manifest F:\Develop\MMDDev\data\fixtures.local.json
```

`--viewer` keeps the player open, lists manifest cases, and reloads PMX + VMD
playback when a case is selected. Camera, audio, background, and audio-offset
references are shown as resolved case metadata; runtime application for those
optional fields is a later viewer slice.

## JSON Schema Overview

The report is emitted to `--out` and also logged as a fallback. The top-level
object includes:

- `schemaVersion`: currently `1`.
- `caseResults[]`: one entry for single runs and one entry per sweep case.
- `model` / `motion`: direct native parser count summaries.
- `playback`: controller configuration, final frame, and fast-runtime state.
- `physics`: Live physics diagnostics summary when available.
- `sampledFrames[]`: reserved for later oracle comparisons.
- `status` and `exitCode`: aggregate result.
