using System.Windows;
using System.Windows.Input;
using MusicFolderTimeFitter.Models;
using MusicFolderTimeFitter.Services;
using MusicFolderTimeFitter.ViewModels;

namespace MusicFolderTimeFitter.Views
{
    /// <summary>
    /// メイン画面のコードビハインド。設定ダイアログの開閉と行ダブルクリック再生を仲介する。
    /// </summary>
    public partial class MainWindow : Window
    {
        /// <summary>設定永続化サービス（設定ダイアログの生成に使用）。</summary>
        private readonly ISettingsService _settingsService;

        /// <summary>
        /// コンストラクター。
        /// </summary>
        /// <param name="settingsService">設定永続化サービス。</param>
        public MainWindow(ISettingsService settingsService)
        {
            _settingsService = settingsService;
            InitializeComponent();
        }

        /// <summary>
        /// 「設定」ボタンクリック時に設定ダイアログをモーダル表示する。
        /// </summary>
        /// <param name="sender">イベント発生元。</param>
        /// <param name="e">イベント引数。</param>
        private void OnSettingsButtonClick(object sender, RoutedEventArgs e)
        {
            var dialog = new SettingsDialog(new SettingsViewModel(_settingsService))
            {
                Owner = this,
            };

            dialog.ShowDialog();

            if (DataContext is MainViewModel viewModel)
            {
                viewModel.RefreshAimpAvailability();
            }
        }

        /// <summary>
        /// 結果一覧の行ダブルクリックで選択フォルダーを AIMP で再生する。
        /// </summary>
        /// <param name="sender">イベント発生元。</param>
        /// <param name="e">イベント引数。</param>
        private void OnResultGridMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is MainViewModel viewModel
                && ResultGrid.SelectedItem is FolderScanResult folder)
            {
                viewModel.PlayFolderCommand.Execute(folder);
            }
        }
    }
}
