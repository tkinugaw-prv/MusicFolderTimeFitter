namespace MusicFolderTimeFitter
{
    /// <summary>
    /// アプリケーション全体で使用する定数を定義するクラス。
    /// </summary>
    public static class Const
    {
        /// <summary>設定ファイルを格納するフォルダー名（%APPDATA% 配下）。</summary>
        public const string SETTINGS_FOLDER_NAME = "MusicFolderTimeFitter";

        /// <summary>設定ファイル名。</summary>
        public const string SETTINGS_FILE_NAME = "settings.json";

        /// <summary>AIMP 実行ファイルパスのデフォルト値。</summary>
        public const string DEFAULT_AIMP_EXECUTABLE_PATH = @"D:\AIMP\AIMP.exe";

        /// <summary>タグ値が存在しない場合の表示文字列。</summary>
        public const string UNKNOWN_VALUE_DISPLAY = "(不明)";

        /// <summary>集計対象とする音楽ファイルの拡張子（小文字、ピリオド付き）。</summary>
        public static readonly IReadOnlySet<string> TARGET_EXTENSIONS =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ".flac",
                ".m4a",
            };
    }
}
