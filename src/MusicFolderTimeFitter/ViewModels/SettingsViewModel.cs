using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MusicFolderTimeFitter.Models;
using MusicFolderTimeFitter.Services;

namespace MusicFolderTimeFitter.ViewModels
{
    /// <summary>
    /// 設定ダイアログの ViewModel。AIMP 実行ファイルパスの編集・保存を行う。
    /// </summary>
    public sealed partial class SettingsViewModel : ObservableObject
    {
        /// <summary>設定永続化サービス。</summary>
        private readonly ISettingsService _settingsService;

        /// <summary>AIMP 実行ファイルパスの編集値。</summary>
        [ObservableProperty]
        private string _aimpExecutablePath = string.Empty;

        /// <summary>ダイアログを閉じる要求（true=保存して閉じる / false=キャンセル）。</summary>
        public event Action<bool>? RequestClose;

        /// <summary>
        /// コンストラクター。現在の設定値を読み込んで編集値を初期化する。
        /// </summary>
        /// <param name="settingsService">設定永続化サービス。</param>
        public SettingsViewModel(ISettingsService settingsService)
        {
            _settingsService = settingsService;
            AimpExecutablePath = _settingsService.Load().AimpExecutablePath;
        }

        /// <summary>
        /// AIMP 実行ファイル選択ダイアログを開く。
        /// </summary>
        [RelayCommand]
        private void BrowseAimpExecutable()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "AIMP 実行ファイルを選択",
                Filter = "実行ファイル (*.exe)|*.exe|すべてのファイル (*.*)|*.*",
                FileName = AimpExecutablePath,
            };

            if (dialog.ShowDialog() == true)
            {
                AimpExecutablePath = dialog.FileName;
            }
        }

        /// <summary>
        /// 編集値を設定に保存してダイアログを閉じる。
        /// </summary>
        [RelayCommand]
        private void Save()
        {
            AppSettings settings = _settingsService.Load();
            settings.AimpExecutablePath = AimpExecutablePath;
            _settingsService.Save(settings);

            RequestClose?.Invoke(true);
        }

        /// <summary>
        /// 保存せずにダイアログを閉じる。
        /// </summary>
        [RelayCommand]
        private void Cancel()
        {
            RequestClose?.Invoke(false);
        }
    }
}
