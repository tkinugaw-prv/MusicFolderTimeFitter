using System.IO;
using MusicFolderTimeFitter.Models;
using MusicFolderTimeFitter.Services;

namespace MusicFolderTimeFitter.Tests
{
    /// <summary>
    /// <see cref="JsonSettingsService"/> の設定永続化ロジックを検証するテストクラス。
    /// </summary>
    public sealed class JsonSettingsServiceTests : IDisposable
    {
        /// <summary>テスト用一時フォルダー。</summary>
        private readonly string _tempFolder;

        /// <summary>テスト用設定ファイルパス。</summary>
        private readonly string _settingsFilePath;

        /// <summary>
        /// コンストラクター。一時フォルダーを準備する。
        /// </summary>
        public JsonSettingsServiceTests()
        {
            _tempFolder = Path.Combine(Path.GetTempPath(), "JsonSettingsServiceTests_" + Guid.NewGuid().ToString("N"));
            _settingsFilePath = Path.Combine(_tempFolder, "settings.json");
        }

        /// <summary>
        /// テスト後に一時フォルダーを削除する。
        /// </summary>
        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_tempFolder))
                {
                    Directory.Delete(_tempFolder, recursive: true);
                }
            }
            catch (Exception)
            {
                // 一時フォルダーの削除失敗はテスト結果に影響しないため無視する
            }
        }

        /// <summary>
        /// 設定ファイルが存在しない場合、デフォルト値
        /// （AIMP パス = D:\AIMP\AIMP.exe）が返ることを検証する。
        /// </summary>
        [Fact]
        public void Load_ファイルなしの場合はデフォルト値()
        {
            var service = new JsonSettingsService(_settingsFilePath);

            AppSettings settings = service.Load();

            Assert.Equal(Const.DEFAULT_AIMP_EXECUTABLE_PATH, settings.AimpExecutablePath);
            Assert.Null(settings.LastRootFolderPath);
            Assert.True(settings.IsDurationMode);
            Assert.Equal(Const.DEFAULT_DURATION_MINUTES, settings.DurationMinutes);
            Assert.Equal(Const.DEFAULT_TARGET_TIME, settings.TargetTime);
        }

        /// <summary>
        /// 保存した設定が再読込で復元される（ラウンドトリップ）ことを検証する。
        /// </summary>
        [Fact]
        public void SaveとLoad_設定がラウンドトリップされる()
        {
            var service = new JsonSettingsService(_settingsFilePath);
            var settings = new AppSettings
            {
                AimpExecutablePath = @"C:\Tools\AIMP\AIMP.exe",
                LastRootFolderPath = @"D:\Music\Library",
                IsDurationMode = false,
                DurationMinutes = 45,
                TargetTime = "07:15",
            };

            service.Save(settings);
            AppSettings loaded = service.Load();

            Assert.Equal(@"C:\Tools\AIMP\AIMP.exe", loaded.AimpExecutablePath);
            Assert.Equal(@"D:\Music\Library", loaded.LastRootFolderPath);
            Assert.False(loaded.IsDurationMode);
            Assert.Equal(45, loaded.DurationMinutes);
            Assert.Equal("07:15", loaded.TargetTime);
        }

        /// <summary>
        /// 設定ファイルが壊れている（不正な JSON）場合、
        /// 例外を出さずデフォルト値で継続することを検証する。
        /// </summary>
        [Fact]
        public void Load_壊れたJSONの場合はデフォルト値()
        {
            Directory.CreateDirectory(_tempFolder);
            File.WriteAllText(_settingsFilePath, "{ this is not valid json !!");

            var service = new JsonSettingsService(_settingsFilePath);

            AppSettings settings = service.Load();

            Assert.Equal(Const.DEFAULT_AIMP_EXECUTABLE_PATH, settings.AimpExecutablePath);
        }
    }
}
