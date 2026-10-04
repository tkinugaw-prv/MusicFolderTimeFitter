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
        [NotifyPropertyChangedFor(nameof(IsTargetTimeMode))]
        private bool _isDurationMode = true;

        /// <summary>所要時間モードの分数入力テキスト。</summary>
        [ObservableProperty]
        private string _durationMinutesText = Const.DEFAULT_DURATION_MINUTES.ToString();

        /// <summary>目標時刻モードの時刻入力テキスト（HH:mm）。</summary>
        [ObservableProperty]
        private string _targetTimeText = Const.DEFAULT_TARGET_TIME;

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

        /// <summary>結果一覧のフリーワード絞り込み文字列。</summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsFiltering))]
        private string _filterText = string.Empty;

        /// <summary>作曲者プルダウンで「絞り込まない」を表す項目の表示文字列。</summary>
        public const string ALL_COMPOSERS_LABEL = "(すべての作曲者)";

        /// <summary>作曲者プルダウンの選択項目（<see cref="ALL_COMPOSERS_LABEL"/> は絞り込みなし）。</summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsFiltering))]
        private string _selectedComposer = ALL_COMPOSERS_LABEL;

        /// <summary>作曲者プルダウンの選択肢（フリーワードで絞り込まれた結果に現れる作曲者）。</summary>
        [ObservableProperty]
        private IReadOnlyList<string> _composerOptions = [ALL_COMPOSERS_LABEL];

        /// <summary>作曲者の選択肢を再構築中か（ComboBox が選択を null で書き戻すのを無視するため）。</summary>
        private bool _isUpdatingComposerOptions;

        /// <summary>絞り込み後に一覧へ表示しているフォルダー数。</summary>
        [ObservableProperty]
        private int _displayedCount;

        /// <summary>条件に該当したフォルダーの一覧（絞り込み前、合計時間降順）。</summary>
        private readonly List<FolderScanResult> _matchedResults = new();

        /// <summary>条件に該当し、かつ絞り込みに一致したフォルダーの一覧（表示用）。</summary>
        public ObservableCollection<FolderScanResult> Results { get; } = new();

        /// <summary>絞り込みが有効か（フリーワードの入力、または作曲者の選択があるか）。</summary>
        public bool IsFiltering
        {
            get
            {
                return !string.IsNullOrWhiteSpace(FilterText) || SelectedComposer != ALL_COMPOSERS_LABEL;
            }
        }

        /// <summary>空状態に表示するメッセージ（絞り込みで 0 件になった場合は専用の文言）。</summary>
        public string EmptyStateText
        {
            get
            {
                return _matchedResults.Count > 0
                    ? "絞り込み条件に一致するフォルダーがありません"
                    : "条件に一致するフォルダーがありません";
            }
        }

        /// <summary>
        /// 目標時刻モードが選択されているか（<see cref="IsDurationMode"/> の反転）。
        /// 目標時刻ラジオボタンの選択状態を双方向で保持するために使用する。
        /// </summary>
        public bool IsTargetTimeMode
        {
            get
            {
                return !IsDurationMode;
            }

            set
            {
                IsDurationMode = !value;
            }
        }

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
            RestoreTimeInputs(settings);

            Results.CollectionChanged += (_, _) => OnPropertyChanged(nameof(IsEmptyStateVisible));

            RefreshAimpAvailability();
            UpdateTargetTimeFromDuration();
        }

        /// <summary>
        /// 前回終了時の時間指定入力（モード・所要時間・目標時刻）を設定から復元する。
        /// 不正な値が保存されていた場合はデフォルト値にフォールバックする。
        /// </summary>
        /// <param name="settings">読み込み済みの設定。</param>
        private void RestoreTimeInputs(AppSettings settings)
        {
            // モードは最後に設定する（所要時間モードでは目標時刻欄が再計算で上書きされるため）
            DurationMinutesText = settings.DurationMinutes > 0
                ? settings.DurationMinutes.ToString()
                : Const.DEFAULT_DURATION_MINUTES.ToString();

            TargetTimeText =
                RemainingTimeCalculator.TryParseTargetTime(settings.TargetTime, out TimeOnly targetTime)
                    ? targetTime.ToString("HH:mm")
                    : Const.DEFAULT_TARGET_TIME;

            IsDurationMode = settings.IsDurationMode;
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

            // 結果を空にすると選択肢が消えて選択が「すべて」に戻るため、再スキャン後に復元する
            string previousComposer = SelectedComposer;

            _matchedResults.Clear();
            ApplyFilter();

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
                    _matchedResults.Add(folder);
                }

                SelectedComposer = previousComposer;
                ApplyFilter();

                ScannedCount = outcome.ScannedCount;
                ExcludedCount = outcome.ExcludedCount;
                MatchedCount = matched.Count;
                StatusText = "スキャン完了";

                SaveInputSettings();
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

        /// <summary>絞り込み文字列が変化したら表示一覧を更新する。</summary>
        /// <param name="value">変更後の絞り込み文字列。</param>
        partial void OnFilterTextChanged(string value)
        {
            ApplyFilter();
        }

        /// <summary>作曲者の選択が変化したら表示一覧を更新する。</summary>
        /// <param name="value">変更後の選択項目。</param>
        partial void OnSelectedComposerChanged(string value)
        {
            if (!_isUpdatingComposerOptions)
            {
                ApplyFilter();
            }
        }

        /// <summary>
        /// 該当フォルダー一覧にフリーワードと作曲者の絞り込み（AND 条件）を適用し、
        /// 表示用の <see cref="Results"/> を更新する。並び順（合計時間降順）は維持する。
        /// 作曲者の選択肢はフリーワードで絞り込まれた結果に現れる作曲者から作り直す。
        /// </summary>
        private void ApplyFilter()
        {
            string[] terms = FreeWordMatcher.SplitTerms(FilterText);

            List<FolderScanResult> wordMatched =
                _matchedResults.Where(f => FreeWordMatcher.IsMatch(f, terms)).ToList();

            UpdateComposerOptions(wordMatched);

            bool isAllComposers = SelectedComposer == ALL_COMPOSERS_LABEL;

            Results.Clear();

            foreach (FolderScanResult folder in wordMatched)
            {
                if (isAllComposers || folder.Composer == SelectedComposer)
                {
                    Results.Add(folder);
                }
            }

            DisplayedCount = Results.Count;
            OnPropertyChanged(nameof(EmptyStateText));
        }

        /// <summary>
        /// 作曲者プルダウンの選択肢を、フリーワードで絞り込まれたフォルダーの作曲者で作り直す。
        /// 選択中の作曲者が選択肢から消えた場合は「すべての作曲者」に戻す。
        /// </summary>
        /// <param name="wordMatched">フリーワードに一致したフォルダー。</param>
        private void UpdateComposerOptions(IEnumerable<FolderScanResult> wordMatched)
        {
            List<string> composers = wordMatched
                .Select(f => f.Composer)
                .Distinct()
                .Order(StringComparer.CurrentCulture)
                .ToList();

            string selected = composers.Contains(SelectedComposer) ? SelectedComposer : ALL_COMPOSERS_LABEL;

            _isUpdatingComposerOptions = true;

            try
            {
                composers.Insert(0, ALL_COMPOSERS_LABEL);
                ComposerOptions = composers;
                SelectedComposer = selected;

                // 選択値が変わらない場合でも、ItemsSource 差し替えで外れた ComboBox の選択を復元する
                OnPropertyChanged(nameof(SelectedComposer));
            }
            finally
            {
                _isUpdatingComposerOptions = false;
            }
        }

        /// <summary>
        /// フリーワードと作曲者の絞り込みをクリアして全件表示に戻す。
        /// </summary>
        [RelayCommand]
        private void ClearFilter()
        {
            FilterText = string.Empty;
            SelectedComposer = ALL_COMPOSERS_LABEL;
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
        /// 現在の入力内容（ルートフォルダー・時間指定モード・所要時間・目標時刻）を設定に保存する。
        /// パースできない入力は保存対象から除外し、前回の保存値を維持する。
        /// </summary>
        public void SaveInputSettings()
        {
            AppSettings settings = _settingsService.Load();
            settings.LastRootFolderPath = RootFolderPath;
            settings.IsDurationMode = IsDurationMode;

            if (int.TryParse(DurationMinutesText, out int minutes) && minutes > 0)
            {
                settings.DurationMinutes = minutes;
            }

            if (RemainingTimeCalculator.TryParseTargetTime(TargetTimeText, out TimeOnly targetTime))
            {
                settings.TargetTime = targetTime.ToString("HH:mm");
            }

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
