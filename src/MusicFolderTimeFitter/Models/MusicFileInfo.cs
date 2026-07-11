namespace MusicFolderTimeFitter.Models
{
    /// <summary>
    /// 音楽ファイル1件分のタグ情報を表すレコード。
    /// </summary>
    /// <param name="Duration">再生時間。</param>
    /// <param name="Composer">作曲者（複数値は "; " 結合済み。値なしは null または空文字列）。</param>
    /// <param name="Artist">アーティスト（同上）。</param>
    /// <param name="Album">アルバム（同上）。</param>
    /// <param name="AlbumArtist">アルバムアーティスト（同上）。</param>
    /// <param name="Year">年（0 は値なし扱い）。</param>
    public sealed record MusicFileInfo(
        TimeSpan Duration,
        string? Composer,
        string? Artist,
        string? Album,
        string? AlbumArtist,
        uint Year);
}
