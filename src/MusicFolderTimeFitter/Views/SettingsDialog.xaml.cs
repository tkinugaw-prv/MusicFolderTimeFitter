using System.Windows;
using MusicFolderTimeFitter.ViewModels;

namespace MusicFolderTimeFitter.Views
{
    /// <summary>
    /// 設定ダイアログのコードビハインド。ViewModel の閉じる要求をウインドウ操作に変換する。
    /// </summary>
    public partial class SettingsDialog : Window
    {
        /// <summary>
        /// コンストラクター。
        /// </summary>
        /// <param name="viewModel">設定ダイアログの ViewModel。</param>
        public SettingsDialog(SettingsViewModel viewModel)
        {
            InitializeComponent();

            DataContext = viewModel;
            viewModel.RequestClose += OnRequestClose;
        }

        /// <summary>
        /// ViewModel からの閉じる要求を処理する。
        /// </summary>
        /// <param name="saved">保存して閉じる場合は true。</param>
        private void OnRequestClose(bool saved)
        {
            DialogResult = saved;
        }
    }
}
