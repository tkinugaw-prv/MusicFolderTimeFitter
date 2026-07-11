namespace MusicFolderTimeFitter.Services
{
    /// <summary>
    /// フォルダー内の複数ファイルのタグ値から代表値を1つ選定するクラス。
    /// 選定ルール: 値なしを母数から除外した上で、最頻値 → 同数タイは最小値 → 有効値が皆無なら null。
    /// </summary>
    public static class RepresentativeValueSelector
    {
        /// <summary>
        /// 文字列タグ（作曲者・アーティスト・アルバム・アルバムアーティスト等）の代表値を選定する。
        /// null または空白のみの値は集計母数から除外する。
        /// </summary>
        /// <param name="values">フォルダー内全ファイルのタグ値。</param>
        /// <returns>代表値。有効値が1つもない場合は null。</returns>
        public static string? SelectString(IEnumerable<string?> values)
        {
            List<string> validValues = values
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Select(v => v!)
                .ToList();

            if (validValues.Count == 0)
            {
                return null;
            }

            return SelectCore(validValues, StringComparer.Ordinal);
        }

        /// <summary>
        /// 年タグの代表値を選定する。0 は値なしとして集計母数から除外する。
        /// </summary>
        /// <param name="years">フォルダー内全ファイルの年タグ値。</param>
        /// <returns>代表値。有効値が1つもない場合は null。</returns>
        public static uint? SelectYear(IEnumerable<uint> years)
        {
            List<uint> validValues = years.Where(y => y != 0).ToList();

            if (validValues.Count == 0)
            {
                return null;
            }

            return SelectCore(validValues, Comparer<uint>.Default);
        }

        /// <summary>
        /// 有効値の集合から最頻値（同数タイは最小値）を選定する共通ロジック。
        /// </summary>
        /// <typeparam name="T">値の型。</typeparam>
        /// <param name="validValues">値なしを除外済みの有効値集合（1件以上であること）。</param>
        /// <param name="comparer">タイブレークに使用する比較子。</param>
        /// <returns>代表値。</returns>
        private static T SelectCore<T>(IReadOnlyCollection<T> validValues, IComparer<T> comparer)
            where T : notnull
        {
            var grouped = validValues
                .GroupBy(v => v)
                .Select(g => new { Value = g.Key, Count = g.Count() })
                .ToList();

            int maxCount = grouped.Max(g => g.Count);

            return grouped
                .Where(g => g.Count == maxCount)
                .Select(g => g.Value)
                .Order(comparer)
                .First();
        }
    }
}
