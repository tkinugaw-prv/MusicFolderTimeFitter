# 音楽フォルダー時間フィッター 実装計画

## Context

`docs/` 配下のドキュメント3点セットに基づき、Windows デスクトップアプリを新規実装する。

- [music_folder_time_fitter_spec.md](docs/music_folder_time_fitter_spec.md) — 機能仕様（確定）
- [design_handoff README](docs/Music%20folder%20time%20filter/design_handoff_music_folder_time_fitter/README.md) — High-fidelity デザイン（1a 案採用、確定）
- [llm_guideline.md](docs/llm_guideline.md) — コーディング規約

**アプリ概要**: ルートフォルダー配下の音楽ファイル（.flac / .m4a）をフォルダー単位（直下ファイルのみ、累積合算なし）で再生時間集計し、「所要時間（分）」または「目標時刻」に収まるフォルダーだけを一覧表示。各行から AIMP（ポータブル版、パスは設定で指定）へフォルダーパスを引数渡しして再生。

**ユーザー確認済みの決定事項**:
- アクセントカラー: **緑 `#5ec2a5`**（アクセント塗りボタンの文字色はダーク `oklch(0.14 0.01 260)` ≈ `#1a1d24`）
- 単体テストプロジェクト: **作成する**（xUnit）
- テスト結果レポートを**カバレッジ込みで第三者検証可能な形**で成果物に含める（TRX + Cobertura XML + HTML レポート、再現コマンドを readme に記載）
- タグ取得項目の変更: **リリース日は廃止**。代わりに **作曲者・アーティスト・アルバム・アルバムアーティスト・年** の5項目を表示する（仕様書 §4 の代表値選定ルールはこれら各項目に適用）
- AIMP 実行ファイルパスのデフォルト値: **`D:\AIMP\AIMP.exe`**（設定未保存時の初期値。設定ダイアログで変更・永続化可能）

## 技術スタック

- .NET 8 / WPF（`net8.0-windows`）
- TagLibSharp（NuGet）— タグからの再生時間・リリース日取得
- CommunityToolkit.Mvvm（NuGet）— ObservableObject / RelayCommand
- xUnit + coverlet.collector — コアロジックのテストとカバレッジ収集
- ReportGenerator（`dotnet tool`）— Cobertura → HTML カバレッジレポート生成

## プロジェクト構成

```
MusicFolderTimeFitter.sln
readme.md                          … ビルド/実行手順、環境変数テーブル（本アプリは環境変数不使用の旨を記載）
src/MusicFolderTimeFitter/
  App.xaml / App.xaml.cs           … DI 的な組み立て（手動 new で十分）、テーマ読込
  Const.cs                         … 定数定義（設定ファイル名、対象拡張子など。FULL_CAPITAL）
  Models/
    TimeFilterMode.cs              … enum: Duration / TargetTime
    MusicFileInfo.cs               … 1ファイル分のメタ情報（duration, 作曲者, アーティスト, アルバム, アルバムアーティスト, 年）
    FolderScanResult.cs            … 相対パス・合計時間・代表値5項目（作曲者/アーティスト/アルバム/アルバムアーティスト/年）
    ScanProgress.cs                … スキャン済み/該当/除外件数、状態テキスト
    AppSettings.cs                 … AimpExecutablePath（デフォルト `D:\AIMP\AIMP.exe`、Const に定義）, LastRootFolderPath
  Services/
    ITagReader.cs / TagLibTagReader.cs
                                   … ファイル→(再生時間, タグ5項目) 抽出。TagLibSharp 依存をここに隔離
    IMusicFolderScanner.cs / MusicFolderScanner.cs
                                   … 再帰列挙＋フォルダー単位集計（非同期、IProgress<ScanProgress> で進捗通知）
    RepresentativeValueSelector.cs … 代表値選定（最頻値→タイは最小値→空なら null）純粋ロジック。タグ5項目それぞれに適用
    RemainingTimeCalculator.cs     … モード別の残り時間算出（現在時刻は TimeProvider 注入でテスト可能に）
    ISettingsService.cs / JsonSettingsService.cs
                                   … %APPDATA%\MusicFolderTimeFitter\settings.json に JSON 永続化
    IAimpLauncher.cs / AimpLauncher.cs
                                   … Process.Start(aimpPath, "\"<フォルダー絶対パス>\"")
  ViewModels/
    MainViewModel.cs               … 入力状態・スキャン実行・結果コレクション・ステータス
    SettingsViewModel.cs           … AIMP パス編集・保存/キャンセル
  Views/
    MainWindow.xaml(.cs)           … メイン画面（4ブロック: タイトルバー/入力/一覧/ステータスバー）
    SettingsDialog.xaml(.cs)       … モーダル設定ダイアログ
  Themes/
    DarkTheme.xaml                 … デザイントークン（色・角丸・フォント）を ResourceDictionary 化
tests/MusicFolderTimeFitter.Tests/
    RepresentativeValueSelectorTests.cs  … 最頻値/タイブレーク/「値なし」除外ロジック（一部値なし混在・全件値なし双方のケース）を検証
    RemainingTimeCalculatorTests.cs
    MusicFolderScannerTests.cs     … ITagReader をスタブ化し、実音源なしで集計/除外ルールを検証
reports/                           … テスト結果 + カバレッジレポート（第三者検証用成果物）
    test-results.trx               … dotnet test の TRX ログ
    coverage/Cobertura.xml         … coverlet 収集の生データ
    coverage/html/                 … ReportGenerator による HTML レポート
```

## 実装のポイント

### スキャン・集計（MusicFolderScanner）
- ルート含む全階層のディレクトリを列挙し、各ディレクトリ**直下**の `.flac` / `.m4a`（`Const` に `HashSet<string>` で定義、拡張容易に）を集計。
- 1ファイルでもタグ読取失敗 → **そのフォルダーごと除外**（除外件数カウント）。対象ファイル0件のフォルダーは一覧対象外（除外件数には含めない）。
- `UnauthorizedAccessException` 等 → 該当フォルダーをスキップし除外理由をステータスへ。
- `Task.Run` でバックグラウンド実行、`IProgress<ScanProgress>` で UI スレッドへ進捗通知（数百〜数千フォルダー想定）。

### タグ項目（TagLibTagReader + RepresentativeValueSelector）
- 取得項目（TagLibSharp の統合 `Tag` プロパティを使用。FLAC/M4A 双方に共通対応）:
  - 作曲者: `Tag.Composers`（複数値は `"; "` 結合）
  - アーティスト: `Tag.Performers`（同上）
  - アルバム: `Tag.Album`
  - アルバムアーティスト: `Tag.AlbumArtists`（同上）
  - 年: `Tag.Year`（0 は「値なし」扱い）
- 代表値（フォルダー単位、各項目独立に選定）: 最頻値 → 同数タイは最小値（文字列は序数比較、年は数値比較）→ 全ファイルで空なら `(不明)` 表示。
  - **「値なし」の扱い**: 各項目（作曲者/アーティスト/アルバム/アルバムアーティスト/年）について、値が空文字列（または年=0）のファイルは**最頻値集計の母数から除外**する。有効値を持つファイルのみで最頻値・タイブレークを行う。フォルダー内の**全ファイルが値なし**の場合にのみ、その項目を `(不明)` とする。
    - 例: 10曲中6曲が年タグなし・4曲が「1975」→ 代表値は「1975」（値なしは除外して集計するため）。

### 時間判定（RemainingTimeCalculator）
- 所要時間モード: 分入力 → `TimeSpan`。
- 目標時刻モード: `HH:mm` パース、`目標時刻 − 現在時刻`。過去時刻は入力エラー（翌日繰越なし）。`TimeProvider` 注入でテスト可能に。
- フィルター条件: `合計時間 ≤ 残り時間` のみ。

### UI（デザインハンドオフ 1a 準拠、アクセント緑 #5ec2a5）
- 単一ウインドウ縦4ブロック。フォント `Inter, Yu Gothic UI, Segoe UI`（Inter 未導入環境はフォールバック）。数値列は等幅数字相当の見た目を優先（右寄せ + 固定幅）。
- 一覧は `DataGrid`（読み取り専用・列ヘッダークリックでソート可＝裁量枠の実装）。初期ソートは合計時間の降順。列: フォルダー名（相対パス）/ 合計時間 `HH:mm:ss`（右寄せ 120px）/ 作曲者 / アーティスト / アルバム / アルバムアーティスト / 年（右寄せ 60px）/ ▶再生ボタン（44px）。行ダブルクリックでも再生。列数が増えるためフォルダー名列は伸縮（`*`）、タグ文字列列は `TextTrimming="CharacterEllipsis"` + ツールチップで全文表示。
- 該当0件時は「条件に一致するフォルダーがありません」の空状態表示。
- ステータスバー: 「スキャン: N 件 / 該当: N 件（緑数字）/ 除外: N 件（オレンジ `oklch(0.7 0.02 40)` 相当）」＋右側にインジケーター（スキャン中アニメーション）とステータステキスト。
- 設定ダイアログ: 420px カード、AIMP パス（読取専用 TextBox + 参照... = `OpenFileDialog` で exe 選択）、保存で JSON 永続化。初期値は `D:\AIMP\AIMP.exe`。パスの実体（exe）が存在しない場合は再生ボタン無効化＋ツールチップで案内。
- ルートフォルダー参照は `Microsoft.Win32.OpenFolderDialog`（.NET 8 標準）を使用。

### 規約対応（llm_guideline.md）
- Allman ブレース必須（1行 if 禁止）、クラス/メソッドにヘッダコメント（XML doc）必須。
- 定数 `FULL_CAPITAL`、フィールド `_lowerCamel`、インターフェイス `I` 接頭辞。
- 環境変数は不使用 → `readme.md` に「使用環境変数: なし」のテーブルを明記（規約要求）。
- EF Core / DB 規約は本アプリでは対象外。

## 実装手順

1. `dotnet new sln` + WPF プロジェクト + xUnit プロジェクト（coverlet.collector 込み）作成、NuGet 追加（TagLibSharp, CommunityToolkit.Mvvm）
2. Models / Const / コアサービス（Scanner, RepresentativeValueSelector, RemainingTimeCalculator, TagReader）
3. JsonSettingsService / AimpLauncher
4. ViewModels（MainViewModel, SettingsViewModel）
5. Themes/DarkTheme.xaml + MainWindow + SettingsDialog（デザイントークン反映）
6. 単体テスト（xUnit、スタブ TagReader で実音源不要に）
7. テストレポート生成: `dotnet test --logger "trx" --collect:"XPlat Code Coverage"` → ReportGenerator で HTML 化し `reports/` に配置。再現手順（コマンド）を readme.md に記載し第三者が再実行・検証できる状態にする
8. readme.md 作成（ビルド/実行手順、テスト・カバレッジ再現手順、環境変数テーブル）

## 検証

1. `dotnet build` / `dotnet test` が成功し、`reports/` に TRX・Cobertura XML・HTML カバレッジレポートが生成されていること（コアロジックのカバレッジ数値をレポートで確認可能）
2. スクラッチパッドにテスト用フォルダー構成を作成（ffmpeg があれば無音の短い .flac/.m4a を生成、なければ手持ちの音楽フォルダーで手動確認を依頼）し、アプリを起動して:
   - スキャン → 件数/除外数のステータス表示 → 条件内フォルダーのみ一覧表示
   - 目標時刻に過去時刻を入れてエラー表示
   - 壊れたファイル（拡張子だけ .flac のテキスト）を含むフォルダーが除外されること
   - 設定ダイアログで AIMP パスを保存 → 再起動後も保持されること
3. AIMP 実機再生はユーザー環境依存のため、`Process.Start` の引数形式（パス引用符付き）をログ確認の上、ユーザーに実再生を確認してもらう
