using MusicFolderTimeFitter.Models;

namespace MusicFolderTimeFitter.Tests
{
    /// <summary>
    /// <see cref="FolderScanResult"/> の表示フォーマットを検証するテストクラス。
    /// </summary>
    public sealed class FolderScanResultTests
    {
        /// <summary>
        /// 指定した合計時間を持つ結果を生成するヘルパー。
        /// </summary>
        /// <param name="totalDuration">合計再生時間。</param>
        /// <returns>テスト用の集計結果。</returns>
        private static FolderScanResult CreateResult(TimeSpan totalDuration)
        {
            return new FolderScanResult
            {
                AbsolutePath = @"D:\Music\Album",
                RelativePath = "Album",
                TotalDuration = totalDuration,
                Composer = "(不明)",
                Artist = "(不明)",
                Album = "(不明)",
                AlbumArtist = "(不明)",
                Year = "(不明)",
            };
        }

        /// <summary>
        /// 合計時間が HH:mm:ss 形式でフォーマットされることを検証する。
        /// </summary>
        [Fact]
        public void TotalDurationText_HHmmss形式でフォーマットされる()
        {
            FolderScanResult result = CreateResult(new TimeSpan(1, 12, 40));

            Assert.Equal("01:12:40", result.TotalDurationText);
        }

        /// <summary>
        /// 24時間を超える合計時間も総時間表記（日数に繰り上げない）で
        /// フォーマットされることを検証する。
        /// </summary>
        [Fact]
        public void TotalDurationText_24時間超も総時間表記になる()
        {
            FolderScanResult result = CreateResult(new TimeSpan(30, 5, 9));

            Assert.Equal("30:05:09", result.TotalDurationText);
        }
    }
}
