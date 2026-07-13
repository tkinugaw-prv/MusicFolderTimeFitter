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
        /// 目標時刻の入力文字列をパースする。コロン省略の数字入力（例: 1030 → 10:30）や
        /// 3 桁入力（例: 730 → 07:30）は自動的に補完して解釈する。
        /// </summary>
        /// <param name="input">目標時刻の入力文字列。</param>
        /// <param name="targetTime">パース結果の時刻。</param>
        /// <returns>パースに成功した場合は true。</returns>
        public static bool TryParseTargetTime(string? input, out TimeOnly targetTime)
        {
            targetTime = default;

            string text = input?.Trim() ?? string.Empty;

            if (text.Length == 0)
            {
                return false;
            }

            if (text.All(char.IsAsciiDigit))
            {
                if (text.Length == 3)
                {
                    text = "0" + text;
                }

                if (text.Length == 4)
                {
                    text = text[..2] + ":" + text[2..];
                }
            }

            return TimeOnly.TryParse(text, out targetTime);
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
