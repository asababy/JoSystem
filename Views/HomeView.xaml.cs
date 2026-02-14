using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using JoSystem.ViewModels;

namespace JoSystem.Views
{
    public partial class HomeView : UserControl
    {
        private HomeViewModel VM => (HomeViewModel)DataContext;

        public HomeView()
        {
            InitializeComponent();
        }

        private void Url_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (VM.IsRunning && !string.IsNullOrEmpty(VM.ServerUrl))
            {
                try
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = VM.ServerUrl,
                        UseShellExecute = true
                    });
                }
                catch (System.Exception ex)
                {
                    MessageBox.Show($"无法打开链接: {ex.Message}");
                }
            }
        }
    }
}
