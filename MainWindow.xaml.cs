using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Interop;

namespace JoSystem
{
    public partial class MainWindow : Window
    {
        private bool _reallyClose = false;
        private IntPtr _trayIconHandle = IntPtr.Zero;

        private const string AppIconPath = "pack://application:,,,/JoSystem;component/Assets/logo.ico";

        public MainWindow()
        {
            InitializeComponent();
            this.Closing += Window_Closing;

            SetWindowIcon();

            // ★ 放到 Loaded，确保句柄已创建
            this.Loaded += MainWindow_Loaded;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            InitTrayIcon();   // ★ 句柄此时有效
        }

        #region 1. 设置窗口图标
        private void SetWindowIcon()
        {
            try
            {
                this.Icon = new BitmapImage(new Uri(AppIconPath));
            }
            catch
            {
                this.Icon = null;
            }
        }
        #endregion

        #region 2. 托盘图标 Win32 Shell_NotifyIcon 实现
        private const int WM_USER = 0x0400;
        private const int WM_TRAYMESSAGE = WM_USER + 1;

        private const int NIF_MESSAGE = 0x0001;
        private const int NIF_ICON = 0x0002;
        private const int NIF_TIP = 0x0004;
        private const int NIM_ADD = 0x00000000;
        private const int NIM_DELETE = 0x00000002;

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern bool Shell_NotifyIcon(uint dwMessage, ref NOTIFYICONDATA lpdata);

        [DllImport("user32.dll", SetLastError = true)]
        static extern IntPtr LoadImage(IntPtr hInst, string lpszName, uint uType, int cxDesired, int cyDesired, uint fuLoad);

        private const uint IMAGE_ICON = 1;
        private const uint LR_LOADFROMFILE = 0x00000010;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct NOTIFYICONDATA
        {
            public int cbSize;
            public IntPtr hWnd;
            public uint uID;
            public uint uFlags;
            public uint uCallbackMessage;
            public IntPtr hIcon;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string szTip;
        }

        private void InitTrayIcon()
        {
            var hwnd = new WindowInteropHelper(this).Handle;

            HwndSource source = HwndSource.FromHwnd(hwnd);
            source.AddHook(WndProc);

            _trayIconHandle = LoadImage(IntPtr.Zero, ResolveIconPath(), IMAGE_ICON, 0, 0, LR_LOADFROMFILE);

            NOTIFYICONDATA data = new NOTIFYICONDATA();
            data.cbSize = Marshal.SizeOf(data);
            data.hWnd = hwnd;
            data.uID = 1;
            data.uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP;
            data.uCallbackMessage = WM_TRAYMESSAGE;
            data.hIcon = _trayIconHandle;
            data.szTip = "文件服务器已运行";

            Shell_NotifyIcon(NIM_ADD, ref data);
        }

        private string ResolveIconPath()
        {
            if (AppIconPath.StartsWith("pack://"))
            {
                var streamInfo = Application.GetResourceStream(new Uri(AppIconPath));
                var temp = Path.Combine(Path.GetTempPath(), "tray_icon.ico");
                using (var fs = new FileStream(temp, FileMode.Create, FileAccess.Write))
                {
                    streamInfo.Stream.CopyTo(fs);
                }
                return temp;
            }
            return AppIconPath;
        }
        #endregion

        #region 3. 托盘消息处理
        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_TRAYMESSAGE)
            {
                int eventId = lParam.ToInt32();

                switch (eventId)
                {
                    case 0x0203:
                        ShowMainWindow();
                        break;

                    case 0x0202:
                        ShowMainWindow();
                        break;

                    case 0x0205:
                        ShowExitMenu();
                        break;
                }
            }
            return IntPtr.Zero;
        }
        #endregion

        #region 4. 右键菜单
        private void ShowExitMenu()
        {
            var menu = new System.Windows.Controls.ContextMenu();

            var openItem = new System.Windows.Controls.MenuItem { Header = "打开主界面" };
            openItem.Click += (s, e) => ShowMainWindow();

            var exitItem = new System.Windows.Controls.MenuItem { Header = "退出" };
            exitItem.Click += (s, e) => ExitApp();

            menu.Items.Add(openItem);
            menu.Items.Add(exitItem);

            menu.IsOpen = true;
        }
        #endregion

        #region 5. 主窗口控制
        private void ShowMainWindow()
        {
            this.Show();
            this.WindowState = WindowState.Normal;
            this.Activate();
            this.ShowInTaskbar = true;
        }

        private void ExitApp()
        {
            _reallyClose = true;
            RemoveTrayIcon();
            Application.Current.Shutdown();
        }

        private void RemoveTrayIcon()
        {
            var hwnd = new WindowInteropHelper(this).Handle;

            NOTIFYICONDATA data = new NOTIFYICONDATA();
            data.cbSize = Marshal.SizeOf(data);
            data.hWnd = hwnd;
            data.uID = 1;

            Shell_NotifyIcon(NIM_DELETE, ref data);
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            if (!_reallyClose)
            {
                e.Cancel = true;
                this.Hide();
                this.ShowInTaskbar = false;
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            RemoveTrayIcon();
        }
        #endregion
    }
}
