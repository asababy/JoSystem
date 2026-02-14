using JoSystem.ViewModels;
using JoSystem.Views.Core;
using System;
using System.IO;
using System.Windows;

namespace JoSystem
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            try
            {
                // Init DB and Config
                Services.DbService.Init();
                Services.ConfigService.LoadFromDb();
                
                // Initialize Language
                Helpers.LanguageManager.SetLanguage(Services.ConfigService.Current.Language);

                // Show Main Window
                var mainWindow = new MainWindow();
                mainWindow.Show();
            }
            catch (Exception ex)
            {
                string msg = $"Startup Error: {ex.Message}\n{ex.StackTrace}";
                if (ex.InnerException != null)
                {
                    msg += $"\nInner: {ex.InnerException.Message}\n{ex.InnerException.StackTrace}";
                }
                System.IO.File.WriteAllText("startup_error.txt", msg);
                MessageBox.Show(msg, "Critical Error");
                Shutdown(1);
            }
        }
    }
}
