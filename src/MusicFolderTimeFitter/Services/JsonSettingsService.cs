using System.IO;
using System.Text.Json;
using MusicFolderTimeFitter.Models;

namespace MusicFolderTimeFitter.Services
{
    /// <summary>
    /// %APPDATA% 配下の JSON ファイルにアプリケーション設定を永続化するクラス。
    /// </summary>
    public sealed class JsonSettingsService : ISettingsService
    {
        /// <summary>JSON シリアライズオプション（整形出力）。</summary>
        private static readonly JsonSerializerOptions SERIALIZER_OPTIONS = new()
        {
            WriteIndented = true,
        };

        /// <summary>設定ファイルの絶対パス。</summary>
        private readonly string _settingsFilePath;

        /// <summary>
        /// コンストラクター。既定の保存先（%APPDATA%）を使用する。
        /// </summary>
        public JsonSettingsService()
            : this(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                Const.SETTINGS_FOLDER_NAME,
                Const.SETTINGS_FILE_NAME))
        {
        }

        /// <summary>
        /// コンストラクター。保存先パスを指定する（テスト用）。
        /// </summary>
        /// <param name="settingsFilePath">設定ファイルの絶対パス。</param>
        public JsonSettingsService(string settingsFilePath)
        {
            _settingsFilePath = settingsFilePath;
        }

        /// <inheritdoc />
        public AppSettings Load()
        {
            try
            {
                if (!File.Exists(_settingsFilePath))
                {
                    return new AppSettings();
                }

                string json = File.ReadAllText(_settingsFilePath);
                AppSettings? settings = JsonSerializer.Deserialize<AppSettings>(json);

                return settings ?? new AppSettings();
            }
            catch (Exception)
            {
                // 設定ファイルが壊れている場合はデフォルト値で継続する
                return new AppSettings();
            }
        }

        /// <inheritdoc />
        public void Save(AppSettings settings)
        {
            string? directory = Path.GetDirectoryName(_settingsFilePath);

            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            string json = JsonSerializer.Serialize(settings, SERIALIZER_OPTIONS);
            File.WriteAllText(_settingsFilePath, json);
        }
    }
}
