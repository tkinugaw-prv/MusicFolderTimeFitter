namespace MusicFolderTimeFitter.Services
{
    /// <summary>
    /// 時間指定モードに応じた残り時間を算出するクラス。
    /// </summary>
    public sealed class RemainingTimeCalculator
    {
        /// <summary>現在時刻の取得に使用するプロバイダー（テスト時に差し替え可能）。</summary>
        private readonly TimeProvider _timeProvider;

        /// <summary>
        /// コンストラクター。
        /// </summary>
        /// <param name="timeProvider">現在時刻の取得に使用するプロバイダー。</param>
        public RemainingTimeCalculator(TimeProvider timeProvider)
        {
            _timeProvider = timeProvider;
        }

        /// <summary>
        /// 所要時間モード: 分数から残り時間を算出する。
        /// </summary>
        /// <param name="minutes">聴ける時間（分）。</param>
        /// <returns>残り時間。分数が 0 以下の場合は null（不正入力）。</returns>
        public TimeSpan? FromDurationMinutes(int minutes)
        {
            if (minutes <= 0)
            {
                return null;
            }

            return TimeSpan.FromMinutes(minutes);
        }

        /// <summary>
        /// 目標時刻モード: 目標時刻と現在時刻の差から残り時間を算出する。
        /// 目標時刻が現在時刻より前の場合は不正入力として null を返す（翌日扱いにはしない）。
        /// </summary>
        /// <param name="targetTime">目標時刻（時:分）。</param>
        /// <returns>残り時間。目標時刻が過去の場合は null。</returns>
        public TimeSpan? FromTargetTime(TimeOnly targetTime)
        {
            DateTimeOffset now = _timeProvider.GetLocalNow();
            DateTime targetDateTime = now.Date + targetTime.ToTimeSpan();
            TimeSpan remaining = targetDateTime - now.DateTime;

            if (remaining < TimeSpan.Zero)
            {
                return null;
            }

            return remaining;
        }
    }
}
