namespace MusicFolderTimeFitter.Models
{
    /// <summary>
    /// 時間指定モードを表す列挙型。
    /// </summary>
    public enum TimeFilterMode
    {
        /// <summary>所要時間モード（今から聴ける時間を分単位で指定）。</summary>
        Duration,

        /// <summary>目標時刻モード（聴き終えたい時刻を指定）。</summary>
        TargetTime,
    }
}
