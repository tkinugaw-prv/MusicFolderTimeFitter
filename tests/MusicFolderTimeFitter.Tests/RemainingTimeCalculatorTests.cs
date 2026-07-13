using MusicFolderTimeFitter.Services;

namespace MusicFolderTimeFitter.Tests
{
    /// <summary>
    /// <see cref="RemainingTimeCalculator"/> の残り時間算出ロジックを検証するテストクラス。
    /// </summary>
    public sealed class RemainingTimeCalculatorTests
    {
        /// <summary>
        /// 現在時刻を固定値で返すテスト用 TimeProvider。
        /// </summary>
        private sealed class FixedTimeProvider : TimeProvider
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

        /// <summary>現在時刻 14:00 固定の計算機を生成する。</summary>
        /// <returns>テスト用計算機。</returns>
        private static RemainingTimeCalculator CreateCalculatorAt1400()
        {
            var timeProvider = new FixedTimeProvider(
                new DateTimeOffset(2026, 7, 12, 14, 0, 0, TimeSpan.Zero));

            return new RemainingTimeCalculator(timeProvider);
        }

        /// <summary>
        /// 所要時間モード: 正の分数が TimeSpan に変換されることを検証する。
        /// </summary>
        [Fact]
        public void FromDurationMinutes_正の分数は変換される()
        {
            RemainingTimeCalculator calculator = CreateCalculatorAt1400();

            TimeSpan? result = calculator.FromDurationMinutes(90);

            Assert.Equal(TimeSpan.FromMinutes(90), result);
        }

        /// <summary>
        /// 所要時間モード: 0 以下の分数は不正入力として null が返ることを検証する。
        /// </summary>
        [Theory]
        [InlineData(0)]
        [InlineData(-5)]
        public void FromDurationMinutes_ゼロ以下はnull(int minutes)
        {
            RemainingTimeCalculator calculator = CreateCalculatorAt1400();

            TimeSpan? result = calculator.FromDurationMinutes(minutes);

            Assert.Null(result);
        }

        /// <summary>
        /// 目標時刻モード: 未来の時刻との差が残り時間として返ることを検証する。
        /// </summary>
        [Fact]
        public void FromTargetTime_未来の時刻は差分が返る()
        {
            RemainingTimeCalculator calculator = CreateCalculatorAt1400();

            TimeSpan? result = calculator.FromTargetTime(new TimeOnly(15, 30));

            Assert.Equal(TimeSpan.FromMinutes(90), result);
        }

        /// <summary>
        /// 目標時刻モード: 過去の時刻は不正入力として null が返ることを検証する（翌日扱いにはしない）。
        /// </summary>
        [Fact]
        public void FromTargetTime_過去の時刻はnull()
        {
            RemainingTimeCalculator calculator = CreateCalculatorAt1400();

            TimeSpan? result = calculator.FromTargetTime(new TimeOnly(13, 59));

            Assert.Null(result);
        }

        /// <summary>
        /// 目標時刻モード: 現在時刻と同時刻は残り時間 0 として有効であることを検証する。
        /// </summary>
        [Fact]
        public void FromTargetTime_現在時刻と同時刻は残り時間ゼロ()
        {
            RemainingTimeCalculator calculator = CreateCalculatorAt1400();

            TimeSpan? result = calculator.FromTargetTime(new TimeOnly(14, 0));

            Assert.Equal(TimeSpan.Zero, result);
        }

        /// <summary>
        /// 目標時刻パース: コロン省略や 3 桁入力が自動補完されて解釈されることを検証する。
        /// </summary>
        [Theory]
        [InlineData("1030", 10, 30)]
        [InlineData("730", 7, 30)]
        [InlineData("0730", 7, 30)]
        [InlineData("18:30", 18, 30)]
        [InlineData(" 730 ", 7, 30)]
        public void TryParseTargetTime_有効な入力はパースされる(string input, int hour, int minute)
        {
            bool success = RemainingTimeCalculator.TryParseTargetTime(input, out TimeOnly result);

            Assert.True(success);
            Assert.Equal(new TimeOnly(hour, minute), result);
        }

        /// <summary>
        /// 目標時刻パース: 補完しても時刻として解釈できない入力は失敗することを検証する。
        /// </summary>
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("abc")]
        [InlineData("2560")]
        [InlineData("12345")]
        [InlineData("73")]
        public void TryParseTargetTime_不正な入力は失敗する(string? input)
        {
            bool success = RemainingTimeCalculator.TryParseTargetTime(input, out _);

            Assert.False(success);
        }
    }
}
