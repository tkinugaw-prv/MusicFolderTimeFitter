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

            var mainWindow = new MainWindow(settingsService)
            {
                DataContext = viewModel,
            };

            mainWindow.Show();
        }
    }
}
