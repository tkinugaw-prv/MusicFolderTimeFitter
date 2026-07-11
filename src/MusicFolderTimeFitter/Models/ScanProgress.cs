namespace MusicFolderTimeFitter.Models
{
    /// <summary>
    /// スキャン中の進捗情報を表すレコード。
    /// </summary>
    /// <param name="ScannedCount">スキャン済みフォルダー数。</param>
    /// <param name="ExcludedCount">除外されたフォルダー数（タグ読取失敗等）。</param>
    public sealed record ScanProgress(int ScannedCount, int ExcludedCount);
}
