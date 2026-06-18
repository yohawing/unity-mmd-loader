# How to use Yohawing MMD Unity

この手順は、UPM package として `com.yohawing.mmd-unity` を Unity project に追加した利用者向けです。

## 1. Package を追加する

Unity の **Window > Package Manager** を開き、**Add package from git URL** に以下を入力します。

```text
https://github.com/yohawing/unity-mmd-loader.git?path=packages/com.yohawing.mmd-unity
```

Unity version は 6000.4 以降を想定しています。

## 2. PMX を import する

`.pmx` ファイルを Unity Project の `Assets/` 配下へ追加します。

import 後、Project window 上では PMX asset として扱われます。package は PMX の元 bytes と import summary を保持しますが、通常 import だけでは mesh、material、texture、prefab などの派生 asset を勝手に永続化しません。

## 3. Scene に配置する

Project window から PMX asset を Scene または Hierarchy にドラッグします。

これにより、scene playback object が作られます。PMX-only の配置でも playback controller が保持されるため、あとから Timeline に VMD を追加できます。

## 4. VMD を import する

`.vmd` ファイルを Unity Project の `Assets/` 配下へ追加します。

VMD asset は Timeline clip や runtime playback source から参照されます。VMD の元 bytes を複製した派生 asset を通常導線で作る設計ではありません。

## 5. Timeline に VMD clip を作る

scene の MMD playback object を Timeline に bind し、VMD Timeline clip を作ります。

利用できる editor action は package version により変わる可能性がありますが、基本方針は次の通りです。

- PMX asset は scene の playback controller を作る。
- VMD asset は Timeline clip から参照する。
- Timeline clip は VMD を AnimationClip に即 bake せず、MMD runtime evaluation に time を渡す。

## 6. 再生と scrub の違い

Play Mode の forward playback では Live physics を使えます。

Edit Mode の Timeline scrub は random access preview として扱い、physics は off です。これは physics simulation が random access と相性が悪いためです。physics 結果を固定して scrub する workflow は将来の Physics Cache 側で扱います。

## 7. Raw path playback

開発や local diagnostics では `MmdRuntimeImporterComponent` による raw PMX/VMD path playback も使えます。

ただし通常の制作導線では、imported PMX/VMD asset と Timeline binding を優先してください。raw path playback は package 利用者向けの主導線ではなく、検証や移行のための補助経路です。

## 8. Known limitations

- Windows x86_64 native binary のみ packaged です。
- macOS / Linux native binary は未配布です。
- PMD、VPD、PMM、accessory、MME effect project は未対応です。
- full Humanoid bridge、AnimationClip writer output、rayMMD compatibility、experimental physics backends、Compute Skinning は future work です。
- third-party MMD assets は同梱しません。利用者側でライセンスを確認してください。
