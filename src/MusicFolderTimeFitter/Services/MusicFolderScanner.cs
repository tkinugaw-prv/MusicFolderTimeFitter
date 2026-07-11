using System.IO;
using MusicFolderTimeFitter.Models;

namespace MusicFolderTimeFitter.Services
{
    /// <summary>
    /// ルートフォルダー配下を再帰的にスキャンし、フォルダー単位（直下ファイルのみ）で
    /// 合計再生時間とタグ代表値を集計するクラス。
    /// </summary>
    public sealed class MusicFolderScanner : IMusicFolderScanner
    {
        /// <summary>タグ読み取りに使用するリーダー。</summary>
        private readonly ITagReader _tagReader;

        /// <summary>
        /// コンストラクター。
        /// </summary>
        /// <param name="tagReader">タグ読み取りに使用するリーダー。</param>
        public MusicFolderScanner(ITagReader tagReader)
        {
            _tagReader = tagReader;
        }

        /// <inheritdoc />
        public Task<FolderScanOutcome> ScanAsync(
            string rootPath,
            IProgress<ScanProgress>? progress,
            CancellationToken cancellationToken)
        {
            return Task.Run(() => ScanCore(rootPath, progress, cancellationToken), cancellationToken);
        }

        /// <summary>
        /// スキャン本体。ルート含む全階層のフォルダーを列挙し、各フォルダー直下のファイルを集計する。
        /// </summary>
        /// <param name="rootPath">ルートフォルダーの絶対パス。</param>
        /// <param name="progress">進捗通知先。</param>
        /// <param name="cancellationToken">キャンセルトークン。</param>
        /// <returns>スキャン結果。</returns>
        private FolderScanOutcome ScanCore(
            string rootPath,
            IProgress<ScanProgress>? progress,
            CancellationToken cancellationToken)
        {
            var results = new List<FolderScanResult>();
            var exclusionLogs = new List<string>();
            int scannedCount = 0;
            int excludedCount = 0;

            // アクセス不可フォルダーで走査全体が中断しないよう、明示的なスタックで深さ優先探索する
            var pendingDirectories = new Stack<string>();
            pendingDirectories.Push(rootPath);

            while (pendingDirectories.Count > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();

                string currentDirectory = pendingDirectories.Pop();
                scannedCount++;

                // サブフォルダーを積む（アクセス不可ならスキップしてログに残す）
                try
                {
                    foreach (string subDirectory in Directory.EnumerateDirectories(currentDirectory))
                    {
                        pendingDirectories.Push(subDirectory);
                    }
                }
                catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
                {
                    exclusionLogs.Add($"アクセス不可（サブフォルダー列挙失敗）: {currentDirectory} - {ex.Message}");
                }

                // 現在のフォルダー直下を集計する
                FolderScanResult? result = AggregateDirectory(
                    rootPath,
                    currentDirectory,
                    exclusionLogs,
                    ref excludedCount);

                if (result != null)
                {
                    results.Add(result);
                }

                progress?.Report(new ScanProgress(scannedCount, excludedCount));
            }

            return new FolderScanOutcome(results, scannedCount, excludedCount, exclusionLogs);
        }

        /// <summary>
        /// 1フォルダー分の集計を行う。
        /// 対象ファイルが0件、またはタグ読取失敗・アクセス不可の場合は null を返す。
        /// </summary>
        /// <param name="rootPath">ルートフォルダーの絶対パス。</param>
        /// <param name="directory">集計対象フォルダーの絶対パス。</param>
        /// <param name="exclusionLogs">除外理由のログ（追記される）。</param>
        /// <param name="excludedCount">除外フォルダー数（インクリメントされる）。</param>
        /// <returns>集計結果。集計対象外の場合は null。</returns>
        private FolderScanResult? AggregateDirectory(
            string rootPath,
            string directory,
            List<string> exclusionLogs,
            ref int excludedCount)
        {
            List<string> musicFiles;

            try
            {
                musicFiles = Directory.EnumerateFiles(directory)
                    .Where(f => Const.TARGET_EXTENSIONS.Contains(Path.GetExtension(f)))
                    .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
            {
                excludedCount++;
                exclusionLogs.Add($"アクセス不可（ファイル列挙失敗）: {directory} - {ex.Message}");
                return null;
            }

            // 対象ファイルが1つもないフォルダーは一覧対象外（除外件数には含めない）
            if (musicFiles.Count == 0)
            {
                return null;
            }

            var fileInfos = new List<MusicFileInfo>(musicFiles.Count);

            foreach (string musicFile in musicFiles)
            {
                try
                {
                    fileInfos.Add(_tagReader.Read(musicFile));
                }
                catch (Exception ex)
                {
                    // タグが読めない／壊れたファイルを含むフォルダーはフォルダーごと除外する
                    excludedCount++;
                    exclusionLogs.Add($"タグ読取失敗: {musicFile} - {ex.Message}");
                    return null;
                }
            }

            return BuildResult(rootPath, directory, fileInfos);
        }

        /// <summary>
        /// ファイル情報の集合からフォルダー集計結果を構築する。
        /// </summary>
        /// <param name="rootPath">ルートフォルダーの絶対パス。</param>
        /// <param name="directory">集計対象フォルダーの絶対パス。</param>
        /// <param name="fileInfos">フォルダー直下ファイルのタグ情報。</param>
        /// <returns>フォルダー集計結果。</returns>
        private static FolderScanResult BuildResult(
            string rootPath,
            string directory,
            IReadOnlyList<MusicFileInfo> fileInfos)
        {
            TimeSpan totalDuration = fileInfos
                .Aggregate(TimeSpan.Zero, (sum, f) => sum + f.Duration);

            string relativePath = Path.GetRelativePath(rootPath, directory);

            // ルートフォルダー自身の場合は "." になるためフォルダー名で表示する
            if (relativePath == ".")
            {
                relativePath = Path.GetFileName(Path.TrimEndingDirectorySeparator(rootPath));
            }

            uint? year = RepresentativeValueSelector.SelectYear(fileInfos.Select(f => f.Year));

            return new FolderScanResult
            {
                AbsolutePath = directory,
                RelativePath = relativePath,
                TotalDuration = totalDuration,
                Composer = ToDisplay(RepresentativeValueSelector.SelectString(fileInfos.Select(f => f.Composer))),
                Artist = ToDisplay(RepresentativeValueSelector.SelectString(fileInfos.Select(f => f.Artist))),
                Album = ToDisplay(RepresentativeValueSelector.SelectString(fileInfos.Select(f => f.Album))),
                AlbumArtist = ToDisplay(RepresentativeValueSelector.SelectString(fileInfos.Select(f => f.AlbumArtist))),
                Year = year.HasValue ? year.Value.ToString() : Const.UNKNOWN_VALUE_DISPLAY,
            };
        }

        /// <summary>
        /// 代表値を表示用文字列に変換する（null は "(不明)"）。
        /// </summary>
        /// <param name="value">代表値。</param>
        /// <returns>表示用文字列。</returns>
        private static string ToDisplay(string? value)
        {
            return value ?? Const.UNKNOWN_VALUE_DISPLAY;
        }
    }
}
