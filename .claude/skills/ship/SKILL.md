---
name: ship
description: リリース前の定型ワークフロー。全テスト実行 → テスト結果・カバレッジレポート更新(reports/) → リリースビルド → コミット → プッシュを順に行う。「リリースして」「出荷」「テストしてコミット・プッシュ」などで使う。
---

リリース前チェックとコミット・プッシュを一気通貫で行う手順書。
**順序厳守**: テストかビルドが失敗したら、その時点で中断して失敗内容を報告し、コミット・プッシュには進まない。
パスはすべてリポジトリルート基準。

## 1. テスト実行 + レポート生成

readme.md「テストとカバレッジレポート」の再現手順に準拠する。

```powershell
dotnet test --logger "trx;LogFileName=test-results.trx" --collect:"XPlat Code Coverage" --results-directory "reports/raw"
```

- **テストが 1 件でも失敗したら即中断**。失敗したテスト名と出力を報告して終了。
- 全パスしたら成果物を配置する:

```powershell
# reportgenerator 未インストール時のみ（初回）
if (-not (Get-Command reportgenerator -ErrorAction SilentlyContinue)) { dotnet tool install --global dotnet-reportgenerator-globaltool }

Copy-Item "reports/raw/test-results.trx" "reports/test-results.trx"
$cov = Get-ChildItem "reports/raw" -Recurse -Filter "coverage.cobertura.xml" | Select-Object -First 1
New-Item -ItemType Directory -Force "reports/coverage" | Out-Null
Copy-Item $cov.FullName "reports/coverage/Cobertura.xml"
reportgenerator "-reports:reports/coverage/Cobertura.xml" "-targetdir:reports/coverage/html" "-reporttypes:Html;TextSummary"

# 中間ディレクトリは git 管理外に保つため削除
Remove-Item -Recurse -Force "reports/raw"
```

- `reports/coverage/html/Summary.txt` を読んでカバレッジサマリーを把握しておく（コミットメッセージや報告に使う）。

## 2. リリースビルド

```powershell
dotnet build -c Release
```

- 失敗したら中断（コミットしない）。エラー内容を報告して終了。
- 出力: `src\MusicFolderTimeFitter\bin\Release\net10.0-windows\MusicFolderTimeFitter.exe`

## 3. コミット

1. `git status` と `git diff` で変更内容を確認する（更新された reports/ も含む）。
2. **変更が reports/ の再生成のみ**（コード・ドキュメントに実質差分なし）の場合は、
   その旨をユーザーに報告して確認を仰ぐ。勝手にコミットしない。
3. 全変更をステージし、既存の規約に従ってコミットする:
   - 件名: 日本語で変更の要約（`git log --oneline` の既存スタイルに合わせる）
   - 本文: 箇条書きで変更点
   - 末尾: `Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>`

## 4. プッシュ

```powershell
git push origin <現在のブランチ>
```

- リモートは origin（github.com/tkinugaw-prv/MusicFolderTimeFitter）、通常は develop ブランチ。
- 完了したらテスト件数・カバレッジ・コミットハッシュ・プッシュ先を報告する。

## Gotchas

- **reports/coverage/html/ は毎回全ファイル再生成される** — 差分が大量に出るのは正常。
- **TRX・レポートにはタイムスタンプが含まれる** — コード変更ゼロでも reports/ に差分が出る。
  これだけの差分をコミットする意味があるかは手順 3-2 でユーザーに確認する。
- **`dotnet test` は Debug 構成で走る** — Release ビルド（手順 2）とは別物。両方必要。
- **reports/raw を残さない** — git 管理外の中間物。配置後に必ず削除する。
