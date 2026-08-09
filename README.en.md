# Music Folder Time Fitter

[日本語](README.md) | English

A Windows desktop app that aggregates the playback duration of music files (FLAC / M4A) under a specified root folder on a per-folder basis, and lists only the folders that fit within a given "duration" or "target time".
Each row in the list can hand the folder off to AIMP (portable edition) for playback.

For detailed specifications and design documents, see [docs/](docs/) (Japanese).

## Tech Stack

| Item | Details |
|---|---|
| Framework | .NET 10 / WPF (`net10.0-windows`) |
| Tag reading | [TagLibSharp](https://github.com/mono/taglib-sharp) (LGPL v2.1) |
| MVVM | CommunityToolkit.Mvvm |
| Testing | xUnit + coverlet (coverage) + ReportGenerator |

## Build & Run

```powershell
# Build
dotnet build

# Run
dotnet run --project src/MusicFolderTimeFitter
```

### Creating a distributable exe (dotnet publish)

Single-file executables can be produced via the publish profiles in
[src/MusicFolderTimeFitter/Properties/PublishProfiles/](src/MusicFolderTimeFitter/Properties/PublishProfiles/). Two configurations are available.

| Profile | Type | Approx. size | Runtime requirement |
|---|---|---|---|
| `win-x64-self-contained` | Self-contained (runtime bundled) | ~70–80 MB | None (Windows x64) |
| `win-x64-framework-dependent` | Framework-dependent | A few MB | .NET 10 Desktop Runtime |

```powershell
# Self-contained (primary distribution)
dotnet publish src/MusicFolderTimeFitter -p:PublishProfile=win-x64-self-contained

# Framework-dependent (lightweight)
dotnet publish src/MusicFolderTimeFitter -p:PublishProfile=win-x64-framework-dependent
```

The output is `src/MusicFolderTimeFitter/bin/publish/<profile name>/MusicFolderTimeFitter.exe` for each profile.

### Release procedure (GitHub Release)

Pushing a tag that starts with `v` triggers the [release workflow](.github/workflows/release.yml), which
runs tests → publishes both configurations → creates a GitHub Release.
Besides the two exe files, the Release also carries `LICENSE.txt` and
[`THIRD-PARTY-NOTICES.md`](THIRD-PARTY-NOTICES.md).
The version is derived from the tag name (e.g. `v1.2.3` → `1.2.3`).

```powershell
git tag v1.0.0
git push origin v1.0.0
```

### Usage

1. Click "参照..." (Browse...) to select the root folder (your music library).
2. Choose a time specification mode:
   - **所要時間 (Duration)**: enter how many minutes you can listen from now (e.g. 90)
   - **目標時刻 (Target time)**: enter the time you want to finish listening by, in `HH:mm` (e.g. 18:30). Inputs without a colon such as `1030` or `730` are auto-completed. Times in the past are rejected.
3. Click "スキャン開始" (Start scan) to aggregate the total duration per folder (direct files only; subfolders are treated as independent units), and only folders that fit within the remaining time are listed.
4. Click the ▶ button on a row, or double-click the row, to hand the folder off to AIMP for playback.
5. The path to the AIMP executable can be changed from "設定" (Settings) at the right of the title bar
   (default: `D:\AIMP\AIMP.exe`; settings are persisted to `%APPDATA%\MusicFolderTimeFitter\settings.json`).

The root folder, time specification mode, duration, and target time are saved on exit and restored on the next launch
(in duration mode, the target time field is recalculated at launch as the current time + the duration).

### Aggregation & exclusion rules

- Folders containing files whose tags cannot be read or are corrupted are **excluded as a whole folder** (the number of exclusions is shown in the status bar).
- Folders with no target files, or with a total duration of 0, are not listed.
- The representative value for each displayed tag field (composer, artist, album, album artist, year) is
  the **mode (most frequent value) excluding empty values** (ties resolve to the smallest value; if all files lack the value, `(不明)` (unknown) is shown).

## Tests and Coverage Reports

Test results and coverage reports are generated on every run by GitHub Actions (the [test workflow](.github/workflows/test.yml))
and published as **Artifacts** (`test-results` / `coverage-report`) of each run.
Third parties can verify the test results (TRX), raw coverage data (Cobertura XML), and HTML reports from there.

### Reproducing locally

```powershell
# 1. Run tests + TRX log + coverage collection (requires .NET 10 SDK or later)
dotnet test --logger "trx;LogFileName=test-results.trx" --collect:"XPlat Code Coverage" --results-directory "reports/raw"

# 2. Install ReportGenerator (first time only)
dotnet tool install --global dotnet-reportgenerator-globaltool

# 3. Generate the HTML report (reports/ is not tracked by git)
$cov = Get-ChildItem "reports/raw" -Recurse -Filter "coverage.cobertura.xml" | Select-Object -First 1
reportgenerator "-reports:$($cov.FullName)" "-targetdir:reports/coverage/html" "-reporttypes:Html;TextSummary"
```

### Coverage policy

Unit tests target the **core logic** (scanning, aggregation, representative-value selection, remaining-time calculation, and settings persistence)
plus the ViewModel logic that restores and saves the input state.
The view layer (Views / App), ViewModel paths that show modal dialogs, and parts depending on external processes or real audio files (AimpLauncher / TagLibTagReader)
are out of scope for unit tests and are verified manually. Coverage of the main classes:

| Class | Line coverage |
|---|---|
| RepresentativeValueSelector | 100% |
| AppSettings / MusicFileInfo / ScanProgress | 100% |
| RemainingTimeCalculator | 95.5% |
| MusicFolderScanner | 90.5% |
| JsonSettingsService | 82.8% |
| FolderScanResult | 77.7% |
| MainViewModel | 37.0% |

These figures are a snapshot taken at measurement time. For current values, see the
`coverage-report` artifact produced by the test workflow.

### Dependency updates

Dependabot opens weekly pull requests for NuGet packages and GitHub Actions
([configuration](.github/dependabot.yml)). They target the default branch, `develop`.
Minor and patch updates are grouped; major updates come one at a time.

Actions in the workflows are pinned to commit SHAs. Dependabot rewrites both the SHA
and the trailing version comment, so the pinning convention stays intact.

## Environment Variables

This application does not use any environment variables.

| Variable | Purpose | Default |
|---|---|---|
| (none) | — | — |

## License

This repository is published under the [MIT License](LICENSE).

Copyright notices and full license texts for the third-party components included in the
distributed exe are collected in [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
GitHub Releases ship `LICENSE.txt` and `THIRD-PARTY-NOTICES.md` alongside the exe files.

### Note on dependencies

TagLibSharp, used for tag reading, is licensed under **LGPL v2.1**.
Since the single-exe distribution bundles the TagLibSharp assembly inside the exe,
the LGPL conditions are addressed as follows.

- **Bundling the license text**: `THIRD-PARTY-NOTICES.md`, which reproduces the full LGPL v2.1 text and the copyright notices, is attached as a Release asset
- **Keeping the library replaceable**: publishing without single-file packaging emits `TagLibSharp.dll` as a standalone file that can be swapped for a modified build (see [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md) for the exact command)

The LGPL does not constrain the license of the code that uses the library, so this
application's own source code remains MIT.
