using MusicFolderTimeFitter.Models;

namespace MusicFolderTimeFitter.Services
{
    /// <summary>
    /// アプリケーション設定の読み書きを行うインターフェイス。
    /// </summary>
    public interface ISettingsService
    {
        /// <summary>
        /// 設定を読み込む。設定ファイルが存在しない／壊れている場合はデフォルト値を返す。
        /// </summary>
        /// <returns>アプリケーション設定。</returns>
        AppSettings Load();

        /// <summary>
        /// 設定を永続化する。
        /// </summary>
        /// <param name="settings">保存するアプリケーション設定。</param>
        void Save(AppSettings settings);
    }
}
