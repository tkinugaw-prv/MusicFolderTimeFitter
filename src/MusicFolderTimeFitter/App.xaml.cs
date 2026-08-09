using System.Windows;
using MusicFolderTimeFitter.Services;
using MusicFolderTimeFitter.ViewModels;
using MusicFolderTimeFitter.Views;

namespace MusicFolderTimeFitter
{
    /// <summary>
    /// アプリケーションのエントリーポイント。サービスの組み立てとメイン画面の起動を行う。
    /// </summary>
    public partial class App : Application
    {
        /// <summary>メイン画面の ViewModel（終了時の設定保存に使用）。</summary>
        private MainViewModel? _mainViewModel;

        /// <summary>
        /// 起動時にサービス群を構築し、メイン画面を表示する。
        /// </summary>
        /// <param name="e">起動イベント引数。</param>
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            var settingsService = new JsonSettingsService();
            var scanner = new MusicFolderScanner(new TagLibTagReader());
            var remainingTimeCalculator = new RemainingTimeCalculator(TimeProvider.System);
            var aimpLauncher = new AimpLauncher();

            var viewModel = new MainViewModel(
                scanner,
                remainingTimeCalculator,
                settingsService,
                aimpLauncher);

            _mainViewModel = viewModel;

            var mainWindow = new MainWindow(settingsService)
            {
                DataContext = viewModel,
            };

            mainWindow.Show();
        }

        /// <summary>
        /// 終了時に現在の入力内容を設定へ保存する（スキャン未実行のまま終了した場合も記憶するため）。
        /// </summary>
        /// <param name="e">終了イベント引数。</param>
        protected override void OnExit(ExitEventArgs e)
        {
            try
            {
                _mainViewModel?.SaveInputSettings();
            }
            catch (Exception)
            {
                // 設定の保存失敗で終了処理を妨げない
            }

            base.OnExit(e);
        }
    }
}
