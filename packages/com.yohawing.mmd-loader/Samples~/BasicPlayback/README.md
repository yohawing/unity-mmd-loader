# Basic Playback sample

このサンプルは、PMX/VMD golden path を最小構成で再現するための案内ページです。
`Assets/` 配下のローカル素材を使って確認します。

## 注意: 同梱資産の境界

- 本サンプルは PMX / VMD の実データを同梱しません。
- ライセンス制約のある PMX/VMD はこのパッケージに含めない設計です。
- 同梱されるのは手順のみで、検証素材はユーザー各自の `Assets/` に配置します。

## 目的

PMX/VMD golden path を次の順序で再確認する:

1. PMX drag
2. Scene placement
3. VMD to Timeline
4. optional Humanoid Clip bake

## 実行手順

1. ローカルの PMX / VMD ファイル（有効なモデル/モーション）を Unity の `Assets/` 配下にコピーします。
   同じライセンス条件で扱えるファイルのみ使用してください。
2. Project ウィンドウから PMX をシーンにドラッグし、配置します。
3. VMD をインポートし、`Assets/MMD Loader/Add VMD Clip to selected Timeline` または MMD EditorWindow の `Create VMD Timeline Clip` で上記モデルへ Timeline clip を作成します。
4. `Play` して PMX 拡張再生経路で再生を確認します。
5. 必要に応じて、Humanoid 化している場合のみ `Humanoid Clip bake`（必要なら）を試します。
   試せない環境ではこの工程はスキップし、必須要件として扱いません。

このサンプルは最小骨格です。
実データやシーン/Prefab の完全版は同梱していません。
必要ならパッケージの `Documentation~/README.md` を参照して拡張してください。
