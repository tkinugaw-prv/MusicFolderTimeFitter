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
        /// 何もしないテスト用のフォルダースキャナー。
        /// </summary>
        private sealed class StubScanner : IMusicFolderScanner
        {
            /// <inheritdoc />
            public Task<FolderScanOutcome> ScanAsync(
                string rootPath,
                IProgress<ScanProgress>? progress,
                CancellationToken cancellationToken)
            {
                return Task.FromResult(new FolderScanOutcome([], 0, 0, []));
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
        /// <returns>テスト用 ViewModel。</returns>
        private static MainViewModel CreateViewModelAt1400(ISettingsService settingsService)
        {
            var timeProvider = new FixedTimeProvider(
                new DateTimeOffset(2026, 7, 12, 14, 0, 0, TimeSpan.Zero));

            return new MainViewModel(
                new StubScanner(),
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
    }
}
