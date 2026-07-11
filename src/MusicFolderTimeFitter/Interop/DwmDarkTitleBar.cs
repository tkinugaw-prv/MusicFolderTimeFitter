using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace MusicFolderTimeFitter.Interop
{
    /// <summary>
    /// DWM API でウィンドウの OS タイトルバーをアプリのダークテーマに合わせて着色するヘルパー。
    /// 属性が未サポートの環境（Windows 10 など）では黙って標準の外観のままにする。
    /// </summary>
    internal static class DwmDarkTitleBar
    {
        /// <summary>ダークモードのタイトルバーを有効化する属性。</summary>
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

        /// <summary>タイトルバーの背景色を指定する属性（Windows 11 以降）。</summary>
        private const int DWMWA_CAPTION_COLOR = 35;

        /// <summary>タイトルバーの文字色を指定する属性（Windows 11 以降）。</summary>
        private const int DWMWA_TEXT_COLOR = 36;

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

        /// <summary>
        /// 指定ウィンドウのタイトルバーにダークテーマの配色を適用する。
        /// HWND 確定後（OnSourceInitialized 以降）に呼び出すこと。
        /// </summary>
        /// <param name="window">適用対象のウィンドウ。</param>
        public static void Apply(Window window)
        {
            var hwnd = new WindowInteropHelper(window).Handle;
            if (hwnd == IntPtr.Zero)
            {
                return;
            }

            // 各属性は独立に設定し、一部が未サポートでも他は適用する。
            SetAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, 1);
            SetAttribute(hwnd, DWMWA_CAPTION_COLOR, ToColorRef(FindColor("BgOuterColor", Color.FromRgb(0x1A, 0x1D, 0x21))));
            SetAttribute(hwnd, DWMWA_TEXT_COLOR, ToColorRef(FindColor("TextPrimaryColor", Color.FromRgb(0xE4, 0xE5, 0xE8))));
        }

        /// <summary>
        /// 属性を設定する。失敗（未サポート環境での E_INVALIDARG など）は無視する。
        /// </summary>
        private static void SetAttribute(IntPtr hwnd, int attribute, int value)
        {
            _ = DwmSetWindowAttribute(hwnd, attribute, ref value, sizeof(int));
        }

        /// <summary>
        /// テーマリソースから色を取得する。見つからない場合は既定値を返す。
        /// </summary>
        private static Color FindColor(string resourceKey, Color fallback)
        {
            return Application.Current?.TryFindResource(resourceKey) is Color color ? color : fallback;
        }

        /// <summary>
        /// WPF の色を Win32 の COLORREF（0x00BBGGRR）へ変換する。
        /// </summary>
        private static int ToColorRef(Color color)
        {
            return color.R | (color.G << 8) | (color.B << 16);
        }
    }
}
