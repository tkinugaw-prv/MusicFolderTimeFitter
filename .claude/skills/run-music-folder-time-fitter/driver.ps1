# =============================================================================
# MusicFolderTimeFitter UI Automation ドライバー
#
# WPF アプリを起動し、UI Automation で操作・観察するエージェント用ハーネス。
# 各アクションは独立したプロセス呼び出しで動作する(状態はアプリ側と
# settings.json バックアップファイルに持つ)。
#
# 使い方:
#   pwsh -File driver.ps1 smoke                  # 全部入り: テストデータ生成→起動→スキャン→SS→終了
#   pwsh -File driver.ps1 testdata               # テスト用 FLAC ライブラリを生成
#   pwsh -File driver.ps1 start [-RootFolder x]  # settings.json をシードして起動
#   pwsh -File driver.ps1 dump                   # UIA ツリーを表示
#   pwsh -File driver.ps1 set-minutes -Value 60  # 所要時間(分)を設定
#   pwsh -File driver.ps1 set-target -Value 18:30# 目標時刻モードに切替+時刻設定
#   pwsh -File driver.ps1 scan                   # スキャン開始を押し完了まで待つ
#   pwsh -File driver.ps1 results                # 結果一覧とステータスをテキストで出力
#   pwsh -File driver.ps1 screenshot [-Path x]   # ウィンドウのスクリーンショット
#   pwsh -File driver.ps1 stop                   # 終了 + settings.json を復元
# =============================================================================
param(
    [Parameter(Mandatory, Position = 0)]
    [ValidateSet("smoke", "testdata", "start", "dump", "set-minutes", "set-target", "scan", "results", "screenshot", "stop")]
    [string]$Action,

    [string]$RootFolder,
    [string]$Value,
    [string]$Path,
    [string]$Exe   # 起動する exe の明示指定(既定: Debug ビルド出力。publish 版の確認などに使う)
)

$ErrorActionPreference = "Stop"

# リポジトリルート = このスクリプトの 3 階層上(.claude/skills/run-*/ 配下にいる)
$RepoRoot = (Resolve-Path "$PSScriptRoot\..\..\..").Path
$ExePath = if ($Exe) { (Resolve-Path $Exe).Path } else { Join-Path $RepoRoot "src\MusicFolderTimeFitter\bin\Debug\net10.0-windows\MusicFolderTimeFitter.exe" }
$SettingsDir = Join-Path $env:APPDATA "MusicFolderTimeFitter"
$SettingsPath = Join-Path $SettingsDir "settings.json"
$SettingsBackup = Join-Path $SettingsDir "settings.json.mftf-driver-bak"
$DefaultTestLib = Join-Path $env:TEMP "mftf-testlib"
$ProcessName = "MusicFolderTimeFitter"
$WindowTitle = "音楽フォルダー時間フィッター"

Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes, System.Drawing, System.Windows.Forms

# =============================================================================
# テストデータ生成: 最小 FLAC(STREAMINFO + VORBIS_COMMENT + ダミー音声領域)
# TagLibSharp は STREAMINFO の総サンプル数から Duration を計算する。
# 音声ストリーム長が 0 だと Duration=0 になるためダミーバイトを付ける。
# =============================================================================
function New-MinimalFlac {
    param([string]$FlacPath, [int]$DurationSeconds, [hashtable]$Tags)

    $sampleRate = 44100
    [uint64]$totalSamples = [uint64]$sampleRate * [uint64]$DurationSeconds

    $ms = [System.IO.MemoryStream]::new()
    $bw = [System.IO.BinaryWriter]::new($ms)
    $bw.Write([byte[]][char[]]"fLaC")

    # STREAMINFO (type 0, 34 bytes)
    $bw.Write([byte]0x00)
    $bw.Write([byte[]](0x00, 0x00, 0x22))
    $si = [byte[]]::new(34)
    $si[0] = 0x10; $si[2] = 0x10  # min/max blocksize = 4096
    # 64bit ビッグエンディアン: sampleRate(20) | ch-1(3) | bps-1(5) | totalSamples(36)
    [uint64]$packed = ([uint64]$sampleRate -shl 44) -bor ([uint64]1 -shl 41) -bor ([uint64]15 -shl 36) -bor ($totalSamples -band 0xFFFFFFFFFuL)
    for ($i = 0; $i -lt 8; $i++) {
        $si[10 + $i] = [byte](($packed -shr ((7 - $i) * 8)) -band 0xFF)
    }
    $bw.Write($si)

    # VORBIS_COMMENT (type 4, last)
    $vc = [System.IO.MemoryStream]::new()
    $vw = [System.IO.BinaryWriter]::new($vc)
    $vendor = [System.Text.Encoding]::UTF8.GetBytes("mftf-driver")
    $vw.Write([uint32]$vendor.Length)
    $vw.Write($vendor)
    $vw.Write([uint32]$Tags.Count)
    foreach ($key in $Tags.Keys) {
        $entry = [System.Text.Encoding]::UTF8.GetBytes("$key=$($Tags[$key])")
        $vw.Write([uint32]$entry.Length)
        $vw.Write($entry)
    }
    $vcBytes = $vc.ToArray()
    $bw.Write([byte](0x80 -bor 0x04))
    $bw.Write([byte[]](
        [byte](($vcBytes.Length -shr 16) -band 0xFF),
        [byte](($vcBytes.Length -shr 8) -band 0xFF),
        [byte]($vcBytes.Length -band 0xFF)))
    $bw.Write($vcBytes)
    $bw.Write([byte[]]::new(4096))  # ダミー音声領域(Duration=0 回避)

    [System.IO.File]::WriteAllBytes($FlacPath, $ms.ToArray())
}

function New-TestLibrary {
    param([string]$LibRoot)

    if (Test-Path $LibRoot) { Remove-Item $LibRoot -Recurse -Force }

    # 合計 15 分 → 60 分指定で該当
    $d = New-Item -ItemType Directory -Force (Join-Path $LibRoot "Album_Short_15min")
    for ($i = 1; $i -le 3; $i++) {
        New-MinimalFlac -FlacPath (Join-Path $d "track$i.flac") -DurationSeconds 300 -Tags @{
            ARTIST = "アーティストA"; ALBUM = "ショートアルバム"; ALBUMARTIST = "アーティストA"; COMPOSER = "作曲者A"; DATE = "2020"; TITLE = "Track $i"
        }
    }

    # 合計 40 分 → 60 分指定で該当
    $d = New-Item -ItemType Directory -Force (Join-Path $LibRoot "Album_Medium_40min")
    for ($i = 1; $i -le 8; $i++) {
        New-MinimalFlac -FlacPath (Join-Path $d "track$i.flac") -DurationSeconds 300 -Tags @{
            ARTIST = "アーティストB"; ALBUM = "ミディアムアルバム"; ALBUMARTIST = "アーティストB"; COMPOSER = "作曲者B"; DATE = "2021"; TITLE = "Track $i"
        }
    }

    # 合計 100 分 → 60 分指定では除外される
    $d = New-Item -ItemType Directory -Force (Join-Path $LibRoot "Album_Long_100min")
    for ($i = 1; $i -le 20; $i++) {
        New-MinimalFlac -FlacPath (Join-Path $d "track$i.flac") -DurationSeconds 300 -Tags @{
            ARTIST = "アーティストC"; ALBUM = "ロングアルバム"; ALBUMARTIST = "アーティストC"; COMPOSER = "作曲者C"; DATE = "2022"; TITLE = "Track $i"
        }
    }

    # サブフォルダーは独立単位(親には直下ファイルなし → 対象外)
    $d = New-Item -ItemType Directory -Force (Join-Path $LibRoot "BoxSet\Disc1_25min")
    for ($i = 1; $i -le 5; $i++) {
        New-MinimalFlac -FlacPath (Join-Path $d "track$i.flac") -DurationSeconds 300 -Tags @{
            ARTIST = "アーティストD"; ALBUM = "ボックス Disc1"; ALBUMARTIST = "アーティストD"; COMPOSER = "作曲者D"; DATE = "2023"; TITLE = "Track $i"
        }
    }

    # 壊れたファイルを含むフォルダー → フォルダーごと除外される
    $d = New-Item -ItemType Directory -Force (Join-Path $LibRoot "Album_Broken")
    New-MinimalFlac -FlacPath (Join-Path $d "ok.flac") -DurationSeconds 300 -Tags @{ ARTIST = "X" }
    [System.IO.File]::WriteAllBytes((Join-Path $d "broken.flac"), [byte[]](1..64))

    Write-Output "テストライブラリを生成しました: $LibRoot"
}

# =============================================================================
# UIA ヘルパー
# =============================================================================
function Get-AppProcess {
    Get-Process -Name $ProcessName -ErrorAction SilentlyContinue | Select-Object -First 1
}

function Get-MainWindow {
    param([switch]$NoThrow)
    $proc = Get-AppProcess
    if (-not $proc) {
        if ($NoThrow) { return $null }
        throw "アプリが起動していません。先に 'driver.ps1 start' を実行してください。"
    }
    $root = [System.Windows.Automation.AutomationElement]::RootElement
    $cond = [System.Windows.Automation.PropertyCondition]::new(
        [System.Windows.Automation.AutomationElement]::ProcessIdProperty, $proc.Id)
    $windows = $root.FindAll([System.Windows.Automation.TreeScope]::Children, $cond)
    foreach ($w in $windows) {
        if ($w.Current.Name -eq $WindowTitle) { return $w }
    }
    if ($NoThrow) { return $null }
    throw "メインウィンドウが見つかりません(プロセスは存在: PID $($proc.Id))。"
}

function Find-ByType {
    param($Parent, $ControlType, [string]$Name)
    $conds = @([System.Windows.Automation.PropertyCondition]::new(
            [System.Windows.Automation.AutomationElement]::ControlTypeProperty, $ControlType))
    if ($Name) {
        $conds += [System.Windows.Automation.PropertyCondition]::new(
            [System.Windows.Automation.AutomationElement]::NameProperty, $Name)
    }
    $cond = if ($conds.Count -gt 1) { [System.Windows.Automation.AndCondition]::new($conds) } else { $conds[0] }
    return $Parent.FindAll([System.Windows.Automation.TreeScope]::Descendants, $cond)
}

function Invoke-ButtonByName {
    param($Window, [string]$Name)
    $buttons = Find-ByType $Window ([System.Windows.Automation.ControlType]::Button) $Name
    if ($buttons.Count -eq 0) { throw "ボタン '$Name' が見つかりません。" }
    $buttons[0].GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
}

function Set-EditValue {
    # メイン画面の Edit は視覚順に [0]=ルートフォルダー(読取専用) [1]=所要時間(分) [2]=目標時刻
    param($Window, [int]$Index, [string]$Text)
    $edits = Find-ByType $Window ([System.Windows.Automation.ControlType]::Edit)
    if ($edits.Count -le $Index) { throw "Edit[$Index] が見つかりません(検出数: $($edits.Count))。" }
    $edits[$Index].GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).SetValue($Text)
}

function Select-RadioByName {
    param($Window, [string]$Name)
    $radios = Find-ByType $Window ([System.Windows.Automation.ControlType]::RadioButton) $Name
    if ($radios.Count -eq 0) { throw "ラジオボタン '$Name' が見つかりません。" }
    $radios[0].GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
}

function Get-StatusTexts {
    # ステータスバー相当のテキスト("スキャン: n 件" 等)と状態テキストを収集する
    param($Window)
    $texts = Find-ByType $Window ([System.Windows.Automation.ControlType]::Text)
    $result = @{}
    foreach ($t in $texts) {
        $n = $t.Current.Name
        if ($n -match "^スキャン:") { $result.Scanned = $n }
        elseif ($n -match "^該当:") { $result.Matched = $n }
        elseif ($n -match "^除外:") { $result.Excluded = $n }
        elseif ($n -in @("待機中", "スキャン中...", "スキャン完了", "スキャン失敗") -or $n -match "^AIMP で再生") { $result.Status = $n }
    }
    return $result
}

# =============================================================================
# アクション
# =============================================================================
function Start-App {
    param([string]$Root)

    if (-not (Test-Path $ExePath)) {
        throw "実行ファイルがありません: $ExePath`n先に 'dotnet build' を実行してください。"
    }
    if (Get-AppProcess) { throw "既に起動しています。先に 'driver.ps1 stop' を実行してください。" }

    # settings.json をバックアップして LastRootFolderPath をシード
    # (「参照...」の OpenFolderDialog は UIA で操作しづらいため設定経由で注入する)
    New-Item -ItemType Directory -Force $SettingsDir | Out-Null
    if ((Test-Path $SettingsPath) -and -not (Test-Path $SettingsBackup)) {
        Copy-Item $SettingsPath $SettingsBackup
    }
    $settings = if (Test-Path $SettingsPath) {
        Get-Content $SettingsPath -Raw | ConvertFrom-Json -AsHashtable
    } else { @{} }
    $settings["LastRootFolderPath"] = $Root
    $settings | ConvertTo-Json | Set-Content $SettingsPath -Encoding utf8

    Start-Process $ExePath | Out-Null

    # メインウィンドウ出現を待つ(最大 15 秒)
    $deadline = (Get-Date).AddSeconds(15)
    while ((Get-Date) -lt $deadline) {
        $w = Get-MainWindow -NoThrow
        if ($w) { Write-Output "起動しました: '$WindowTitle' (ルート: $Root)"; return }
        Start-Sleep -Milliseconds 300
    }
    throw "15 秒以内にメインウィンドウが出現しませんでした。"
}

function Invoke-Scan {
    $w = Get-MainWindow
    Invoke-ButtonByName $w "スキャン開始"

    # 完了を待つ(最大 60 秒)。入力エラー時はモーダルの MessageBox が出るので検出する
    $deadline = (Get-Date).AddSeconds(60)
    while ((Get-Date) -lt $deadline) {
        Start-Sleep -Milliseconds 300
        $dialogs = Find-ByType $w ([System.Windows.Automation.ControlType]::Window)
        if ($dialogs.Count -gt 0) {
            $texts = Find-ByType $dialogs[0] ([System.Windows.Automation.ControlType]::Text)
            $msg = ($texts | ForEach-Object { $_.Current.Name }) -join " / "
            throw "ダイアログが表示されました: [$($dialogs[0].Current.Name)] $msg"
        }
        $st = Get-StatusTexts $w
        if ($st.Status -in @("スキャン完了", "スキャン失敗")) {
            Write-Output "ステータス: $($st.Status) | $($st.Scanned) | $($st.Matched) | $($st.Excluded)"
            return
        }
    }
    throw "60 秒以内にスキャンが完了しませんでした。"
}

function Show-Results {
    $w = Get-MainWindow
    $st = Get-StatusTexts $w
    Write-Output "ステータス: $($st.Status) | $($st.Scanned) | $($st.Matched) | $($st.Excluded)"

    # セル値は ValuePattern ではなく Name プロパティに入る(WPF DataGridCell の UIA 実装)。
    # 最終列(再生ボタン列)の Name は "項目: ..." になるため除外する。
    $rows = Find-ByType $w ([System.Windows.Automation.ControlType]::DataItem)
    Write-Output "結果一覧 ($($rows.Count) 行):"
    foreach ($row in $rows) {
        $cells = Find-ByType $row ([System.Windows.Automation.ControlType]::Custom)
        $values = $cells | ForEach-Object { $_.Current.Name } | Where-Object { $_ -and $_ -notmatch "^項目:" }
        Write-Output ("  " + ($values -join " | "))
    }
}

function Save-Screenshot {
    param([string]$OutPath)
    if (-not $OutPath) { $OutPath = Join-Path $env:TEMP "mftf-screenshot.png" }

    $w = Get-MainWindow
    # 前面化してから物理ピクセル座標でキャプチャ(DPI 仮想化を避けるため DPI Aware 化)
    try { $w.SetFocus() } catch {}
    Add-Type -Namespace Mftf -Name Native -MemberDefinition '[DllImport("user32.dll")] public static extern bool SetProcessDPIAware();'
    [Mftf.Native]::SetProcessDPIAware() | Out-Null
    Start-Sleep -Milliseconds 500

    $rect = $w.Current.BoundingRectangle
    $bmp = [System.Drawing.Bitmap]::new([int]$rect.Width, [int]$rect.Height)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.CopyFromScreen([int]$rect.X, [int]$rect.Y, 0, 0, $bmp.Size)
    $g.Dispose()
    $bmp.Save($OutPath, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    Write-Output "スクリーンショット: $OutPath"
}

function Show-UiaTree {
    $w = Get-MainWindow
    $walker = [System.Windows.Automation.TreeWalker]::ControlViewWalker

    function Walk($el, $depth) {
        $c = $el.Current
        $name = $c.Name
        if ($name.Length -gt 60) { $name = $name.Substring(0, 60) + "…" }
        $val = ""
        try {
            $vp = $el.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern)
            $val = " value='$($vp.Value)'"
        } catch {}
        Write-Output ("{0}{1} '{2}'{3}" -f ("  " * $depth), $c.ControlType.ProgrammaticName.Replace("ControlType.", ""), $name, $val)
        $child = $walker.GetFirstChild($el)
        while ($child) {
            Walk $child ($depth + 1)
            $child = $walker.GetNextSibling($child)
        }
    }
    Walk $w 0
}

function Stop-App {
    $proc = Get-AppProcess
    if ($proc) {
        $w = Get-MainWindow -NoThrow
        if ($w) {
            try { $w.GetCurrentPattern([System.Windows.Automation.WindowPattern]::Pattern).Close() } catch {}
        }
        if (-not $proc.WaitForExit(5000)) { $proc.Kill() }
        Write-Output "アプリを終了しました。"
    } else {
        Write-Output "アプリは起動していません。"
    }

    # settings.json を復元
    if (Test-Path $SettingsBackup) {
        Move-Item $SettingsBackup $SettingsPath -Force
        Write-Output "settings.json を復元しました。"
    }
}

# =============================================================================
# ディスパッチ
# =============================================================================
switch ($Action) {
    "testdata" {
        New-TestLibrary -LibRoot ($Path ? $Path : $DefaultTestLib)
    }
    "start" {
        Start-App -Root ($RootFolder ? $RootFolder : $DefaultTestLib)
    }
    "dump" { Show-UiaTree }
    "set-minutes" {
        if (-not $Value) { throw "-Value <分> を指定してください。" }
        $w = Get-MainWindow
        Select-RadioByName $w "所要時間"
        Set-EditValue $w 1 $Value
        Write-Output "所要時間モード: $Value 分"
    }
    "set-target" {
        if (-not $Value) { throw "-Value <HH:mm> を指定してください。" }
        $w = Get-MainWindow
        Select-RadioByName $w "目標時刻"
        Set-EditValue $w 2 $Value
        Write-Output "目標時刻モード: $Value"
    }
    "scan" { Invoke-Scan }
    "results" { Show-Results }
    "screenshot" { Save-Screenshot -OutPath $Path }
    "stop" { Stop-App }
    "smoke" {
        New-TestLibrary -LibRoot $DefaultTestLib
        try {
            Start-App -Root $DefaultTestLib
            $w = Get-MainWindow
            Select-RadioByName $w "所要時間"
            Set-EditValue $w 1 "60"
            Invoke-Scan
            Show-Results
            Save-Screenshot -OutPath $Path
        } finally {
            Stop-App
        }
    }
}
