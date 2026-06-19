# unity-mmd-loader

![unity-mmd-loader](./assets/main-image.png)

> クレジット — モデル: [Sour](https://bowlroll.net/file/146103) ／ モーション: [mobiusP](https://www.nicovideo.jp/watch/sm42576784) ／ カメラモーション: [koko様](https://bowlroll.net/file/305434)

unity-mmd-loader は、PMX / VMD を Unity に取り込むためプラグインです。

モダンなUnityの特徴をいかした自然なインポート体験を目指して設計されていて、
Unity の Project / Scene / Timeline / Runtime 上で PMX モデルと VMD モーションをそのまま扱えるようにしています。

[English](../README.md) / [使い方](./HOW_TO_USE.md)

## 特徴

- **MMD ファイルを Unity 標準アセットとして扱える** — PMX / VMD を専用ビューアではなく Unity のインポートパイプラインに乗せます。`.pmx` をプロジェクトに入れるだけでプレハブ化され、マテリアルとテクスチャも自動でセットアップされます。
- **FBX と同じ感覚のインポート体験** — Project ウィンドウにドラッグ＆ドロップして、Scene / Hierarchy に置くだけ。MMD のための特別な手順を覚える必要はありません。
- **揺れもの物理をそのまま再生** — PMX に設定された剛体・ジョイントを取り込み、Play Mode の通常再生中はMMDの物理エンジンでリアルタイムに揺らします。
- **VMD を Timeline で編集** — VMDモーションを Timeline クリップとして扱えるので、複数モーションの合成やシーン演出に Unity の Timeline がそのまま使えます。カメラ・ライトの VMD にも対応。
- **MMD らしいトゥーン表現** — URP ベースのトゥーンシェーディングで、エッジやアルファ、テクスチャ周りも MMD シェーディングを再現しています。

## 対応状況

| 項目 | 状態 |
| --- | --- |
| 対象 Unity | 現状、Windows Unity 6000.4 のみで検証されています|
| PMX | インポートとシーンへの配置に対応 |
| VMD | インポートとTimelineクリップに対応。モーション再生は [mmd-anim](https://github.com/yohawing/mmd-anim) によるランタイム評価。カメラモーション対応済み |
| モーフ | 頂点（ブレンドシェイプ）／ UV ／材質／ボーン／グループの各モーフに対応 |
| 物理 | Play Mode の通常再生中はリアルタイム物理に対応 |
| レンダリング | URP ベースのトゥーン、アルファ、テクスチャ診断、マテリアル順序の引き渡しに対応 |
| Humanoid | メタデータとセットアップの土台まで。完全な Humanoid 連携は今後の課題 |

## 対応予定

今後のリリースで対応を進めたい項目です（内容・優先度は変わる可能性があります）。

| 項目 | 予定 |
| --- | --- |
| VMD のアニメーションクリップ化 | VMD モーションを Unity の AnimationClip にベイクする機能 |
| Timeline 機能の強化 | オーディオ（楽曲）同期再生など、Timeline 上での編集・演出機能の拡充 |
| Runtime MMD Rig | 実行時の MMD リグ（IK、付与親、軸制限 など）への対応 |
| レンダリング忠実度の向上 | アウトラインの忠実度向上、セルフシャドウ（ShadowCaster パス＋シャドウサンプリング）への対応 |
| URP パイプライン統合 | アウトラインを ScriptableRendererFeature（カスタムパス）として組み込み。Forward / Forward+ / Deferred の各レンダリングパスへの対応と、Render Pipeline Asset・Volume 設定との連携を進める |
| ランタイムロード | 実行時に PMX / VMD を動的に読み込む API |
| macOS / Linux ネイティブ | 各プラットフォーム向けネイティブバイナリの配布を予定 |
| 対象 Unity の拡大 | Unity 6000.4 以外のバージョンでの検証を予定 |

## インストール

Unity Package Manager の **Add package from git URL** で以下の URL を追加します。

```text
https://github.com/yohawing/unity-mmd-loader.git?path=packages/com.yohawing.mmd-loader
```

ローカルにチェックアウトしたものを参照する場合は、Unity プロジェクトの `Packages/manifest.json` にファイル参照の依存関係を追加します。

```json
{
  "dependencies": {
    "com.yohawing.mmd-loader": "file:../../packages/com.yohawing.mmd-loader"
  }
}
```

相対パスは、利用する Unity プロジェクトの配置に合わせて調整してください。

## 基本的な流れ

1. `.pmx` を Unity の Project にインポートする。
2. インポートした PMX アセットを Scene または Hierarchy にドラッグする。
3. `.vmd` をインポートする。
4. シーン上の再生対象オブジェクトを Timeline に割り当て、VMD の Timeline クリップを作成する。
5. Play Mode で通常再生とリアルタイム物理を確認する。
6. Edit Mode の Timeline スクラブは、物理を無効にしたアニメーションのプレビューとして扱う。

詳しい手順は [HOW_TO_USE.md](./HOW_TO_USE.md) を参照してください。

## ライセンス境界

このリポジトリは、第三者の PMX / VMD / テクスチャ / モーション / 音声 / キャプチャを再配布しません。

MMD アセットを使う場合は、利用者自身がライセンスと再配布の可否を確認してください。ローカルの検証用素材、生成ログ、スクリーンショット、テスト成果物は、パッケージのコミットに含めない前提です。

## 開発メモ

公開している `main` ブランチは、パッケージを中心とした構成にしています。

- 配布対象は `packages/com.yohawing.mmd-loader/` が中心です。
- `native/` は、ネイティブのソースと再ビルドの参照用として残しています。
- ローカルのスクリプト、利用側の Unity プロジェクト、検証用の成果物、AI の作業メモは、公開パッケージには含めません。
- リリース準備とブランチ運用方針は [RELEASE.md](./RELEASE.md) にまとめています。
