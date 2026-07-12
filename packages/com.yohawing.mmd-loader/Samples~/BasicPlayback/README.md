# Basic Playback sample

このサンプルは、PMX/VMD golden path を最小構成で確認するためのサンプルです。
再配布可能なテスト用 PMX/VMD を同梱しています。

## Included assets

- `Assets/mmt_test_model.pmx`
- `Assets/mmt_test_model_test_motion.vmd`
- `Assets/BasicSampleTimeline.playable`
- `Assets/BasicPlayback.unity`

これらは `maya_mmd_tools` のテスト用に作成された再配布可能な小型モデル/モーションです。
一般の MMD モデル、モーション、テクスチャ、PMM は同梱していません。
追加素材を使う場合は、各素材のライセンス条件を確認してください。

## Purpose

PMX/VMD golden path を次の順序で確認します。

1. PMX drag
2. Scene placement
3. VMD to Timeline
4. optional Humanoid Clip bake

## Steps

1. Package Manager から `Basic Playback` sample を importします。
2. `Assets/BasicPlayback.unity` を開きます。
3. `Basic Timeline` の Timeline をスクラブするか、Play Mode で PMX/VMD playback を確認します。

シーンには PMX モデル、VMD clip入りTimeline Asset、`MmdUnityPlaybackController` への
track bindingが設定済みです。手動の golden path を確認する場合は、新規シーンで
`mmt_test_model.pmx` を配置し、`mmt_test_model_test_motion.vmd` をTimelineへ追加してください。

このサンプルは最小の release visual baseline です。
外部 MMD 資産の再現性や最終 shading parity を保証するものではありません。
