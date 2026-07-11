# 音楽フォルダー時間フィッター（Music Folder Time Fitter）

指定したルートフォルダー配下の音楽ファイル（FLAC / M4A）をフォルダー単位で再生時間集計し、
「所要時間」または「目標時刻」に収まるフォルダーだけを一覧表示する Windows デスクトップアプリです。
一覧の各行から AIMP（ポータブル版）へフォルダーを渡して再生できます。

仕様・デザインの詳細は [docs/](docs/) 配下を参照してください。

## 技術スタック

| 項目 | 内容 |
|---|---|
| フレームワーク | .NET 8 / WPF (`net8.0-windows`) |
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

### 使い方

1. 「参照...」でルートフォルダー（音楽ライブラリ）を選択する。
2. 時間指定モードを選ぶ:
   - **所要時間**: 今から聴ける時間を分単位で入力（例: 90）
   - **目標時刻**: 聴き終えたい時刻を `HH:mm` で入力（例: 18:30）。過去時刻はエラー。
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

テスト結果とカバレッジは第三者が検証できる形で [reports/](reports/) に配置しています。

| ファイル | 内容 |
|---|---|
| `reports/test-results.trx` | `dotnet test` の実行結果ログ（VSTest TRX 形式） |
| `reports/coverage/Cobertura.xml` | coverlet が収集したカバレッジ生データ（Cobertura 形式） |
| `reports/coverage/html/index.html` | ReportGenerator による HTML カバレッジレポート |
| `reports/coverage/html/Summary.txt` | カバレッジサマリー（テキスト） |

### 再現手順

```powershell
# 1. テスト実行 + TRX ログ + カバレッジ収集（要 .NET 8 SDK 以降）
dotnet test --logger "trx;LogFileName=test-results.trx" --collect:"XPlat Code Coverage" --results-directory "reports/raw"

# 2. ReportGenerator のインストール（初回のみ）
dotnet tool install --global dotnet-reportgenerator-globaltool

# 3. 成果物の配置と HTML レポート生成
Copy-Item "reports/raw/test-results.trx" "reports/test-results.trx"
$cov = Get-ChildItem "reports/raw" -Recurse -Filter "coverage.cobertura.xml" | Select-Object -First 1
New-Item -ItemType Directory -Force "reports/coverage" | Out-Null
Copy-Item $cov.FullName "reports/coverage/Cobertura.xml"
reportgenerator "-reports:reports/coverage/Cobertura.xml" "-targetdir:reports/coverage/html" "-reporttypes:Html;TextSummary"
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

## ライセンス上の留意点

タグ読み取りに使用している TagLibSharp は **LGPL v2.1** です。
バイナリ配布時は LGPL の条件（ライセンス文書の同梱、ライブラリ差し替え可能性の確保等）に留意してください。
本リポジトリのように NuGet 参照でアセンブリを分離したまま配布する形態であれば通常問題ありません。
