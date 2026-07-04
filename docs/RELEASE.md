# Release Flow

この文書は `com.yohawing.mmd-loader` を外部利用可能な UPM package として切るための運用手順を固定する。テスト選択の詳細は `docs/TESTING.md`、公開する機能境界は `docs/ARCHITECTURE.md`、現行の実行順序は root `TODO.md` を正とする。

## Release 判定

release は「PMX / VMD を Unity の普通の資産として扱えるか」を基準に判定する。最低限の golden path は次の一本。

1. PMX を Unity Project に import できる。
2. import された PMX を Scene / Hierarchy に配置できる。
3. VMD を import し、Timeline clip として scene の playback object に bind できる。
4. Edit Mode の Timeline scrub は animation-only / physics off として破綻しない。
5. Play Mode の forward playback は Live physics を含めて動く。
6. 必要な場合だけ、明示操作で Humanoid AnimationClip bake へ進める。

この導線に直接関係しない fidelity 項目は release blocker にしない。SDEF/QDEF exact deformation、sphere map parity、SelfShadow、MMD 本家 screenshot parity、Physics Cache、Humanoid Bridge の成熟は future / deferred として扱う。

## Release 種別

| 種別 | 用途 | 必須 gate |
| --- | --- | --- |
| `preview` | 内部検証、ユーザー手元確認、破壊的変更前の確認。 | `.\scripts\check-cli.ps1 -Tier unity` |
| `release` | Git tag / UPM git URL で参照してよい公開候補。 | `.\scripts\check-cli.ps1` |
| `hotfix` | 既存 release の局所修正。scope を regression fix に限定する。 | 変更面に応じた focused test + `.\scripts\check-cli.ps1` |

`release` では `check-cli` の full tier が Unity compile、EditMode、PlayMode を担当する。Unity や `unity-mmd/` が無くて SKIP された場合は公開 release として扱わず、環境を直して再実行する。

## Preflight

1. 作業 tree を確認する。

```powershell
rtk git status --short
rtk git submodule status --recursive
```

2. 既存の未コミット変更を分類する。
   - release 対象の変更だけを含める。
   - `artifacts/`、`data-local/`、local MMD asset、スクリーンショット、AI scratch は含めない。
   - 例外: `Samples~/BasicPlayback/Assets/` の再配布可能 sample PMX/VMD は release 対象として含めてよい。
   - ユーザー作業由来の差分が混ざっている場合は、勝手に戻さず release scope から外す。

3. package metadata を確認する。
   - `docs/README.ja.md` が README 内容の正本である。
   - `README.md` は `docs/README.ja.md` から英訳する。
   - `packages/com.yohawing.mmd-loader/Documentation~/README.md` は `README.md` から機械的にコピーする。個別編集しない。
   - `packages/com.yohawing.mmd-loader/Documentation~/MMD_SELF_SHADOW.md` は `docs/MMD_SELF_SHADOW.md` からコピーする。
   - `packages/com.yohawing.mmd-loader/package.json`
   - `packages/com.yohawing.mmd-loader/CHANGELOG.md`
   - `packages/com.yohawing.mmd-loader/Documentation~/README.md`
   - `packages/com.yohawing.mmd-loader/Samples~/BasicPlayback/README.md`
   - `docs/README.ja.md`

4. `package.json` の `version` を SemVer で更新する。Unity UPM が参照する正は `packages/com.yohawing.mmd-loader/package.json`。

## Branch / PR Policy

`main` への直接 push は禁止する。公開 release に入る変更は、必ず `develop` branch から `main` への PR として通す。

許可される経路:

```text
feature/local work -> develop -> PR(develop -> main) -> user manual merge -> tag
```

禁止する操作:

- `main` への direct push。
- agent / automation による PR merge。
- force push で release tag や `main` の履歴を書き換えること。
- release 用の未検証差分を `main` へ直接持ち込むこと。

PR 作成前の確認:

```powershell
rtk git branch --show-current
rtk git status --short
.\scripts\check-cli.ps1
rtk git push origin develop
```

PR は `develop` base ではなく、`main` base / `develop` head で作る。PR 本文には実行した gate、results / log path、native DLL 更新有無、README / release note 更新有無を書く。PR merge はユーザーが GitHub UI などで手動実行する。Codex や subagent は merge button を押さない。

## Native Binary 更新

Windows x86_64 package には次の DLL だけを含める。

```text
packages/com.yohawing.mmd-loader/Runtime/Plugins/x86_64/mmd_runtime_ffi.dll
packages/com.yohawing.mmd-loader/Runtime/Plugins/x86_64/mmd_bullet.dll
```

`native/mmd-anim` を更新した release では、submodule と package DLL を同じ slice で扱う。

```powershell
rtk git -C native/mmd-anim status --short
rtk git -C native/mmd-anim describe --tags --always --dirty
.\scripts\build-mmd-runtime-ffi.ps1
```

注意: `scripts/build-mmd-runtime-ffi.ps1` は `git submodule update --init --recursive native/mmd-anim` を実行する。submodule を新しい release tag へ上げる作業では、先に `native/mmd-anim` 側を目的の tag / commit へ checkout し、親 repository の gitlink と packaged DLL を別々に確認する。

Physics DLL を更新した場合だけ Bullet backend を rebuild する。

```powershell
.\scripts\build-physics-native.ps1
```

native 更新後は package plugin folder に不要 DLL が増えていないことを確認する。

```powershell
Get-ChildItem .\packages\com.yohawing.mmd-loader\Runtime\Plugins\x86_64
```

## Verification

docs-only または metadata-only の確認:

```powershell
.\scripts\check-cli.ps1 -Tier fast
```

package source、Editor、importer、Timeline、runtime、native DLL を含む確認:

```powershell
.\scripts\check-cli.ps1 -Tier unity
```

release 候補の最終 gate:

```powershell
.\scripts\check-cli.ps1
```

Live physics を release 判定に含める場合は、PlayMode test が Bullet backend を `Assert.Ignore` していないことを results XML で確認する。Bullet DLL が無い場合は先に `.\scripts\build-physics-native.ps1` を実行する。

Unity-backed command は同じ `unity-mmd` project に対して並列実行しない。package resolution、Unity lock、Domain Reload 中の失敗は機能不具合と混ぜず、log と category を見て再実行可否を判断する。

## Editor-connected Smoke

CLI gate が通った後、runtime-facing の release では必要に応じて起動中 Unity Editor で golden path を確認する。これは自動合否の代替ではなく、UX / scene / Timeline / Console の実観測である。

確認対象:

- PMX import 後の Project 表示と Scene placement。
- VMD Timeline clip の作成、binding、Edit Mode scrub。
- Play Mode forward playback と Live physics。
- Console の Error 数。検証前に stale log を clear し、検証後の Error を今回の結果として読む。

ユーザーが Editor 上で作った未保存 scene / selection / PlayMode 状態は勝手に保存または破棄しない。

## Release Commit

release commit に含める代表ファイル:

- `packages/com.yohawing.mmd-loader/package.json`
- `packages/com.yohawing.mmd-loader/CHANGELOG.md`
- `README.md`
- `packages/com.yohawing.mmd-loader/Documentation~/README.md`
- `packages/com.yohawing.mmd-loader/Samples~/BasicPlayback/README.md`
- `docs/README.ja.md`
- native 更新がある場合は `native/mmd-anim` gitlink と package DLL
- release 手順または既知制限を変えた場合は `docs/RELEASE.md`

commit 前に差分を確認する。

```powershell
rtk git diff --stat
rtk git diff -- packages/com.yohawing.mmd-loader/package.json docs/RELEASE.md
```

release commit message は次の形式を基本にする。

```text
Prepare com.yohawing.mmd-loader vX.Y.Z
```

release commit は `develop` に置く。`main` へ直接 commit しない。

## Tagging

公開 release tag は package version と合わせる。tag は `develop -> main` PR がユーザーにより手動 merge された後、merge 済みの `main` に対して打つ。

```powershell
rtk git fetch origin
rtk git checkout main
rtk git pull --ff-only origin main
rtk git tag -a vX.Y.Z -m "com.yohawing.mmd-loader vX.Y.Z"
rtk git push origin vX.Y.Z
```

UPM install URL は次の形式を release note に載せる。

```text
https://github.com/yohawing/unity-mmd-loader.git?path=packages/com.yohawing.mmd-loader#vX.Y.Z
```

tag 後に別の修正が必要になった場合は既存 tag を動かさない。新しい patch version を切る。

## Release Notes

release note には少なくとも以下を書く。

- package version と tag。
- `packages/com.yohawing.mmd-loader/CHANGELOG.md` の該当 version section。
- 対象 Unity version: Unity 6000.4 以降。
- 対応 platform: Windows x86_64 native binary packaged。macOS / Linux は未配布。
- golden path の状態。
- 既知制限: Timeline scrub は physics off、Physics Cache 未実装、Humanoid Bridge は future、licensed MMD asset は未同梱。
- 実行した gate と log / results path。
- native DLL を更新した場合は `native/mmd-anim` の tag / commit と packaged DLL の確認結果。

## Rollback / Hotfix

release 後に regression が出た場合、まず再現を `unity-mmd` project または redistribution-safe fixture へ落とす。licensed local asset だけで起きる場合は asset を commit せず、`artifacts/` に sanitised summary を残す。

hotfix は regression fix のみを含める。version は patch increment とし、既存 tag の force update はしない。

## Release Blocker

次は release blocker として扱う。

- `.\scripts\check-cli.ps1` が FAIL する。
- Unity compile / EditMode / PlayMode が SKIP のまま公開 release を切ろうとしている。
- `main` への direct push、または agent / automation による PR merge を前提にしている。
- `develop -> main` PR 以外の経路で公開 release へ入れようとしている。
- package install URL から `packages/com.yohawing.mmd-loader` が解決できない。
- `package.json` version と tag が一致しない。
- packaged native DLL と submodule commit の対応が説明できない。
- golden path の PMX import、Scene placement、VMD Timeline、Play Mode playback のどれかが壊れている。
- licensed MMD asset、local path、private artifact を commit / release note に混ぜている。
