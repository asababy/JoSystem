using System;
using System.Linq;
using System.ServiceProcess;
using System.Windows;

namespace JoSystem
{
    public static class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            if (args.Contains("--service"))
            {
                // 运行为 Windows 服务
                ServiceBase[] servicesToRun =
                [
                    new Helpers.JoSystemWindowsService()
                ];
                ServiceBase.Run(servicesToRun);
            }
            else
            {
                // 运行为 WPF 桌面程序
                var app = new App();
                app.InitializeComponent(); // 必须调用，否则 App.xaml 中的全局资源（如 Styles.xaml）不会被加载
                app.Run();
            }
        }
    }
}
