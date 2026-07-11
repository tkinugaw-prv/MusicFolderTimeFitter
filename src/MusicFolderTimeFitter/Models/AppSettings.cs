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
    }
}
