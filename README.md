# 音楽フォルダー時間フィッター（Music Folder Time Fitter）

日本語 | [English](README.en.md)

指定したルートフォルダー配下の音楽ファイル（FLAC / M4A）をフォルダー単位で再生時間集計し、
「所要時間」または「目標時刻」に収まるフォルダーだけを一覧表示する Windows デスクトップアプリです。
一覧の各行から AIMP（ポータブル版）へフォルダーを渡して再生できます。

仕様・デザインの詳細は [docs/](docs/) 配下を参照してください。

## 技術スタック

| 項目 | 内容 |
|---|---|
| フレームワーク | .NET 10 / WPF (`net10.0-windows`) |
| タグ読み取り | [TagLibSharp](https://github.com/mono/taglib-sharp)（LGPL v2.1） |
| MVVM | CommunityToolkit.Mvvm |
| テスト | xUnit + coverlet（カバレッジ） + ReportGenerator |

## ビルド・実行

```powershell
# ビルド
dotnet build

# 実行
dotnet run --project src/MusicFolderTimeFitter
```

### 配布用 exe の作成（dotnet publish）

publish プロファイル（[src/MusicFolderTimeFitter/Properties/PublishProfiles/](src/MusicFolderTimeFitter/Properties/PublishProfiles/)）で
単一 exe を生成できます。2 種類の構成があります。

| プロファイル | 形態 | サイズ目安 | 実行要件 |
|---|---|---|---|
| `win-x64-self-contained` | 自己完結型（ランタイム同梱） | 約 70〜80MB | なし（Windows x64） |
| `win-x64-framework-dependent` | フレームワーク依存型 | 数 MB | .NET 10 デスクトップランタイム |

```powershell
# 自己完結型（配布のメイン）
dotnet publish src/MusicFolderTimeFitter -p:PublishProfile=win-x64-self-contained

# フレームワーク依存型（軽量版）
dotnet publish src/MusicFolderTimeFitter -p:PublishProfile=win-x64-framework-dependent
```

出力先はそれぞれ `src/MusicFolderTimeFitter/bin/publish/<プロファイル名>/MusicFolderTimeFitter.exe` です。

### リリース手順（GitHub Release）

`v` で始まるタグを push すると、[release ワークフロー](.github/workflows/release.yml)が
テスト → 両構成の publish → GitHub Release 作成を自動実行します。
Release には exe 2 種類に加えて `LICENSE.txt` と
[`THIRD-PARTY-NOTICES.md`](THIRD-PARTY-NOTICES.md) が添付されます。
バージョンはタグ名から設定されます（例: `v1.2.3` → `1.2.3`）。

```powershell
git tag v1.0.0
git push origin v1.0.0
```

### 使い方

1. 「参照...」でルートフォルダー（音楽ライブラリ）を選択する。
2. 時間指定モードを選ぶ:
   - **所要時間**: 今から聴ける時間を分単位で入力（例: 90）
   - **目標時刻**: 聴き終えたい時刻を `HH:mm` で入力（例: 18:30）。`1030` や `730` のようにコロンを省略した入力も自動補完される。過去時刻はエラー。
3. 「スキャン開始」でフォルダー単位（直下ファイルのみ、サブフォルダーは独立単位）の合計時間を集計し、
   残り時間以下のフォルダーだけを一覧表示する。
4. 行の ▶ ボタンまたはダブルクリックで AIMP に渡して再生する。
5. AIMP 実行ファイルのパスはタイトルバー右の「設定」から変更できる
   （デフォルト: `D:\AIMP\AIMP.exe`。設定は `%APPDATA%\MusicFolderTimeFitter\settings.json` に永続化）。

### 集計・除外ルール

- タグが読めない／壊れたファイルを含むフォルダーは**フォルダーごと除外**（ステータスバーに除外件数を表示）。
- 対象ファイルが1つもない、または合計時間 0 のフォルダーは一覧対象外。
- タグ表示項目（作曲者・アーティスト・アルバム・アルバムアーティスト・年）の代表値は、
  **値なしを除外した最頻値**（同数タイは最小値、全ファイル値なしは `(不明)`）。

## テストとカバレッジレポート

テスト結果とカバレッジレポートは GitHub Actions（[test ワークフロー](.github/workflows/test.yml)）が
実行ごとに生成し、各 Run の **Artifacts**（`test-results` / `coverage-report`）として公開しています。
第三者はそこからテスト結果（TRX）・カバレッジ生データ（Cobertura XML）・HTML レポートを検証できます。

### ローカルでの再現手順

```powershell
# 1. テスト実行 + TRX ログ + カバレッジ収集（要 .NET 10 SDK 以降）
dotnet test --logger "trx;LogFileName=test-results.trx" --collect:"XPlat Code Coverage" --results-directory "reports/raw"

# 2. ReportGenerator のインストール（初回のみ）
dotnet tool install --global dotnet-reportgenerator-globaltool

# 3. HTML レポート生成（reports/ は git 管理外）
$cov = Get-ChildItem "reports/raw" -Recurse -Filter "coverage.cobertura.xml" | Select-Object -First 1
reportgenerator "-reports:$($cov.FullName)" "-targetdir:reports/coverage/html" "-reporttypes:Html;TextSummary"
```

### カバレッジ方針

単体テストの対象は**コアロジック**（スキャン・集計・代表値選定・残り時間算出・設定永続化）です。
UI 層（Views / ViewModels / App）と外部プロセス・実音源依存部（AimpLauncher / TagLibTagReader）は
単体テスト対象外とし、手動の動作確認で検証します。主要クラスのカバレッジ:

| クラス | ラインカバレッジ |
|---|---|
| MusicFolderScanner | 90.5% |
| RepresentativeValueSelector | 100% |
| RemainingTimeCalculator | 100% |
| JsonSettingsService | 82.8% |
| Models（FolderScanResult 等） | 100% |

## 使用する環境変数

本アプリケーションで使用する環境変数はありません。

| 環境変数名 | 用途 | 既定値 |
|---|---|---|
| （なし） | — | — |

## ライセンス

本リポジトリは [MIT License](LICENSE) で公開しています。

配布する exe に含まれる第三者コンポーネントの著作権表示とライセンス全文は
[THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md) にまとめています。
GitHub Release では exe と一緒に `LICENSE.txt` と `THIRD-PARTY-NOTICES.md` を配布しています。

### 依存ライブラリに関する留意点

タグ読み取りに使用している TagLibSharp は **LGPL v2.1** です。
単一 exe 配布は TagLibSharp のアセンブリを exe 内へバンドルする形態のため、
LGPL の条件には次のとおり対応しています。

- **ライセンス文書の同梱**: LGPL v2.1 の全文と著作権表示を収録した `THIRD-PARTY-NOTICES.md` を Release の資産として添付
- **差し替え可能性の確保**: 単一ファイル化しない構成で publish すれば `TagLibSharp.dll` が独立したファイルとして出力され、改変版に置き換えられます（手順は [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md) を参照）

LGPL はライブラリを利用する側のライセンスを制約しないため、本体のソースコードは MIT のままです。
