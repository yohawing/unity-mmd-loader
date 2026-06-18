# Yohawing MMD Unity

Yohawing MMD Unity は、PMX / VMD を Unity の通常の資産ワークフローに乗せるための UPM package です。

目標は、MMD 専用ビューアではなく、Unity の Project、Scene、Timeline、Play Mode 上で PMX モデルと VMD モーションを扱えるようにすることです。

## 対応状況

| 項目 | 状態 |
| --- | --- |
| 対象 Unity | Unity 6000.4 以降 |
| package 名 | `com.yohawing.mmd-unity` |
| PMX | import / scene placement 対応 |
| VMD | import / Timeline clip 対応 |
| Camera / Light VMD | runtime apply 対応 |
| Physics | Play Mode の forward playback で Live physics 対応 |
| Timeline scrub | animation-only。Edit Mode scrub では physics off |
| Rendering | URP baseline toon、alpha、texture diagnostics、material order handoff |
| Humanoid | metadata / setup boundary。完全な Humanoid bridge は future |
| native platform | Windows x86_64 packaged。macOS / Linux native binary は未配布 |

PMD、VPD、PMM、accessory、MME effect project、rayMMD 互換は現在の release path には含めていません。

## インストール

Unity Package Manager の **Add package from git URL** で以下を追加します。

```text
https://github.com/yohawing/unity-mmd-loader.git?path=packages/com.yohawing.mmd-unity
```

local checkout から参照する場合は、Unity project の `Packages/manifest.json` に file dependency を追加します。

```json
{
  "dependencies": {
    "com.yohawing.mmd-unity": "file:../../packages/com.yohawing.mmd-unity"
  }
}
```

相対パスは利用する Unity project の配置に合わせて調整してください。

## 基本導線

1. `.pmx` を Unity Project に import する。
2. import された PMX asset を Scene または Hierarchy にドラッグする。
3. `.vmd` を import する。
4. scene の playback object を Timeline に bind し、VMD Timeline clip を作る。
5. Play Mode では forward playback と Live physics を確認する。
6. Edit Mode の Timeline scrub では physics off の animation preview として扱う。

詳しい手順は [HOW_TO_USE.md](./HOW_TO_USE.md) を参照してください。

## ライセンス境界

この repository は、第三者の PMX / VMD / texture / motion / audio / capture を再配布しません。

MMD asset を使う場合は、利用者自身がライセンスと再配布可否を確認してください。local 検証用素材、生成ログ、スクリーンショット、test artifact は package commit に含めない前提です。

## 開発メモ

公開 `main` は package-first の構成に寄せています。

- 配布対象は `packages/com.yohawing.mmd-unity/` が中心です。
- `native/` は native source / rebuild reference のために残します。
- local scripts、Unity consumer project、検証 artifact、AI 作業メモは公開 package surface ではありません。
