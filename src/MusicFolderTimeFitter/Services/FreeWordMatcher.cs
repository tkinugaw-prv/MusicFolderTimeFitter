using System.Globalization;
using MusicFolderTimeFitter.Models;

namespace MusicFolderTimeFitter.Services
{
    /// <summary>
    /// 結果一覧のフリーワード絞り込みにおける一致判定を行うクラス。
    /// 空白区切りの各語を AND 条件とし、いずれかの検索対象列に部分一致すれば一致とみなす。
    /// アルバム・年は年号の数字が曲番号などの検索語に誤ヒットしやすいため対象外とする。
    /// </summary>
    public static class FreeWordMatcher
    {
        /// <summary>比較オプション（大文字小文字・全角半角を区別しない）。</summary>
        private const CompareOptions MATCH_OPTIONS = CompareOptions.IgnoreCase | CompareOptions.IgnoreWidth;

        /// <summary>
        /// 絞り込み文字列を空白（全角スペースを含む）で分割して検索語の配列にする。
        /// </summary>
        /// <param name="query">絞り込み文字列。</param>
        /// <returns>検索語の配列。空白のみ・null の場合は空配列。</returns>
        public static string[] SplitTerms(string? query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return [];
            }

            // separator に null を渡すと char.IsWhiteSpace の文字（U+3000 を含む）で分割される
            return query.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        }

        /// <summary>
        /// フォルダーの集計結果がすべての検索語に一致するかを判定する。
        /// </summary>
        /// <param name="folder">判定対象のフォルダー。</param>
        /// <param name="terms">検索語（<see cref="SplitTerms"/> の結果）。</param>
        /// <returns>すべての語がいずれかの検索対象列に含まれていれば true。語がなければ true。</returns>
        public static bool IsMatch(FolderScanResult folder, IReadOnlyList<string> terms)
        {
            string[] fields =
            [
                folder.RelativePath,
                folder.Composer,
                folder.Artist,
                folder.AlbumArtist,
            ];

            return terms.All(term => fields.Any(field => Contains(field, term)));
        }

        /// <summary>
        /// 大文字小文字・全角半角を区別せずに部分一致を判定する。
        /// </summary>
        /// <param name="text">検索対象の文字列。</param>
        /// <param name="term">検索語。</param>
        /// <returns>含まれていれば true。</returns>
        private static bool Contains(string text, string term)
        {
            return CultureInfo.InvariantCulture.CompareInfo.IndexOf(text, term, MATCH_OPTIONS) >= 0;
        }
    }
}
