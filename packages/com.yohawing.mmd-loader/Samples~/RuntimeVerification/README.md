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

Directory sweep:

```powershell
.\artifacts\runtime-verification\MmdRuntimeVerification.exe `
  --dir F:\MMD\SomeFolder `
  --out F:\Develop\MMDDev\unity-mmd-loader\artifacts\runtime-verification\sweep.json
```

`--dir` by itself is a parse-only PMX/VMD sweep. Use `--dir` with `--vmd` to
run each PMX in the directory against one VMD, or `--dir` with `--pmx` to run
one PMX against each VMD in the directory.

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
