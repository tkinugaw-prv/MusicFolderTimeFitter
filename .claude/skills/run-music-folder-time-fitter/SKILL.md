---
name: run-music-folder-time-fitter
description: 音楽フォルダー時間フィッター(MusicFolderTimeFitter)のビルド・起動・UI 操作・スクリーンショット取得。アプリを実行(run/start)する、スキャンを実際に動かして確認する、画面のスクリーンショットを撮る、テストを実行するときに使う。
---

WPF デスクトップアプリ(.NET 10 / Windows)。エージェントは
`.claude/skills/run-music-folder-time-fitter/driver.ps1`(UI Automation ベース)で起動から
スキャン実行・結果読み取り・スクリーンショットまで操作する。パスはすべてリポジトリルート基準。

## Prerequisites

- Windows + .NET 10 SDK(`dotnet --version` で確認)
- 追加インストール不要。テスト音源は driver が偽 FLAC を生成するので実音源・ffmpeg 不要。

## Build

```powershell
dotnet build
```

出力: `src\MusicFolderTimeFitter\bin\Debug\net10.0-windows\MusicFolderTimeFitter.exe`

## Run (agent path)

一発で全部確認するなら(テストデータ生成 → 起動 → 60 分でスキャン → 結果表示 → スクリーンショット → 終了):

```powershell
pwsh -NoProfile -File .claude\skills\run-music-folder-time-fitter\driver.ps1 smoke
```

期待される出力: `スキャン: 7 件 | 該当: 3 件 | 除外: 1 件` と結果 3 行
(Album_Medium_40min / BoxSet\Disc1_25min / Album_Short_15min)。

ステップ実行(起動したまま操作を重ねる場合):

```powershell
$d = ".claude\skills\run-music-folder-time-fitter\driver.ps1"
pwsh -NoProfile -File $d testdata                 # %TEMP%\mftf-testlib に偽 FLAC ライブラリ生成
pwsh -NoProfile -File $d start                    # settings.json をシードして起動(ダイアログ回避)
pwsh -NoProfile -File $d set-minutes -Value 60    # 所要時間モード + 60 分
pwsh -NoProfile -File $d scan                     # スキャン開始 → 完了待ち
pwsh -NoProfile -File $d results                  # ステータス + 結果一覧をテキスト出力
pwsh -NoProfile -File $d set-target -Value 23:59  # 目標時刻モードに切替
pwsh -NoProfile -File $d scan
pwsh -NoProfile -File $d screenshot               # → %TEMP%\mftf-screenshot.png
pwsh -NoProfile -File $d stop                     # 終了 + settings.json 復元
```

| アクション | 内容 |
|---|---|
| `smoke` | 全部入り(生成→起動→スキャン→SS→終了)。終了時に settings.json を必ず復元 |
| `testdata [-Path x]` | 偽 FLAC ライブラリ生成(既定: `%TEMP%\mftf-testlib`) |
| `start [-RootFolder x]` | `%APPDATA%\MusicFolderTimeFitter\settings.json` をバックアップ+シードして起動 |
| `dump` | メインウィンドウの UIA ツリーを表示(要素探索のデバッグ用) |
| `set-minutes -Value N` | 所要時間モードに切替えて分数を設定 |
| `set-target -Value HH:mm` | 目標時刻モードに切替えて時刻を設定(過去時刻はスキャン時にエラーダイアログ) |
| `scan` | 「スキャン開始」を押して完了/失敗まで待機。エラーダイアログ検出時は内容を表示して throw |
| `results` | ステータスバーの件数と DataGrid 全行をテキスト出力 |
| `screenshot [-Path x]` | ウィンドウをキャプチャ(既定: `%TEMP%\mftf-screenshot.png`)。撮ったら必ず Read で目視確認 |
| `stop` | ウィンドウを閉じ、settings.json バックアップを復元 |

実行するとユーザーのデスクトップに実ウィンドウが表示される(ヘッドレス不可)。作業後は必ず `stop` すること。

## Run (human path)

```powershell
dotnet run --project src/MusicFolderTimeFitter   # ウィンドウが開く。閉じるボタンで終了
```

## Test

```powershell
dotnet test
```

38 件全パス(約 1 秒)。カバレッジ付き実行は readme.md 参照。

## Gotchas

- **「参照...」ボタンは押さない** — `OpenFolderDialog`(ネイティブダイアログ)が開き UIA での操作が面倒。
  ルートフォルダーは `MainViewModel` が起動時に settings.json の `LastRootFolderPath` から復元するため、
  driver の `start` が設定ファイルをシードして注入する(既存設定は `.mftf-driver-bak` にバックアップし `stop` で復元)。
- **テスト音源は手組みの偽 FLAC** — `fLaC` マーカー + STREAMINFO(総サンプル数→再生時間)+
  VORBIS_COMMENT(タグ)+ ダミー音声 4096 バイト。TagLibSharp は音声ストリーム長が 0 だと
  Duration を 0 と報告するため、ダミー音声領域が必須(実測でハマった)。
- **DataGrid セルの値は UIA の `Name` プロパティで読む** — セル(`ClassName=DataGridCell`)は
  ValuePattern を公開するが `Value` は空文字を返す。`Current.Name` に表示文字列が入っている。
  最終列(再生ボタン列)の Name は `項目: MusicFolderTimeFitter.Models.FolderScanResult、...` なので除外する。
- **TextBox の特定はインデックス** — Edit 要素に AutomationId がないため視覚順で
  `[0]=ルートフォルダー(読取専用) [1]=所要時間(分) [2]=目標時刻` と決め打ちしている。
  入力欄を追加・並べ替えたら driver の `Set-EditValue` 呼び出しを直すこと。
- **最小化するとタスクトレイに格納されウィンドウが消える**(`TrayIconController`)。
  UIA から見えなくなるので driver 操作中は最小化しない。閉じる(×)は普通に終了する。
- **入力エラー時はモーダル MessageBox** が出てスキャンは始まらない(例: 過去の目標時刻)。
  `scan` はダイアログを検出してメッセージ付きで throw する。ダイアログが残ると以降の操作が詰まるので、
  出たままなら `stop`(プロセス kill にフォールバック)で回収する。
- **再生ボタン(▶)は AIMP を実際に起動する** — 設定パス(既定 `D:\AIMP\AIMP.exe`)が存在すると
  外部プロセスが立ち上がるので、動作確認では押さない。

## Troubleshooting

- **`実行ファイルがありません: ...MusicFolderTimeFitter.exe`**: 未ビルド。`dotnet build` を先に実行。
- **`既に起動しています`**: 前回の `stop` 漏れ。`driver.ps1 stop` してから `start`。
- **スクリーンショットが真っ黒/別画面**: ウィンドウが背面か最小化。driver は `SetFocus` +
  `SetProcessDPIAware` 済みだが、撮影の瞬間に別ウィンドウを前面に出さないこと。
- **`results` の行数が 0**: スキャン未実行(起動直後は空)。`scan` を先に実行する。
