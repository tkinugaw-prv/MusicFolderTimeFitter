using System.IO;
using MusicFolderTimeFitter.Models;
using MusicFolderTimeFitter.Services;
using MusicFolderTimeFitter.ViewModels;

namespace MusicFolderTimeFitter.Tests
{
    /// <summary>
    /// <see cref="MainViewModel"/> の入力状態の復元・保存ロジックを検証するテストクラス。
    /// </summary>
    public sealed class MainViewModelTests
    {
        /// <summary>
        /// 設定をメモリ上に保持するテスト用の設定サービス。
        /// </summary>
        private sealed class FakeSettingsService : ISettingsService
        {
            /// <summary>保持している設定。</summary>
            private AppSettings _settings;

            /// <summary>
            /// コンストラクター。
            /// </summary>
            /// <param name="settings">初期状態の設定。null の場合はデフォルト値。</param>
            public FakeSettingsService(AppSettings? settings = null)
            {
                _settings = settings ?? new AppSettings();
            }

            /// <summary>最後に保存された設定。</summary>
            public AppSettings Saved
            {
                get
                {
                    return _settings;
                }
            }

            /// <inheritdoc />
            public AppSettings Load()
            {
                // 実サービスと同様、呼び出しごとに独立したインスタンスを返す
                return new AppSettings
                {
                    AimpExecutablePath = _settings.AimpExecutablePath,
                    LastRootFolderPath = _settings.LastRootFolderPath,
                    IsDurationMode = _settings.IsDurationMode,
                    DurationMinutes = _settings.DurationMinutes,
                    TargetTime = _settings.TargetTime,
                };
            }

            /// <inheritdoc />
            public void Save(AppSettings settings)
            {
                _settings = settings;
            }
        }

        /// <summary>
        /// 指定したフォルダー一覧をスキャン結果として返すテスト用のフォルダースキャナー。
        /// </summary>
        private sealed class StubScanner : IMusicFolderScanner
        {
            /// <summary>スキャン結果として返すフォルダー一覧。</summary>
            private readonly IReadOnlyList<FolderScanResult> _folders;

            /// <summary>
            /// コンストラクター。
            /// </summary>
            /// <param name="folders">スキャン結果として返すフォルダー一覧。null の場合は空。</param>
            public StubScanner(IReadOnlyList<FolderScanResult>? folders = null)
            {
                _folders = folders ?? [];
            }

            /// <inheritdoc />
            public Task<FolderScanOutcome> ScanAsync(
                string rootPath,
                IProgress<ScanProgress>? progress,
                CancellationToken cancellationToken)
            {
                return Task.FromResult(new FolderScanOutcome(_folders.ToList(), _folders.Count, 0, []));
            }
        }

        /// <summary>
        /// 常に起動不可を返すテスト用の AIMP ランチャー。
        /// </summary>
        private sealed class StubAimpLauncher : IAimpLauncher
        {
            /// <inheritdoc />
            public bool CanLaunch(string? aimpExecutablePath)
            {
                return false;
            }

            /// <inheritdoc />
            public void Launch(string aimpExecutablePath, string folderPath)
            {
            }
        }

        /// <summary>現在時刻 14:00 固定で ViewModel を生成する。</summary>
        /// <param name="settingsService">使用する設定サービス。</param>
        /// <param name="scanner">使用するフォルダースキャナー。null の場合は空の結果を返すスタブ。</param>
        /// <returns>テスト用 ViewModel。</returns>
        private static MainViewModel CreateViewModelAt1400(
            ISettingsService settingsService,
            IMusicFolderScanner? scanner = null)
        {
            var timeProvider = new FixedTimeProvider(
                new DateTimeOffset(2026, 7, 12, 14, 0, 0, TimeSpan.Zero));

            return new MainViewModel(
                scanner ?? new StubScanner(),
                new RemainingTimeCalculator(timeProvider),
                settingsService,
                new StubAimpLauncher());
        }

        /// <summary>
        /// 設定が未保存の場合、時間指定入力がデフォルト値になることを検証する。
        /// </summary>
        [Fact]
        public void コンストラクター_設定なしはデフォルト値()
        {
            MainViewModel viewModel = CreateViewModelAt1400(new FakeSettingsService());

            Assert.True(viewModel.IsDurationMode);
            Assert.Equal(Const.DEFAULT_DURATION_MINUTES.ToString(), viewModel.DurationMinutesText);

            // 所要時間モードでは目標時刻欄は現在時刻 + 所要時間で上書きされる
            Assert.Equal("15:30", viewModel.TargetTimeText);
        }

        /// <summary>
        /// 目標時刻モードで保存された設定が、モード・時刻ともに復元されることを検証する。
        /// </summary>
        [Fact]
        public void コンストラクター_目標時刻モードの設定が復元される()
        {
            var settingsService = new FakeSettingsService(new AppSettings
            {
                IsDurationMode = false,
                DurationMinutes = 45,
                TargetTime = "19:45",
            });

            MainViewModel viewModel = CreateViewModelAt1400(settingsService);

            Assert.False(viewModel.IsDurationMode);
            Assert.True(viewModel.IsTargetTimeMode);
            Assert.Equal("45", viewModel.DurationMinutesText);
            Assert.Equal("19:45", viewModel.TargetTimeText);
        }

        /// <summary>
        /// 所要時間モードで保存された分数が復元されることを検証する。
        /// </summary>
        [Fact]
        public void コンストラクター_所要時間モードの分数が復元される()
        {
            var settingsService = new FakeSettingsService(new AppSettings
            {
                IsDurationMode = true,
                DurationMinutes = 30,
            });

            MainViewModel viewModel = CreateViewModelAt1400(settingsService);

            Assert.True(viewModel.IsDurationMode);
            Assert.Equal("30", viewModel.DurationMinutesText);
            Assert.Equal("14:30", viewModel.TargetTimeText);
        }

        /// <summary>
        /// 不正な値が保存されていた場合、デフォルト値にフォールバックすることを検証する。
        /// </summary>
        [Fact]
        public void コンストラクター_不正な保存値はデフォルトにフォールバック()
        {
            var settingsService = new FakeSettingsService(new AppSettings
            {
                IsDurationMode = false,
                DurationMinutes = 0,
                TargetTime = "not-a-time",
            });

            MainViewModel viewModel = CreateViewModelAt1400(settingsService);

            Assert.Equal(Const.DEFAULT_DURATION_MINUTES.ToString(), viewModel.DurationMinutesText);
            Assert.Equal(Const.DEFAULT_TARGET_TIME, viewModel.TargetTimeText);
        }

        /// <summary>
        /// 現在の入力内容が設定へ保存されることを検証する。
        /// </summary>
        [Fact]
        public void SaveInputSettings_現在の入力が保存される()
        {
            var settingsService = new FakeSettingsService();
            MainViewModel viewModel = CreateViewModelAt1400(settingsService);

            viewModel.RootFolderPath = @"D:\Music\Library";
            viewModel.IsDurationMode = false;
            viewModel.DurationMinutesText = "120";
            viewModel.TargetTimeText = "2015";

            viewModel.SaveInputSettings();

            Assert.Equal(@"D:\Music\Library", settingsService.Saved.LastRootFolderPath);
            Assert.False(settingsService.Saved.IsDurationMode);
            Assert.Equal(120, settingsService.Saved.DurationMinutes);

            // コロン省略入力は正規化して保存する
            Assert.Equal("20:15", settingsService.Saved.TargetTime);
        }

        /// <summary>
        /// パースできない入力は保存対象から除外され、前回の保存値が維持されることを検証する。
        /// </summary>
        [Fact]
        public void SaveInputSettings_不正な入力は前回値を維持する()
        {
            var settingsService = new FakeSettingsService(new AppSettings
            {
                DurationMinutes = 45,
                TargetTime = "19:45",
            });

            MainViewModel viewModel = CreateViewModelAt1400(settingsService);

            viewModel.DurationMinutesText = "abc";
            viewModel.TargetTimeText = "xx:yy";

            viewModel.SaveInputSettings();

            Assert.Equal(45, settingsService.Saved.DurationMinutes);
            Assert.Equal("19:45", settingsService.Saved.TargetTime);
        }

        /// <summary>
        /// 目標時刻モードのプロパティが所要時間モードと排他で連動することを検証する。
        /// </summary>
        [Fact]
        public void IsTargetTimeMode_IsDurationModeと排他で連動する()
        {
            MainViewModel viewModel = CreateViewModelAt1400(new FakeSettingsService());

            Assert.False(viewModel.IsTargetTimeMode);

            viewModel.IsTargetTimeMode = true;

            Assert.False(viewModel.IsDurationMode);

            viewModel.IsDurationMode = true;

            Assert.False(viewModel.IsTargetTimeMode);
        }

        /// <summary>
        /// 絞り込みテスト用のフォルダーを生成するヘルパー。
        /// </summary>
        /// <param name="relativePath">相対パス。</param>
        /// <param name="minutes">合計時間（分）。</param>
        /// <param name="composer">作曲者。</param>
        /// <param name="artist">アーティスト。</param>
        /// <returns>テスト用の集計結果。</returns>
        private static FolderScanResult CreateFolder(string relativePath, int minutes, string composer, string artist)
        {
            return new FolderScanResult
            {
                AbsolutePath = Path.Combine(@"D:\Music", relativePath),
                RelativePath = relativePath,
                TotalDuration = TimeSpan.FromMinutes(minutes),
                Composer = composer,
                Artist = artist,
                Album = "(不明)",
                AlbumArtist = "(不明)",
                Year = "(不明)",
            };
        }

        /// <summary>
        /// 3件のフォルダーを返すスキャナーで、所要時間 90 分のスキャンを実行済みの ViewModel を生成する。
        /// </summary>
        /// <returns>スキャン済みのテスト用 ViewModel。</returns>
        private static async Task<MainViewModel> CreateScannedViewModelAsync()
        {
            var scanner = new StubScanner(
            [
                CreateFolder("Goldberg", 50, "Bach", "Glenn Gould"),
                CreateFolder("Requiem", 60, "Mozart", "Karajan"),
                CreateFolder("Partitas", 70, "Bach", "Hilary Hahn"),
            ]);

            MainViewModel viewModel = CreateViewModelAt1400(new FakeSettingsService(), scanner);
            viewModel.RootFolderPath = Path.GetTempPath();
            viewModel.DurationMinutesText = "90";

            await viewModel.StartScanCommand.ExecuteAsync(null);

            return viewModel;
        }

        /// <summary>
        /// 絞り込みなしではスキャン該当の全件が合計時間降順で表示されることを検証する。
        /// </summary>
        [Fact]
        public async Task 絞り込みなしは全件表示()
        {
            MainViewModel viewModel = await CreateScannedViewModelAsync();

            Assert.False(viewModel.IsFiltering);
            Assert.Equal(["Partitas", "Requiem", "Goldberg"], viewModel.Results.Select(r => r.RelativePath));
            Assert.Equal(3, viewModel.MatchedCount);
            Assert.Equal(3, viewModel.DisplayedCount);
        }

        /// <summary>
        /// 絞り込み文字列に一致する行だけが、並び順を保ったまま表示されることを検証する。
        /// </summary>
        [Fact]
        public async Task FilterText_一致する行だけが順序を保って表示される()
        {
            MainViewModel viewModel = await CreateScannedViewModelAsync();

            viewModel.FilterText = "bach";

            Assert.True(viewModel.IsFiltering);
            Assert.Equal(["Partitas", "Goldberg"], viewModel.Results.Select(r => r.RelativePath));
            Assert.Equal(2, viewModel.DisplayedCount);
            Assert.Equal(3, viewModel.MatchedCount);
        }

        /// <summary>
        /// 複数語は AND 条件で絞り込まれることを検証する。
        /// </summary>
        [Fact]
        public async Task FilterText_複数語はAND条件()
        {
            MainViewModel viewModel = await CreateScannedViewModelAsync();

            viewModel.FilterText = "Bach Gould";

            Assert.Equal(["Goldberg"], viewModel.Results.Select(r => r.RelativePath));
        }

        /// <summary>
        /// クリアコマンドで絞り込みが解除され全件表示に戻ることを検証する。
        /// </summary>
        [Fact]
        public async Task ClearFilterCommand_全件表示に戻る()
        {
            MainViewModel viewModel = await CreateScannedViewModelAsync();
            viewModel.FilterText = "Mozart";

            viewModel.ClearFilterCommand.Execute(null);

            Assert.Equal(string.Empty, viewModel.FilterText);
            Assert.False(viewModel.IsFiltering);
            Assert.Equal(3, viewModel.Results.Count);
            Assert.Equal(3, viewModel.DisplayedCount);
        }

        /// <summary>
        /// 絞り込み文字列を入力したまま再スキャンしても、新しい結果に絞り込みが適用されることを検証する。
        /// </summary>
        [Fact]
        public async Task StartScan_絞り込みは再スキャン後も適用される()
        {
            MainViewModel viewModel = await CreateScannedViewModelAsync();
            viewModel.FilterText = "Mozart";

            await viewModel.StartScanCommand.ExecuteAsync(null);

            Assert.Equal(["Requiem"], viewModel.Results.Select(r => r.RelativePath));
            Assert.Equal(1, viewModel.DisplayedCount);
        }

        /// <summary>
        /// 絞り込みで 0 件になった場合、空状態に絞り込み用のメッセージが表示されることを検証する。
        /// </summary>
        [Fact]
        public async Task EmptyStateText_絞り込みで0件なら専用メッセージ()
        {
            MainViewModel viewModel = await CreateScannedViewModelAsync();

            viewModel.FilterText = "Beethoven";

            Assert.Empty(viewModel.Results);
            Assert.True(viewModel.IsEmptyStateVisible);
            Assert.Equal("絞り込み条件に一致するフォルダーがありません", viewModel.EmptyStateText);
        }

        /// <summary>
        /// スキャン該当が 0 件の場合、空状態に従来のメッセージが表示されることを検証する。
        /// </summary>
        [Fact]
        public void EmptyStateText_該当0件なら従来のメッセージ()
        {
            MainViewModel viewModel = CreateViewModelAt1400(new FakeSettingsService());

            Assert.Equal("条件に一致するフォルダーがありません", viewModel.EmptyStateText);
        }
    }
}
