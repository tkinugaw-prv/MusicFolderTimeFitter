using MusicFolderTimeFitter.Models;

namespace MusicFolderTimeFitter.Services
{
    /// <summary>
    /// スキャン全体の結果を表すレコード。
    /// </summary>
    /// <param name="Folders">集計に成功したフォルダーの一覧（時間フィルター適用前）。</param>
    /// <param name="ScannedCount">スキャンしたフォルダー総数。</param>
    /// <param name="ExcludedCount">除外されたフォルダー数（タグ読取失敗・アクセス不可）。</param>
    /// <param name="ExclusionLogs">除外理由のログ。</param>
    public sealed record FolderScanOutcome(
        IReadOnlyList<FolderScanResult> Folders,
        int ScannedCount,
        int ExcludedCount,
        IReadOnlyList<string> ExclusionLogs);

    /// <summary>
    /// ルートフォルダー配下を再帰的にスキャンし、フォルダー単位で再生時間を集計するインターフェイス。
    /// </summary>
    public interface IMusicFolderScanner
    {
        /// <summary>
        /// スキャンを非同期に実行する。
        /// </summary>
        /// <param name="rootPath">ルートフォルダーの絶対パス。</param>
        /// <param name="progress">進捗通知先（null 可）。</param>
        /// <param name="cancellationToken">キャンセルトークン。</param>
        /// <returns>スキャン結果。</returns>
        Task<FolderScanOutcome> ScanAsync(
            string rootPath,
            IProgress<ScanProgress>? progress,
            CancellationToken cancellationToken);
    }
}
