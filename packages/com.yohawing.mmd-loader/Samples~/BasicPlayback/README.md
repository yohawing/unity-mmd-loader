# Basic Playback sample

このサンプルは、PMX/VMD golden path を最小構成で確認するためのサンプルです。
再配布可能なテスト用 PMX/VMD を同梱しています。

## Included assets

- `Assets/mmt_test_model.pmx`
- `Assets/mmt_test_model_test_motion.vmd`

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

1. Package Manager から `Basic Playback` sample を import します。
2. Project ウィンドウで `mmt_test_model.pmx` をシーンにドラッグして配置します。
3. `mmt_test_model_test_motion.vmd` を Timeline の MMD VMD track に配置し、配置済み PMX の `MmdUnityPlaybackController` を track binding に設定します。
4. Play Mode で PMX/VMD playback を確認します。
5. Humanoid 化している場合だけ、必要に応じて Humanoid Clip bake を試します。

このサンプルは最小の release visual baseline です。
外部 MMD 資産の再現性や最終 shading parity を保証するものではありません。
