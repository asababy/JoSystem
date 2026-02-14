using System;
using System.ServiceProcess;
using System.Threading.Tasks;
using JoSystem.Services;
using JoSystem.Services.Hosting;

namespace JoSystem.Helpers
{
    public class JoSystemWindowsService : ServiceBase
    {
        private WebServer _webServer;

        public JoSystemWindowsService()
        {
            ServiceName = ServiceHelper.ServiceName;
        }

        protected override void OnStart(string[] args)
        {
            try
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                System.IO.Directory.SetCurrentDirectory(baseDir);

                DbService.Init();
                ConfigService.LoadFromDb();

                string rootPath = ConfigService.Current.AbsoluteRootDirectory;
                _webServer = new WebServer(rootPath);

                Task.Run(async () => await _webServer.StartAsync());

                LogService.Write("Windows 服务已启动，Web 服务器运行中...");
            }
            catch (Exception ex)
            {
                string errorMsg = $"Windows 服务启动失败: {ex.Message}\n{ex.StackTrace}";
                try { LogService.WriteError(errorMsg); } catch { }
                throw new Exception(errorMsg);
            }
        }

        protected override void OnStop()
        {
            try
            {
                _webServer?.StopAsync().GetAwaiter().GetResult();
                LogService.Write("Windows 服务已停止");
            }
            catch (Exception ex)
            {
                LogService.WriteError($"Windows 服务停止时出错: {ex.Message}");
            }
        }
    }
}

