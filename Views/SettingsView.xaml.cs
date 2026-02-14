using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Controls;
using System.Windows.Input;

namespace JoSystem.Views
{
    /// <summary>
    /// SettingsView.xaml 的交互逻辑
    /// </summary>
    public partial class SettingsView : UserControl
    {
        private int _clickCount = 0;
        private DateTime _lastClickTime = DateTime.MinValue;

        public SettingsView()
        {
            InitializeComponent();
        }

        private void Icon_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var now = DateTime.Now;
            if ((now - _lastClickTime).TotalMilliseconds > 1000) // Reset if more than 1 second between clicks
            {
                _clickCount = 1;
            }
            else
            {
                _clickCount++;
            }
            _lastClickTime = now;

            if (_clickCount >= 5)
            {
                _clickCount = 0; // Reset
                OpenDatabaseLocation();
            }
        }

        private void OpenDatabaseLocation()
        {
            try
            {
                string dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "fileserver.db");
                if (File.Exists(dbPath))
                {
                    // Open folder and select file
                    Process.Start("explorer.exe", $"/select,\"{dbPath}\"");
                }
                else
                {
                    // Fallback to just opening the directory
                    Process.Start("explorer.exe", AppDomain.CurrentDomain.BaseDirectory);
                }
            }
            catch (Exception ex)
            {
                // Should we show error? For hidden feature, maybe just debug log.
                Debug.WriteLine($"Failed to open DB location: {ex.Message}");
            }
        }
    }
}
