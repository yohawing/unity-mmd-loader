# unity-mmd-loader

![unity-mmd-loader](./assets/main-image.png)

> クレジット — モデル: [Sour](https://bowlroll.net/file/146103) ／ モーション: [mobiusP](https://www.nicovideo.jp/watch/sm42576784) ／ カメラモーション: [koko](https://bowlroll.net/file/305434) ／ 背景: [とじる](https://seiga.nicovideo.jp/seiga/im11796453)

unity-mmd-loader は、PMX / VMD を Unity に取り込むためプラグインです。

モダンなUnityの特徴をいかした自然なインポート体験を目指して設計されていて、
Unity の Project / Scene / Timeline / Runtime 上で PMX モデルと VMD モーションをそのまま扱えるようにしています。

[English](../README.md) / [使い方](./HOW_TO_USE.md)

## 特徴

- **MMD ファイルを Unity 標準アセットとして扱える** — PMX / VMD を専用ビューアではなく Unity のインポートパイプラインに乗せます。`.pmx` をプロジェクトに入れるだけでプレハブ化され、マテリアルとテクスチャも自動でセットアップされます。
- **VMD を Timeline で編集** — VMDモーションを Timeline クリップとして扱えるので、複数モーションの合成やシーン演出に Unity の Timeline がそのまま使えます。カメラ・ライトの VMD にも対応。
- **MMD らしいトゥーン表現** — URPベースのMMDシェーダーで、エッジやアルファ、テクスチャ周りを MMD らしい見た目に近づけます。

## 対応状況

| 項目 | 状態 |
| --- | --- |
| 対象環境 | Unity 6000.4 / Windows x86_64 / URP |
| モデル | PMXのインポートとシーンへの配置に対応(PMD非対応) |
| VMD | インポートとTimelineクリップに対応。モーション再生は [mmd-anim](https://github.com/yohawing/mmd-anim) によるランタイム評価。カメラモーション対応済み |
| モーフ | 頂点（ブレンドシェイプ）／ UV ／材質／ボーン／グループの各モーフに対応 |
| 物理 | Play Mode の通常再生中はリアルタイム物理に対応 |
| レンダリング | URP ベースのトゥーン、アウトライン RendererFeature、アルファ、テクスチャ診断、マテリアル順序の引き渡しに対応 |
| Humanoid | インポート時に Animator とプロキシリグを自動セットアップ。 既存のモーションアセットをリターゲットできます。 |

## 対応予定

今後のリリースで対応を進めたい項目です（内容・優先度は変わる可能性があります）。

| 項目 | 予定 |
| --- | --- |
| Timeline 機能の強化 | オーディオ（楽曲）同期再生など、Timeline 上での編集・演出機能の拡充。 |
| Runtime MMD Rig | 実行時の MMD リグ（IK、付与親、軸制限 など）への対応 |
| レンダリング忠実度の向上 | アウトラインの忠実度向上、セルフシャドウ（ShadowCaster パス＋シャドウサンプリング）への対応。 |
| URP パイプライン対応の拡充 | 既存のアウトライン RendererFeature を基準に、Forward+ / Deferred など追加パスや Render Pipeline Asset・Volume 設定との連携を検証する |
| ランタイムロード | 実行時に PMX / VMD を動的に読み込む API |
| macOS / Linux ネイティブ | 各プラットフォーム向けネイティブバイナリの配布を予定 |
| 対象 Unity の拡大 | Unity 6000.4 以外のバージョンでの検証を予定 |

## インストール

Unity Package Manager の **Add package from git URL** で以下の URL を追加します。

```text
https://github.com/yohawing/unity-mmd-loader.git?path=packages/com.yohawing.mmd-loader
```

## 基本的な流れ

1. `.pmx` をテクスチャも含めてUnityにインポートする。
2. インポートした PMX アセットを Scene または Hierarchy にドラッグする。
3. `.vmd` をインポートする。
4. シーン上の再生対象オブジェクトを Timeline に割り当て、VMD の Timeline クリップを作成する。
5. Play Mode で通常再生とリアルタイム物理を確認する。
6. Edit Mode の Timeline スクラブは、物理を無効にしたアニメーションのプレビューとして扱う。

詳しい手順は [HOW_TO_USE.md](./HOW_TO_USE.md) を参照してください。

## ライセンス境界

このリポジトリは、第三者の PMX / VMD / テクスチャ / モーション / 音声 / キャプチャを再配布しません。

MMD アセットを使う場合は、利用者自身がライセンスと再配布の可否を確認してください。ローカルの検証用素材、生成ログ、スクリーンショット、テスト成果物は、パッケージのコミットに含めない前提です。
