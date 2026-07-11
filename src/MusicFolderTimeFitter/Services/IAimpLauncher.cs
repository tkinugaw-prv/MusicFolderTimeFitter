namespace MusicFolderTimeFitter.Services
{
    /// <summary>
    /// AIMP へのフォルダー再生渡しを行うインターフェイス。
    /// </summary>
    public interface IAimpLauncher
    {
        /// <summary>
        /// 指定した AIMP 実行ファイルで再生を開始できるか（実行ファイルが存在するか）を判定する。
        /// </summary>
        /// <param name="aimpExecutablePath">AIMP 実行ファイルのパス。</param>
        /// <returns>再生可能なら true。</returns>
        bool CanLaunch(string? aimpExecutablePath);

        /// <summary>
        /// AIMP をプロセス起動し、フォルダーの絶対パスを引数として渡す。
        /// </summary>
        /// <param name="aimpExecutablePath">AIMP 実行ファイルのパス。</param>
        /// <param name="folderPath">再生するフォルダーの絶対パス。</param>
        void Launch(string aimpExecutablePath, string folderPath);
    }
}
