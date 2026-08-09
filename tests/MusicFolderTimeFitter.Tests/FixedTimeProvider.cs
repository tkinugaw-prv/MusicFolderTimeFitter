namespace MusicFolderTimeFitter.Tests
{
    /// <summary>
    /// 現在時刻を固定値で返すテスト用 TimeProvider。
    /// </summary>
    internal sealed class FixedTimeProvider : TimeProvider
    {
        /// <summary>固定の現在時刻（UTC）。</summary>
        private readonly DateTimeOffset _utcNow;

        /// <summary>
        /// コンストラクター。
        /// </summary>
        /// <param name="utcNow">固定する現在時刻（UTC 扱い）。</param>
        public FixedTimeProvider(DateTimeOffset utcNow)
        {
            _utcNow = utcNow;
        }

        /// <inheritdoc />
        public override DateTimeOffset GetUtcNow()
        {
            return _utcNow;
        }

        /// <inheritdoc />
        public override TimeZoneInfo LocalTimeZone
        {
            get
            {
                return TimeZoneInfo.Utc;
            }
        }
    }
}
