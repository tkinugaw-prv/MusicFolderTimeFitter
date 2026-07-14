using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MusicFolderTimeFitter.Models;
using MusicFolderTimeFitter.Services;

namespace MusicFolderTimeFitter.ViewModels
{
    /// <summary>
    /// メイン画面の ViewModel。入力状態・スキャン実行・結果一覧・ステータスを管理する。
    /// </summary>
    public sealed partial class MainViewModel : ObservableObject
    {
        /// <summary>フォルダースキャンサービス。</summary>
        private readonly IMusicFolderScanner _scanner;

        /// <summary>残り時間算出サービス。</summary>
        private readonly RemainingTimeCalculator _remainingTimeCalculator;

        /// <summary>設定永続化サービス。</summary>
        private readonly ISettingsService _settingsService;

        /// <summary>AIMP 起動サービス。</summary>
        private readonly IAimpLauncher _aimpLauncher;

        /// <summary>ルートフォルダーの絶対パス。</summary>
        [ObservableProperty]
        private string _rootFolderPath = string.Empty;

        /// <summary>所要時間モードが選択されているか（false は目標時刻モード）。</summary>
        [ObservableProperty]
        private bool _isDurationMode = true;

        /// <summary>所要時間モードの分数入力テキスト。</summary>
        [ObservableProperty]
        private string _durationMinutesText = "90";

        /// <summary>目標時刻モードの時刻入力テキスト（HH:mm）。</summary>
        [ObservableProperty]
        private string _targetTimeText = "18:30";

        /// <summary>スキャン実行中か。</summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsEmptyStateVisible))]
        [NotifyCanExecuteChangedFor(nameof(StartScanCommand))]
        private bool _isScanning;

        /// <summary>スキャン済みフォルダー数。</summary>
        [ObservableProperty]
        private int _scannedCount;

        /// <summary>条件に該当したフォルダー数。</summary>
        [ObservableProperty]
        private int _matchedCount;

        /// <summary>除外されたフォルダー数（タグ読取失敗等）。</summary>
        [ObservableProperty]
        private int _excludedCount;

        /// <summary>ステータスバー右側に表示する状態テキスト。</summary>
        [ObservableProperty]
        private string _statusText = "待機中";

        /// <summary>一度でもスキャンを完了したか（空状態表示の判定に使用）。</summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsEmptyStateVisible))]
        private bool _hasScanned;

        /// <summary>設定された AIMP 実行ファイルが存在し再生可能か。</summary>
        [ObservableProperty]
        private bool _isAimpAvailable;

        /// <summary>条件に該当したフォルダーの一覧（表示用）。</summary>
        public ObservableCollection<FolderScanResult> Results { get; } = new();

        /// <summary>空状態メッセージを表示すべきか。</summary>
        public bool IsEmptyStateVisible
        {
            get
            {
                return HasScanned && !IsScanning && Results.Count == 0;
            }
        }

        /// <summary>
        /// コンストラクター。
        /// </summary>
        /// <param name="scanner">フォルダースキャンサービス。</param>
        /// <param name="remainingTimeCalculator">残り時間算出サービス。</param>
        /// <param name="settingsService">設定永続化サービス。</param>
        /// <param name="aimpLauncher">AIMP 起動サービス。</param>
        public MainViewModel(
            IMusicFolderScanner scanner,
            RemainingTimeCalculator remainingTimeCalculator,
            ISettingsService settingsService,
            IAimpLauncher aimpLauncher)
        {
            _scanner = scanner;
            _remainingTimeCalculator = remainingTimeCalculator;
            _settingsService = settingsService;
            _aimpLauncher = aimpLauncher;

            AppSettings settings = _settingsService.Load();
            RootFolderPath = settings.LastRootFolderPath ?? string.Empty;

            Results.CollectionChanged += (_, _) => OnPropertyChanged(nameof(IsEmptyStateVisible));

            RefreshAimpAvailability();
            UpdateTargetTimeFromDuration();
        }

        /// <summary>所要時間の分数入力が変化したら目標時刻の表示を更新する。</summary>
        /// <param name="value">変更後の分数入力テキスト。</param>
        partial void OnDurationMinutesTextChanged(string value)
        {
            UpdateTargetTimeFromDuration();
        }

        /// <summary>所要時間モードに切り替わったら目標時刻の表示を更新する。</summary>
        /// <param name="value">変更後のモード（true は所要時間モード）。</param>
        partial void OnIsDurationModeChanged(bool value)
        {
            if (value)
            {
                UpdateTargetTimeFromDuration();
            }
        }

        /// <summary>
        /// 所要時間モードの分数入力から目標時刻（現在時刻 + 分数）を算出し、目標時刻欄に表示する。
        /// 分数が不正な場合は何もしない。
        /// </summary>
        private void UpdateTargetTimeFromDuration()
        {
            if (!IsDurationMode || !int.TryParse(DurationMinutesText, out int minutes))
            {
                return;
            }

            TimeOnly? targetTime = _remainingTimeCalculator.TargetTimeFromDurationMinutes(minutes);

            if (targetTime != null)
            {
                TargetTimeText = targetTime.Value.ToString("HH:mm");
            }
        }

        /// <summary>
        /// AIMP 実行ファイルの存在状態を再評価する（設定変更後にも呼び出す）。
        /// </summary>
        public void RefreshAimpAvailability()
        {
            AppSettings settings = _settingsService.Load();
            IsAimpAvailable = _aimpLauncher.CanLaunch(settings.AimpExecutablePath);
        }

        /// <summary>
        /// ルートフォルダー選択ダイアログを開く。
        /// </summary>
        [RelayCommand]
        private void BrowseRootFolder()
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog
            {
                Title = "ルートフォルダーを選択",
            };

            if (Directory.Exists(RootFolderPath))
            {
                dialog.InitialDirectory = RootFolderPath;
            }

            if (dialog.ShowDialog() == true)
            {
                RootFolderPath = dialog.FolderName;
            }
        }

        /// <summary>
        /// スキャンを実行し、残り時間に収まるフォルダーで一覧を更新する。
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanStartScan))]
        private async Task StartScanAsync()
        {
            if (!Directory.Exists(RootFolderPath))
            {
                ShowInputError("ルートフォルダーが存在しません。フォルダーを選択してください。");
                return;
            }

            TimeSpan? remaining = CalculateRemainingTime();

            if (remaining == null)
            {
                return;
            }

            IsScanning = true;
            StatusText = "スキャン中...";
            ScannedCount = 0;
            MatchedCount = 0;
            ExcludedCount = 0;
            Results.Clear();

            try
            {
                var progress = new Progress<ScanProgress>(p =>
                {
                    ScannedCount = p.ScannedCount;
                    ExcludedCount = p.ExcludedCount;
                });

                FolderScanOutcome outcome =
                    await _scanner.ScanAsync(RootFolderPath, progress, CancellationToken.None);

                // 合計時間 0（対象ファイルなし等）は対象外とし、残り時間以下のみを合計時間降順で表示する
                List<FolderScanResult> matched = outcome.Folders
                    .Where(f => f.TotalDuration > TimeSpan.Zero && f.TotalDuration <= remaining.Value)
                    .OrderByDescending(f => f.TotalDuration)
                    .ToList();

                foreach (FolderScanResult folder in matched)
                {
                    folder.Slack = remaining.Value - folder.TotalDuration;
                    Results.Add(folder);
                }

                ScannedCount = outcome.ScannedCount;
                ExcludedCount = outcome.ExcludedCount;
                MatchedCount = matched.Count;
                StatusText = "スキャン完了";

                SaveLastRootFolder();
            }
            catch (Exception ex)
            {
                StatusText = "スキャン失敗";
                MessageBox.Show(
                    $"スキャン中にエラーが発生しました。\n{ex.Message}",
                    "エラー",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                IsScanning = false;
                HasScanned = true;
            }
        }

        /// <summary>
        /// スキャン開始コマンドが実行可能かを判定する。
        /// </summary>
        /// <returns>実行可能なら true。</returns>
        private bool CanStartScan()
        {
            return !IsScanning;
        }

        /// <summary>
        /// 指定フォルダーを AIMP で再生する。
        /// </summary>
        /// <param name="folder">再生対象のフォルダー。</param>
        [RelayCommand]
        private void PlayFolder(FolderScanResult? folder)
        {
            if (folder == null)
            {
                return;
            }

            AppSettings settings = _settingsService.Load();

            if (!_aimpLauncher.CanLaunch(settings.AimpExecutablePath))
            {
                MessageBox.Show(
                    $"AIMP 実行ファイルが見つかりません。\nパス: {settings.AimpExecutablePath}\n設定画面で AIMP のパスを指定してください。",
                    "AIMP 起動エラー",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            try
            {
                _aimpLauncher.Launch(settings.AimpExecutablePath, folder.AbsolutePath);
                StatusText = $"AIMP で再生: {folder.RelativePath}";
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"AIMP の起動に失敗しました。\n{ex.Message}",
                    "AIMP 起動エラー",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 現在の入力から残り時間を算出する。不正入力の場合は警告を表示して null を返す。
        /// </summary>
        /// <returns>残り時間。不正入力の場合は null。</returns>
        private TimeSpan? CalculateRemainingTime()
        {
            if (IsDurationMode)
            {
                if (!int.TryParse(DurationMinutesText, out int minutes))
                {
                    ShowInputError("所要時間は分単位の数値で入力してください。");
                    return null;
                }

                TimeSpan? remaining = _remainingTimeCalculator.FromDurationMinutes(minutes);

                if (remaining == null)
                {
                    ShowInputError("所要時間は 1 分以上を入力してください。");
                }
                else
                {
                    // 入力からスキャン開始までの経過時間を補正して目標時刻の表示を最新化する
                    UpdateTargetTimeFromDuration();
                }

                return remaining;
            }
            else
            {
                if (!RemainingTimeCalculator.TryParseTargetTime(TargetTimeText, out TimeOnly targetTime))
                {
                    ShowInputError("目標時刻は HH:mm 形式で入力してください。（例: 18:30）");
                    return null;
                }

                TimeSpan? remaining = _remainingTimeCalculator.FromTargetTime(targetTime);

                if (remaining == null)
                {
                    ShowInputError("目標時刻が現在時刻より前です。未来の時刻を入力してください。");
                }

                return remaining;
            }
        }

        /// <summary>
        /// 直前に使用したルートフォルダーパスを設定に保存する。
        /// </summary>
        private void SaveLastRootFolder()
        {
            AppSettings settings = _settingsService.Load();
            settings.LastRootFolderPath = RootFolderPath;
            _settingsService.Save(settings);
        }

        /// <summary>
        /// 入力エラーの警告を表示し、ステータスを更新する。
        /// </summary>
        /// <param name="message">警告メッセージ。</param>
        private static void ShowInputError(string message)
        {
            MessageBox.Show(message, "入力エラー", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
