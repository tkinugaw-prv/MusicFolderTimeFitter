using MusicFolderTimeFitter.Models;

namespace MusicFolderTimeFitter.Services
{
    /// <summary>
    /// TagLibSharp を使用して音楽ファイルのタグ情報を読み取るクラス。
    /// TagLibSharp への依存はこのクラスに隔離する。
    /// </summary>
    public sealed class TagLibTagReader : ITagReader
    {
        /// <summary>複数値タグの結合セパレーター。</summary>
        private const string MULTI_VALUE_SEPARATOR = "; ";

        /// <inheritdoc />
        public MusicFileInfo Read(string filePath)
        {
            using (TagLib.File file = TagLib.File.Create(filePath))
            {
                TagLib.Tag tag = file.Tag;

                return new MusicFileInfo(
                    Duration: file.Properties.Duration,
                    Composer: JoinValues(tag.Composers),
                    Artist: JoinValues(tag.Performers),
                    Album: tag.Album,
                    AlbumArtist: JoinValues(tag.AlbumArtists),
                    Year: tag.Year);
            }
        }

        /// <summary>
        /// 複数値のタグ配列を1つの表示文字列に結合する。
        /// </summary>
        /// <param name="values">タグ値の配列。</param>
        /// <returns>結合済み文字列。有効値がない場合は null。</returns>
        private static string? JoinValues(string[]? values)
        {
            if (values == null)
            {
                return null;
            }

            string[] validValues = values
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .ToArray();

            if (validValues.Length == 0)
            {
                return null;
            }

            return string.Join(MULTI_VALUE_SEPARATOR, validValues);
        }
    }
}
