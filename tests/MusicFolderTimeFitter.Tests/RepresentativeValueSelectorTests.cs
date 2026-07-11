using MusicFolderTimeFitter.Services;

namespace MusicFolderTimeFitter.Tests
{
    /// <summary>
    /// <see cref="RepresentativeValueSelector"/> の代表値選定ロジックを検証するテストクラス。
    /// </summary>
    public sealed class RepresentativeValueSelectorTests
    {
        /// <summary>
        /// 文字列: 最頻値が代表値として選定されることを検証する。
        /// </summary>
        [Fact]
        public void SelectString_最頻値が選定される()
        {
            string? result = RepresentativeValueSelector.SelectString(
                new[] { "Bach", "Mozart", "Bach", "Bach", "Mozart" });

            Assert.Equal("Bach", result);
        }

        /// <summary>
        /// 文字列: 同数タイの場合は最小値（序数比較）が選定されることを検証する。
        /// </summary>
        [Fact]
        public void SelectString_同数タイは最小値が選定される()
        {
            string? result = RepresentativeValueSelector.SelectString(
                new[] { "Mozart", "Bach", "Mozart", "Bach" });

            Assert.Equal("Bach", result);
        }

        /// <summary>
        /// 文字列: null・空文字列・空白のみの値は集計母数から除外されることを検証する。
        /// 有効値が少数派でも、値なしを除外した上で最頻値が選定される。
        /// </summary>
        [Fact]
        public void SelectString_値なしは母数から除外される()
        {
            string? result = RepresentativeValueSelector.SelectString(
                new[] { null, "", "  ", null, null, "Beethoven", "Beethoven" });

            Assert.Equal("Beethoven", result);
        }

        /// <summary>
        /// 文字列: 全ファイルが値なしの場合は null が返ることを検証する。
        /// </summary>
        [Fact]
        public void SelectString_全て値なしの場合はnull()
        {
            string? result = RepresentativeValueSelector.SelectString(
                new string?[] { null, "", "   " });

            Assert.Null(result);
        }

        /// <summary>
        /// 文字列: 入力が空の場合は null が返ることを検証する。
        /// </summary>
        [Fact]
        public void SelectString_空の入力はnull()
        {
            string? result = RepresentativeValueSelector.SelectString(Array.Empty<string?>());

            Assert.Null(result);
        }

        /// <summary>
        /// 年: 最頻値が代表値として選定されることを検証する。
        /// </summary>
        [Fact]
        public void SelectYear_最頻値が選定される()
        {
            uint? result = RepresentativeValueSelector.SelectYear(
                new uint[] { 2001, 1999, 2001, 2001 });

            Assert.Equal(2001u, result);
        }

        /// <summary>
        /// 年: 同数タイの場合は最も古い（小さい）年が選定されることを検証する。
        /// </summary>
        [Fact]
        public void SelectYear_同数タイは最古の年が選定される()
        {
            uint? result = RepresentativeValueSelector.SelectYear(
                new uint[] { 2010, 1975, 2010, 1975 });

            Assert.Equal(1975u, result);
        }

        /// <summary>
        /// 年: 0（値なし）は集計母数から除外されることを検証する。
        /// 仕様例: 10曲中6曲が年タグなし・4曲が1975 → 代表値は 1975。
        /// </summary>
        [Fact]
        public void SelectYear_値なしのゼロは母数から除外される()
        {
            uint? result = RepresentativeValueSelector.SelectYear(
                new uint[] { 0, 0, 0, 0, 0, 0, 1975, 1975, 1975, 1975 });

            Assert.Equal(1975u, result);
        }

        /// <summary>
        /// 年: 全ファイルが 0（値なし）の場合は null が返ることを検証する。
        /// </summary>
        [Fact]
        public void SelectYear_全てゼロの場合はnull()
        {
            uint? result = RepresentativeValueSelector.SelectYear(new uint[] { 0, 0, 0 });

            Assert.Null(result);
        }
    }
}
