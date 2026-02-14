using JoSystem.Services;
using JoSystem.ViewModels.Core;
using System.Windows.Controls;

namespace JoSystem.Views.Core
{
    /// <summary>
    /// LogView.xaml 的交互逻辑
    /// </summary>
    public partial class LogView : UserControl
    {
        public LogView()
        {
            InitializeComponent();
            DataContext = new LogViewModel();
        }
    }
}
