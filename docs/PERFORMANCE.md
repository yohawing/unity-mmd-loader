# Performance baseline (P0)

`scripts/run-performance-baseline.ps1` は、tracked fixture を使った load / animation / playback の基準値を Unity batchmode から取得する唯一の P0 入口です。出力は caller が指定した JSON path（既定は `artifacts/performance/performance-baseline.json`）へ書き込まれます。

```powershell
.\scripts\run-performance-baseline.ps1
.\scripts\run-performance-baseline.ps1 -OutputPath artifacts\performance\candidate.json -BaselinePath artifacts\performance\baseline.json
```

既定値は warmup 5 frame、measurement 120 frame、30 fps です。P0 では再現条件を固定するため、`-WarmupFrames` は 5 以上、`-FrameCount` は 120 以外を受け付けません。PMX / VMD は `Tests/Fixtures/Assets/test_1bone_cube.*`、physics fixture は `test_hair_physics.pmx` です。いずれかが欠けている場合は synthetic fixture を作らず、JSON `status: "SKIP"` として終了します。

## Report schema

`schemaVersion: 1` と `schema: "mmd-performance-baseline"` を固定します。トップレベルには次を記録します。

- PMX / VMD / physics fixture SHA-256
- Unity version、package HEAD、`native/mmd-anim` revision、ABI、backend、`SystemInfo.processorType` / CPU count
- warmup / measurement frame 数、frame rate
- deterministic result checksum
- 各 phase の `status`、sample 数、p50 / p95 / p99、GC bytes/frame

phase は `pmx-load`、`pmx-parse`、`vmd-load-parse`、`unity-asset-build`、`native-evaluate-copy`、`unity-pose-morph-apply`、`live-physics-total` を持ちます。Live Physics の evaluate / sync / step / apply は現在の production binding が一つの `ApplyFrame` にまとめているため、`live-physics-evaluate`、`live-physics-sync`、`live-physics-step`、`live-physics-apply` を `UNAVAILABLE` とし、推測値を記録しません。

`test_hair_physics.pmx` に含まれる両 endpoint が `-1` の pure world-anchor joint は current production validator が非対応として拒否するため、既存 Live Physics regression test と同じく benchmark 用の in-memory model から除外します。fixture file と SHA-256 は変更しません。

`native-evaluate-copy` は内部 `MmdRuntimeFfiPlaybackSession` を一度生成し、world matrix / morph / IK の caller-owned buffer を再利用します。計測中の timer は `Stopwatch.GetTimestamp()` だけを使うため、benchmark 自身の Stopwatch オブジェクト生成を GC bytes/frame に混ぜません。

`PASS` phase の分位点は、昇順サンプル列に対する線形補間です。`UNAVAILABLE` は独立計測不能、`SKIP` は fixture・license・native backend などの実行条件不足を表します。`SKIP` を `PASS` として扱うことは禁止です。

予期しない例外、baseline の欠落・読込失敗・schema 不一致は `ERROR` です。baseline comparer の必須 phase は上記 7 phase で、欠落・重複・空サンプル・`SKIP` / `UNAVAILABLE` は fail とします。

## Comparer and process semantics

`-BaselinePath` を指定すると、report と baseline を比較します。既定閾値は p95 / p99 / GC bytes per frame の 10% regression です。deterministic checksum が一致しない場合も fail します。必須 phase の欠落・重複・SKIP / UNAVAILABLE・空サンプルも fail します。閾値を変更する場合は JSON comparer を直接利用し、変更理由をレビューに残してください。

PowerShell の終了コードは次の通りです。

- `0`: report が `PASS`
- `1`: threshold / checksum の `FAIL` または baseline / benchmark 実行中の `ERROR`
- `2`: `SKIP`（fixture、Unity license、native backend、batchmode infrastructure など）

Unity Editor が同じ project を開いている間は、同じ project へ batchmode を並列実行しないでください。license が無い、または Unity が report を生成できない場合も、script は explicit `SKIP` JSON を生成して `2` で終了します。

The implementation intentionally does not alter runtime code. A future phase may add independent production instrumentation only when a measured baseline shows that the combined phase hides a material regression.
