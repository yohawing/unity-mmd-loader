# unity-mmd-loader

![unity-mmd-loader](./assets/main-image.png)

> クレジット — モデル: [Sour](https://bowlroll.net/file/146103) ／ モーション: [mobiusP](https://www.nicovideo.jp/watch/sm42576784) ／ カメラモーション: [koko](https://bowlroll.net/file/305434) ／ 背景: [とじる](https://seiga.nicovideo.jp/seiga/im11796453)

unity-mmd-loader は、PMX / VMD を Unity に取り込むためのプラグインです。
自然なPMXインポーターとTimeline統合されたVMDインポーターと、URPをカスタムしたMMDシェーダーも提供します。

[English](../README.md) / [詳しい使い方（英語）](./HOW_TO_USE.md)

## クイックスタート

1. パッケージを導入し、Package Managerから`Basic Playback` sampleをインポートします。
2. `.pmx`と参照テクスチャをインポートし、PMXアセットをSceneまたはHierarchyへドラッグします。
3. `.vmd`をインポートし、配置した再生オブジェクトへbindした`MmdVmdTimelineTrack`に追加します。
4. Edit ModeではTimelineをスクラブしてanimation-only previewを確認します。編集中の物理は意図的にoffです。
5. Play Modeに入り、Timelineをforward再生するとLive physicsが動作します。
6. Unity Humanoid clipが必要な場合はPMX RigをHumanoidに設定し、`MMD Humanoid Animation Track` からアニメーションを追加してください。 

## 要件と対応状況

| 項目 | 現在の対応 |
| --- | --- |
| 対象環境 | Unity 6000.4 / Windows x86_64 / URP |
| モデル | PMXのインポートとシーンへの配置に対応(PMD非対応) |
| VMD | インポートとTimelineクリップに対応。モーション再生は [mmd-anim](https://github.com/yohawing/mmd-anim) によるランタイム評価。カメラモーション対応済み |
| モーフ | 頂点（ブレンドシェイプ）／ UV ／材質／ボーン／グループの各モーフに対応 |
| 物理 | Play Mode の通常再生中はリアルタイム物理に対応 |
| レンダリング | URP ベースのトゥーン、アウトライン、半透明マテリアルの描画順、セルフシャドウに対応 |
| Humanoid | インポート時に Animator とプロキシリグを自動セットアップ。 既存のモーションアセットをリターゲットできます。 |

## インストール

Unity Package Managerの **Add package from git URL** に以下を指定します。

```text
https://github.com/yohawing/unity-mmd-loader.git?path=packages/com.yohawing.mmd-loader
```
