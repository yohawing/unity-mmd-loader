# Testing Guide

This document is the practical index for `unity-mmd-loader` verification. It
does not replace the contract boundaries in `docs/ARCHITECTURE.md`; use it to choose
the right command before changing parser, runtime, rendering, or workflow
behavior.

## Operating Rule: Scripts Are Scaffolds, Tests Are The Deliverable

The single source of verification truth is the repo-side C# test suite under
`packages/**/Tests` (EditMode / PlayMode / Contracts). Standalone verification
scripts are scaffolding for exploration and in-flight implementation — not a
parallel test suite to be maintained forever.

1. **作ってよい** — 実装・調査中の確認スクリプト (PowerShell / Python / CLI) は
   自由に作る。探索の足場として歓迎する。
2. **「完了」の定義** — スクリプトが確かめている振る舞い／契約が実装され、緑に
   なった瞬間が完了。
3. **落とす → 消す** — 完了時に、そのアサーションを Repo 側テスト
   (EditMode / PlayMode C#) へ移植し、スクリプトを削除する。1 実装の締めに
   「スキャフォールド棚卸し」を 1 ステップ必ず挟む。
4. **アサーションは弱めない** — 移植は等価以上。tolerance / golden / pass-fail を
   緩めて通すのは禁止。
5. **例外＝消さない恒久 CLI** — build / Unity 起動 / orchestration 系の薄い CLI は
   「インフラ」であって検証足場ではないので対象外 (下記 `scripts/` の生存分が
   これ)。

### 禁止パターン (2026-06 の tools/ 肥大化の教訓)

- **phase 名で番号を増やし続ける** (phase10→18 の累積が温床だった)。
- **1 アサーション = 1 スクリプト + 1 fixture** (negative fixture の爆発)。
- **「成果物 JSON を別ツールで検証」する間接テスト**。振る舞いは C# の `Assert`
  一行に寄せる。
- **wrapper + body の二重化** (コマンドを短くするためだけの薄い shim)。
- **非再現の対話診断を「テスト」と呼ぶ** (editor セッション状態に依存する smoke)。

## Default Gate

Run this before treating a local change as green:

```powershell
.\scripts\check-cli.ps1
```

The gate accepts `-Tier fast|unity|full`; the default is `full`.

| Tier | Scope |
| --- | --- |
| `fast` | Non-Unity tier. The standalone non-Unity verification layer was retired, so this is now a near-noop kept for the dispatch contract. Use for docs-only changes. |
| `unity` | Unity compile plus EditMode tests. Use when changing package source, the Unity object factory, or editor-only surfaces. |
| `full` | Release-facing gate: Unity compile, EditMode, and PlayMode when Unity is available. Default when `-Tier` is omitted. |

Important rules:

- Do not relax tolerances, golden values, comparer pass/fail criteria, or skip
  conditions to make this pass.
- Unity-backed commands must not be run in parallel against the same
  `unity-mmd` project. Unity batchmode rejects concurrent opens and that failure
  is infrastructure noise.
- Generated logs, traces, screenshots, and reports belong under `artifacts/`.

The gate is fail-closed. A required child stage that reports `SKIP` or has no
recognizable `PASS` status fails the wrapper. For release XML, use
`scripts/read-nunit-test-result.ps1`: the result file must exist, parse, contain
non-zero internally consistent counts, and have no failed, inconclusive, or
invalid tests. The configured minimum total/passed counts must be met, and each
skip must be named in the exact allowlist for that evidence. A count-only,
wildcard, missing, or unexpected skip is not proof of a green run.

When a child check fails, the wrapper prints the captured child output before
reporting the failing check name and exit code. Use `-Detailed` only when a
passing run still needs full child output.

For C# changes under `packages/com.yohawing.mmd-loader/Runtime/` or
`UnityIntegration`, run `.\scripts\check-cli.ps1 -Tier unity` before commit even
when `-Tier fast` passes. The fast tier skips Unity compile and EditMode, so it
cannot catch Unity assembly or API-surface failures.

## Unity Test Framework

The package uses Unity Test Framework through batchmode wrappers.

```powershell
.\scripts\unity-editmode-tests.ps1
```

EditMode tests cover native parser PMX/VMD IR contract checks, import / factory
behavior, package fixture inventory, runtime/frame descriptors, and editor
workflow surfaces that can be tested without Play Mode.

```powershell
.\scripts\unity-playmode-tests.ps1
```

PlayMode tests are the dedicated entry point for runtime behavior that needs
Unity player-style execution, including **live physics (揺れもの)**: the hair
physics fixture tests assert that mmd-anim native stepping propagates to the rig
(bone transforms change across frames) and that diagnostics report pinned
bodies, step time, and unsupported joints. Host-driven live playback uses the
mmd-anim FFI physics world; clip-driven physics baking is covered by native
runtime parity checks. A local diagnostic run may report an explicit native
backend skip when that dependency is unavailable, but release evidence does not
accept PlayMode skips. Prefer EditMode when the behavior can be checked without
entering Play Mode.

Use `.\scripts\unity-compile.ps1` when the goal is only import/compile health.
Batchmode exit code alone is not enough; the wrapper scans Unity logs for
package-resolution and compiler-error failures.

## Surviving `scripts/` (permanent infrastructure CLIs)

These are the orchestration / build entry points exempt from the scaffold rule:

```text
check-cli.ps1 / check-cli.full.ps1 / check-cli/{common,unity}.ps1   # gate
build-mmd-runtime-ffi.ps1                                         # native build
unity-compile.ps1 / unity-editmode-tests.ps1 / unity-playmode-tests.ps1
run-local-asset-fixture-gate.ps1 / run-mmd-anim-cli-parity-report.ps1
```

`build-mmd-runtime-ffi.ps1` builds the `mmd-anim` FFI plugin, including the
native physics exports used by host-driven playback and clip-driven baking.
Run it when the native source or submodule changes.

For release-candidate machine evidence, collect
`.\scripts\unity-lts-compatibility-gate.ps1` and the default
`.\scripts\check-cli.ps1` full gate. Re-run
`run-local-asset-fixture-gate.ps1` and `run-mmd-anim-cli-parity-report.ps1` when
package/native/runtime changes affect their covered behavior. The local-asset
gate is report-only by default for licensed local assets; use
`-RequireLocalAssets` only when the local corpus should be a hard gate. The
parity script writes dedicated CLI-vs-packaged-native evidence under
`artifacts/parity/`.

## Parser And Fixture Checks

Committed parser model fixtures live under
`packages/com.yohawing.mmd-loader/Tests/Fixtures/`. They are validated by EditMode
contract tests — there is no separate standalone manifest validator.

Rules:

- Package fixtures must be redistribution-safe.
- Missing or mismatched model goldens fail the normal contract tests. A
  maintainer can opt in to `YMU_GENERATE_GOLDENS=1` to create candidates under
  `artifacts/golden-candidates/`; candidates are ignored, must be reviewed for
  provenance and parser drift, and are never promoted automatically by a test.
- Licensed local PMX/VMD files stay outside the repo and are referenced through
  `data-local/fixtures.local.json` (gitignored). Such cases are diagnostics
  until provenance is cleared and the fixture is promoted into a committed,
  redistribution-safe test.

## Related Contracts

- `CompatibilitySurfaceContractTests` (serialized/public/native/asmdef surface)
- `scripts/read-nunit-test-result.ps1` (strict NUnit evidence policy)

- `docs/ARCHITECTURE.md` (`Animation Boundaries`, trace schema)
- `docs/ARCHITECTURE.md` (`Runtime API And Snapshot Boundaries`)
- `docs/ARCHITECTURE.md` (`Import Boundaries`)
- `docs/ARCHITECTURE.md` (`Validation Artifact Boundaries`)
