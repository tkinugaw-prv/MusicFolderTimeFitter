using System.IO;
using MusicFolderTimeFitter.Models;
using MusicFolderTimeFitter.Services;

namespace MusicFolderTimeFitter.Tests
{
    /// <summary>
    /// <see cref="MusicFolderScanner"/> のスキャン・集計・除外ルールを検証するテストクラス。
    /// 実音源は使用せず、スタブの <see cref="ITagReader"/> と一時フォルダーで検証する。
    /// </summary>
    public sealed class MusicFolderScannerTests : IDisposable
    {
        /// <summary>
        /// ファイル名からタグ情報を返すスタブリーダー。
        /// ファイル名に "broken" を含む場合は読取失敗として例外をスローする。
        /// </summary>
        private sealed class StubTagReader : ITagReader
        {
            /// <summary>ファイル名（拡張子なし）→ タグ情報のマップ。</summary>
            private readonly Dictionary<string, MusicFileInfo> _map;

            /// <summary>
            /// コンストラクター。
            /// </summary>
            /// <param name="map">ファイル名（拡張子なし）→ タグ情報のマップ。</param>
            public StubTagReader(Dictionary<string, MusicFileInfo> map)
            {
                _map = map;
            }

            /// <inheritdoc />
            public MusicFileInfo Read(string filePath)
            {
                string name = Path.GetFileNameWithoutExtension(filePath);

                if (name.Contains("broken"))
                {
                    throw new InvalidOperationException("タグが壊れています（テスト用）");
                }

                if (_map.TryGetValue(name, out MusicFileInfo? info))
                {
                    return info;
                }

                return new MusicFileInfo(TimeSpan.FromMinutes(3), null, null, null, null, 0);
            }
        }

        /// <summary>テスト用一時ルートフォルダー。</summary>
        private readonly string _rootPath;

        /// <summary>
        /// コンストラクター。一時ルートフォルダーを作成する。
        /// </summary>
        public MusicFolderScannerTests()
        {
            _rootPath = Path.Combine(Path.GetTempPath(), "MusicFolderScannerTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_rootPath);
        }

        /// <summary>
        /// テスト後に一時フォルダーを削除する。
        /// </summary>
        public void Dispose()
        {
            try
            {
                Directory.Delete(_rootPath, recursive: true);
            }
            catch (Exception)
            {
                // 一時フォルダーの削除失敗はテスト結果に影響しないため無視する
            }
        }

        /// <summary>
        /// 指定した相対フォルダーに空のダミーファイルを作成する。
        /// </summary>
        /// <param name="relativeFolder">ルートからの相対フォルダーパス（null はルート直下）。</param>
        /// <param name="fileNames">作成するファイル名。</param>
        private void CreateFiles(string? relativeFolder, params string[] fileNames)
        {
            string folder = relativeFolder == null ? _rootPath : Path.Combine(_rootPath, relativeFolder);
            Directory.CreateDirectory(folder);

            foreach (string fileName in fileNames)
            {
                File.WriteAllBytes(Path.Combine(folder, fileName), Array.Empty<byte>());
            }
        }

        /// <summary>
        /// スキャンを同期的に実行するヘルパー。
        /// </summary>
        /// <param name="reader">使用するタグリーダー。</param>
        /// <returns>スキャン結果。</returns>
        private FolderScanOutcome Scan(ITagReader reader)
        {
            var scanner = new MusicFolderScanner(reader);

            return scanner.ScanAsync(_rootPath, progress: null, CancellationToken.None)
                .GetAwaiter()
                .GetResult();
        }

        /// <summary>
        /// フォルダー直下のファイルのみが合計され、サブフォルダーは独立した集計単位になる
        /// （累積合算しない）ことを検証する。
        /// </summary>
        [Fact]
        public void ScanAsync_フォルダー単位で集計されサブフォルダーは独立単位になる()
        {
            CreateFiles(@"AlbumA", "a1.flac", "a2.flac");
            CreateFiles(@"AlbumA\Disc2", "b1.m4a");

            var reader = new StubTagReader(new Dictionary<string, MusicFileInfo>
            {
                ["a1"] = new MusicFileInfo(TimeSpan.FromMinutes(10), null, null, null, null, 0),
                ["a2"] = new MusicFileInfo(TimeSpan.FromMinutes(5), null, null, null, null, 0),
                ["b1"] = new MusicFileInfo(TimeSpan.FromMinutes(7), null, null, null, null, 0),
            });

            FolderScanOutcome outcome = Scan(reader);

            Assert.Equal(2, outcome.Folders.Count);

            FolderScanResult albumA = outcome.Folders.Single(f => f.RelativePath == "AlbumA");
            FolderScanResult disc2 = outcome.Folders.Single(f => f.RelativePath == Path.Combine("AlbumA", "Disc2"));

            // 親フォルダーに子フォルダーの時間は加算されない
            Assert.Equal(TimeSpan.FromMinutes(15), albumA.TotalDuration);
            Assert.Equal(TimeSpan.FromMinutes(7), disc2.TotalDuration);
        }

        /// <summary>
        /// 対象拡張子（.flac / .m4a）以外のファイルは集計されないことを検証する。
        /// </summary>
        [Fact]
        public void ScanAsync_対象外拡張子は集計されない()
        {
            CreateFiles("AlbumA", "a1.flac", "cover.jpg", "notes.txt", "track.mp3");

            var reader = new StubTagReader(new Dictionary<string, MusicFileInfo>
            {
                ["a1"] = new MusicFileInfo(TimeSpan.FromMinutes(4), null, null, null, null, 0),
            });

            FolderScanOutcome outcome = Scan(reader);

            FolderScanResult albumA = Assert.Single(outcome.Folders);
            Assert.Equal(TimeSpan.FromMinutes(4), albumA.TotalDuration);
        }

        /// <summary>
        /// タグ読取に失敗するファイルを含むフォルダーは、フォルダーごと除外され
        /// 除外件数にカウントされることを検証する。
        /// </summary>
        [Fact]
        public void ScanAsync_タグ読取失敗を含むフォルダーは除外される()
        {
            CreateFiles("GoodAlbum", "ok1.flac");
            CreateFiles("BadAlbum", "ok2.flac", "broken.flac");

            var reader = new StubTagReader(new Dictionary<string, MusicFileInfo>());

            FolderScanOutcome outcome = Scan(reader);

            FolderScanResult remaining = Assert.Single(outcome.Folders);
            Assert.Equal("GoodAlbum", remaining.RelativePath);
            Assert.Equal(1, outcome.ExcludedCount);
            Assert.Contains(outcome.ExclusionLogs, log => log.Contains("broken.flac"));
        }

        /// <summary>
        /// 対象ファイルが1つもないフォルダーは一覧に含まれず、
        /// 除外件数にもカウントされないことを検証する。
        /// </summary>
        [Fact]
        public void ScanAsync_対象ファイルなしのフォルダーは一覧対象外()
        {
            CreateFiles("EmptyFolder");
            CreateFiles("ImagesOnly", "cover.jpg");
            CreateFiles("AlbumA", "a1.flac");

            var reader = new StubTagReader(new Dictionary<string, MusicFileInfo>());

            FolderScanOutcome outcome = Scan(reader);

            FolderScanResult albumA = Assert.Single(outcome.Folders);
            Assert.Equal("AlbumA", albumA.RelativePath);
            Assert.Equal(0, outcome.ExcludedCount);
        }

        /// <summary>
        /// タグ5項目の代表値（最頻値→タイは最小値→全て値なしは "(不明)"）が
        /// フォルダー集計結果に反映されることを検証する。
        /// </summary>
        [Fact]
        public void ScanAsync_タグ代表値が選定される()
        {
            CreateFiles("AlbumA", "t1.flac", "t2.flac", "t3.flac");

            var reader = new StubTagReader(new Dictionary<string, MusicFileInfo>
            {
                ["t1"] = new MusicFileInfo(TimeSpan.FromMinutes(3), "Bach", "PlayerX", "AlbumName", null, 1975),
                ["t2"] = new MusicFileInfo(TimeSpan.FromMinutes(3), "Bach", "PlayerY", "AlbumName", null, 0),
                ["t3"] = new MusicFileInfo(TimeSpan.FromMinutes(3), "Mozart", "PlayerX", "AlbumName", null, 1975),
            });

            FolderScanOutcome outcome = Scan(reader);

            FolderScanResult albumA = Assert.Single(outcome.Folders);
            Assert.Equal("Bach", albumA.Composer);
            Assert.Equal("PlayerX", albumA.Artist);
            Assert.Equal("AlbumName", albumA.Album);
            Assert.Equal("(不明)", albumA.AlbumArtist);
            Assert.Equal("1975", albumA.Year);
        }

        /// <summary>
        /// ルートフォルダー直下にも対象ファイルがある場合、
        /// ルート自身が1つの集計単位として（フォルダー名表示で）含まれることを検証する。
        /// </summary>
        [Fact]
        public void ScanAsync_ルート直下のファイルも集計単位になる()
        {
            CreateFiles(null, "r1.flac");

            var reader = new StubTagReader(new Dictionary<string, MusicFileInfo>
            {
                ["r1"] = new MusicFileInfo(TimeSpan.FromMinutes(2), null, null, null, null, 0),
            });

            FolderScanOutcome outcome = Scan(reader);

            FolderScanResult root = Assert.Single(outcome.Folders);
            Assert.Equal(Path.GetFileName(_rootPath), root.RelativePath);
            Assert.Equal(TimeSpan.FromMinutes(2), root.TotalDuration);
        }

        /// <summary>
        /// 進捗通知でスキャン件数・除外件数が報告されることを検証する。
        /// </summary>
        [Fact]
        public async Task ScanAsync_進捗が通知される()
        {
            CreateFiles("AlbumA", "a1.flac");
            CreateFiles("BadAlbum", "broken.flac");

            var reader = new StubTagReader(new Dictionary<string, MusicFileInfo>());
            var scanner = new MusicFolderScanner(reader);
            var reports = new List<ScanProgress>();
            var progress = new SynchronousProgress(reports);

            FolderScanOutcome outcome =
                await scanner.ScanAsync(_rootPath, progress, CancellationToken.None);

            // ルート + AlbumA + BadAlbum = 3 フォルダー分の通知
            Assert.Equal(3, reports.Count);
            Assert.Equal(3, outcome.ScannedCount);
            Assert.Equal(1, reports[^1].ExcludedCount);
        }

        /// <summary>
        /// テスト用の同期 IProgress 実装（Report を即座にリストへ記録する）。
        /// </summary>
        private sealed class SynchronousProgress : IProgress<ScanProgress>
        {
            /// <summary>通知の記録先。</summary>
            private readonly List<ScanProgress> _reports;

            /// <summary>
            /// コンストラクター。
            /// </summary>
            /// <param name="reports">通知の記録先。</param>
            public SynchronousProgress(List<ScanProgress> reports)
            {
                _reports = reports;
            }

            /// <inheritdoc />
            public void Report(ScanProgress value)
            {
                _reports.Add(value);
            }
        }
    }
}
