using System.Windows;
using System.Windows.Input;
using MusicFolderTimeFitter.Interop;
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

        /// <summary>タスクトレイ格納の管理（HWND 確定後に生成）。</summary>
        private TrayIconController? _trayIconController;

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
        /// HWND 確定後にタイトルバーのダーク化とトレイアイコンの初期化を行う。
        /// </summary>
        /// <param name="e">イベント引数。</param>
        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            DwmDarkTitleBar.Apply(this);
            _trayIconController = new TrayIconController(this);
        }

        /// <summary>
        /// 最小化時はトレイへ格納し、それ以外の状態は復帰用に記録する。
        /// </summary>
        /// <param name="e">イベント引数。</param>
        protected override void OnStateChanged(EventArgs e)
        {
            base.OnStateChanged(e);

            if (WindowState == WindowState.Minimized)
            {
                _trayIconController?.HideToTray();
            }
            else
            {
                _trayIconController?.RememberWindowState(WindowState);
            }
        }

        /// <summary>
        /// ウィンドウ破棄時にトレイアイコンを破棄する。
        /// </summary>
        /// <param name="e">イベント引数。</param>
        protected override void OnClosed(EventArgs e)
        {
            _trayIconController?.Dispose();
            _trayIconController = null;
            base.OnClosed(e);
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
