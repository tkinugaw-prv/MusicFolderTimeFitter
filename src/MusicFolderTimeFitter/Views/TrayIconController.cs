using System.IO;
using System.Windows;
using WinForms = System.Windows.Forms;

namespace MusicFolderTimeFitter.Views
{
    /// <summary>
    /// タスクトレイアイコンを管理し、ウィンドウの最小化時にトレイへ格納する。
    /// トレイアイコンは格納中のみ表示し、クリックまたはメニューの「開く」で復帰する。
    /// </summary>
    internal sealed class TrayIconController : IDisposable
    {
        /// <summary>管理対象のウィンドウ。</summary>
        private readonly Window _window;

        /// <summary>トレイアイコン本体。生成は1回のみで、表示切替は Visible で行う。</summary>
        private readonly WinForms.NotifyIcon _notifyIcon;

        /// <summary>トレイアイコンに使用するアイコン（Dispose 対象）。</summary>
        private readonly System.Drawing.Icon _icon;

        /// <summary>最小化直前のウィンドウ状態。復帰時に最大化状態を復元するために保持する。</summary>
        private WindowState _lastNonMinimizedState = WindowState.Normal;

        /// <summary>
        /// コンストラクター。トレイアイコンを非表示状態で生成する。
        /// </summary>
        /// <param name="window">管理対象のウィンドウ。</param>
        public TrayIconController(Window window)
        {
            _window = window;
            _icon = LoadAppIcon();

            var contextMenu = new WinForms.ContextMenuStrip();
            contextMenu.Items.Add("開く(&O)", null, (_, _) => RestoreWindow());
            contextMenu.Items.Add(new WinForms.ToolStripSeparator());
            contextMenu.Items.Add("終了(&X)", null, (_, _) => Application.Current.Shutdown());

            _notifyIcon = new WinForms.NotifyIcon
            {
                Icon = _icon,
                Text = "音楽フォルダー時間フィッター",
                Visible = false,
                ContextMenuStrip = contextMenu,
            };
            _notifyIcon.MouseClick += (_, e) =>
            {
                if (e.Button == WinForms.MouseButtons.Left)
                {
                    RestoreWindow();
                }
            };
            _notifyIcon.DoubleClick += (_, _) => RestoreWindow();
        }

        /// <summary>
        /// ウィンドウを非表示にしてトレイへ格納する。
        /// </summary>
        public void HideToTray()
        {
            _window.Hide();
            _notifyIcon.Visible = true;
        }

        /// <summary>
        /// トレイからウィンドウを復帰させ、最小化前の状態で前面に表示する。
        /// </summary>
        public void RestoreWindow()
        {
            _notifyIcon.Visible = false;
            _window.Show();
            _window.WindowState = _lastNonMinimizedState;
            _window.Activate();
        }

        /// <summary>
        /// 最小化以外のウィンドウ状態を記録する。状態変更のたびに呼び出すこと。
        /// </summary>
        /// <param name="state">現在のウィンドウ状態。</param>
        public void RememberWindowState(WindowState state)
        {
            if (state != WindowState.Minimized)
            {
                _lastNonMinimizedState = state;
            }
        }

        /// <summary>
        /// トレイアイコンを破棄する。呼び忘れるとプロセス終了まで幽霊アイコンが残る。
        /// </summary>
        public void Dispose()
        {
            _notifyIcon.Visible = false;
            _notifyIcon.ContextMenuStrip?.Dispose();
            _notifyIcon.Dispose();
            _icon.Dispose();
        }

        /// <summary>
        /// アプリリソースからトレイ用アイコンをロードする。
        /// </summary>
        private static System.Drawing.Icon LoadAppIcon()
        {
            var resource = Application.GetResourceStream(new Uri("pack://application:,,,/Assets/app_icon.ico"))
                ?? throw new FileNotFoundException("アプリアイコンのリソースが見つかりません。", "Assets/app_icon.ico");
            using var stream = resource.Stream;
            return new System.Drawing.Icon(stream);
        }
    }
}
