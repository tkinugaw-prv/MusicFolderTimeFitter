using MusicFolderTimeFitter.Models;
using MusicFolderTimeFitter.Services;

namespace MusicFolderTimeFitter.Tests
{
    /// <summary>
    /// <see cref="FreeWordMatcher"/> の検索語分割と一致判定を検証するテストクラス。
    /// </summary>
    public sealed class FreeWordMatcherTests
    {
        /// <summary>
        /// 各列に識別しやすい値を持つ集計結果を生成するヘルパー。
        /// </summary>
        /// <returns>テスト用の集計結果。</returns>
        private static FolderScanResult CreateResult()
        {
            return new FolderScanResult
            {
                AbsolutePath = @"D:\Music\Baroque\Goldberg",
                RelativePath = @"Baroque\Goldberg",
                TotalDuration = TimeSpan.FromMinutes(50),
                Composer = "Bach",
                Artist = "Glenn Gould",
                Album = "Goldberg Variations",
                AlbumArtist = "Sony Classical",
                Year = "1981",
            };
        }

        /// <summary>
        /// 絞り込み文字列から検索語を取り出して判定するヘルパー。
        /// </summary>
        /// <param name="query">絞り込み文字列。</param>
        /// <returns>一致すれば true。</returns>
        private static bool Match(string? query)
        {
            return FreeWordMatcher.IsMatch(CreateResult(), FreeWordMatcher.SplitTerms(query));
        }

        /// <summary>
        /// 空・空白のみ・null の絞り込み文字列は語なしとなり、全件一致することを検証する。
        /// </summary>
        /// <param name="query">絞り込み文字列。</param>
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("\u3000")]
        public void 語なしは全件一致(string? query)
        {
            Assert.Empty(FreeWordMatcher.SplitTerms(query));
            Assert.True(Match(query));
        }

        /// <summary>
        /// 検索対象列（フォルダー名・作曲者・アーティスト・アルバムアーティスト）のいずれでも部分一致することを検証する。
        /// </summary>
        /// <param name="query">各列の一部。</param>
        [Theory]
        [InlineData("Baroque")]
        [InlineData("Bach")]
        [InlineData("Gould")]
        [InlineData("Sony")]
        public void 各列に部分一致する(string query)
        {
            Assert.True(Match(query));
        }

        /// <summary>
        /// アルバム・年は検索対象外であることを検証する。
        /// </summary>
        /// <param name="query">アルバムまたは年にのみ含まれる語。</param>
        [Theory]
        [InlineData("Variations")]
        [InlineData("1981")]
        [InlineData("8")]
        public void アルバムと年は検索対象外(string query)
        {
            Assert.False(Match(query));
        }

        /// <summary>
        /// どの列にも含まれない語は一致しないことを検証する。
        /// </summary>
        [Fact]
        public void どの列にも含まれない語は不一致()
        {
            Assert.False(Match("Mozart"));
        }

        /// <summary>
        /// 大文字小文字・全角半角を区別しないことを検証する。
        /// </summary>
        /// <param name="query">表記ゆれのある検索語。</param>
        [Theory]
        [InlineData("bach")]
        [InlineData("GOULD")]
        [InlineData("ＢＡＣＨ")]
        [InlineData("ＳＯＮＹ")]
        public void 大文字小文字と全角半角を区別しない(string query)
        {
            Assert.True(Match(query));
        }

        /// <summary>
        /// 複数語は AND 条件で、語が別々の列にあっても一致することを検証する。
        /// </summary>
        [Fact]
        public void 複数語は別々の列でもすべて含めば一致()
        {
            Assert.True(Match("Bach Gould Sony"));
        }

        /// <summary>
        /// 複数語のうち1語でも含まれなければ不一致となることを検証する。
        /// </summary>
        [Fact]
        public void 複数語の一部しか含まなければ不一致()
        {
            Assert.False(Match("Bach Mozart"));
        }

        /// <summary>
        /// 全角スペースも区切り文字として扱われることを検証する。
        /// </summary>
        [Fact]
        public void 全角スペースでも区切られる()
        {
            Assert.Equal(["Bach", "Gould"], FreeWordMatcher.SplitTerms("Bach\u3000Gould"));
            Assert.True(Match("Bach\u3000Gould"));
        }
    }
}
