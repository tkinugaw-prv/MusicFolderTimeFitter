namespace MusicFolderTimeFitter.Models
{
    /// <summary>
    /// フォルダー1件分の集計結果を表すクラス。一覧表示の1行に対応する。
    /// </summary>
    public sealed class FolderScanResult
    {
        /// <summary>フォルダーの絶対パス（AIMP 再生用）。</summary>
        public required string AbsolutePath { get; init; }

        /// <summary>ルートフォルダーからの相対パス（一覧表示用）。</summary>
        public required string RelativePath { get; init; }

        /// <summary>直下ファイルの合計再生時間。</summary>
        public required TimeSpan TotalDuration { get; init; }

        /// <summary>代表値: 作曲者（値なしは "(不明)"）。</summary>
        public required string Composer { get; init; }

        /// <summary>代表値: アーティスト（値なしは "(不明)"）。</summary>
        public required string Artist { get; init; }

        /// <summary>代表値: アルバム（値なしは "(不明)"）。</summary>
        public required string Album { get; init; }

        /// <summary>代表値: アルバムアーティスト（値なしは "(不明)"）。</summary>
        public required string AlbumArtist { get; init; }

        /// <summary>代表値: 年（値なしは "(不明)"）。</summary>
        public required string Year { get; init; }

        /// <summary>スキャン時の残り時間に対する余裕時間（残り時間 − 合計時間）。スキャン後に設定される。</summary>
        public TimeSpan Slack { get; set; }

        /// <summary>合計再生時間の表示用文字列（HH:mm:ss、24時間超も総時間表記）。</summary>
        public string TotalDurationText
        {
            get
            {
                return FormatDuration(TotalDuration);
            }
        }

        /// <summary>余裕時間の表示用文字列（HH:mm:ss、24時間超も総時間表記）。</summary>
        public string SlackText
        {
            get
            {
                return FormatDuration(Slack);
            }
        }

        /// <summary>
        /// 時間量を HH:mm:ss 形式（24時間超も総時間表記）に整形する。
        /// </summary>
        /// <param name="duration">整形対象の時間量。</param>
        /// <returns>整形後の文字列。</returns>
        private static string FormatDuration(TimeSpan duration)
        {
            return $"{(int)duration.TotalHours:00}:{duration.Minutes:00}:{duration.Seconds:00}";
        }
    }
}
