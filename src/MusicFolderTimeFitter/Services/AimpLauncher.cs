using System.Diagnostics;
using System.IO;

namespace MusicFolderTimeFitter.Services
{
    /// <summary>
    /// AIMP 実行ファイルへの引数渡しによるプロセス起動で再生を委ねるクラス。
    /// AIMP の外部制御 SDK/DLL は使用しない。
    /// </summary>
    public sealed class AimpLauncher : IAimpLauncher
    {
        /// <inheritdoc />
        public bool CanLaunch(string? aimpExecutablePath)
        {
            return !string.IsNullOrWhiteSpace(aimpExecutablePath) && File.Exists(aimpExecutablePath);
        }

        /// <inheritdoc />
        public void Launch(string aimpExecutablePath, string folderPath)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = aimpExecutablePath,
                Arguments = $"\"{folderPath}\"",
                UseShellExecute = false,
            };

            using (Process.Start(startInfo))
            {
                // 起動のみ行い、プロセスの終了は待たない
            }
        }
    }
}
