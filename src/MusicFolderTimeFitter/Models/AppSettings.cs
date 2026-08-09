namespace MusicFolderTimeFitter.Models
{
    /// <summary>
    /// 永続化対象のアプリケーション設定を表すクラス。
    /// </summary>
    public sealed class AppSettings
    {
        /// <summary>AIMP 実行ファイルのパス。</summary>
        public string AimpExecutablePath { get; set; } = Const.DEFAULT_AIMP_EXECUTABLE_PATH;

        /// <summary>直前に使用したルートフォルダーのパス。</summary>
        public string? LastRootFolderPath { get; set; }

        /// <summary>直前に選択していた時間指定モード（true は所要時間モード、false は目標時刻モード）。</summary>
        public bool IsDurationMode { get; set; } = true;

        /// <summary>直前に入力した所要時間（分）。</summary>
        public int DurationMinutes { get; set; } = Const.DEFAULT_DURATION_MINUTES;

        /// <summary>直前に入力した目標時刻（HH:mm 形式）。</summary>
        public string TargetTime { get; set; } = Const.DEFAULT_TARGET_TIME;
    }
}
