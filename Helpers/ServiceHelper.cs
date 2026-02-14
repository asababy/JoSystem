using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.ServiceProcess;
using JoSystem.Services;

namespace JoSystem.Helpers
{
    public static class ServiceHelper
    {
        public const string ServiceName = "JoSystemService";
        public const string DisplayName = "JoSystem Service";
        public const string Description = "A background file server service managed by JoSystem.";

        public static bool IsAdmin()
        {
            using var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }

        public static bool IsServiceInstalled()
        {
            return ServiceController.GetServices().Any(s => s.ServiceName == ServiceName);
        }

        public static ServiceControllerStatus GetServiceStatus()
        {
            if (!IsServiceInstalled()) return ServiceControllerStatus.Stopped;
            using var sc = new ServiceController(ServiceName);
            return sc.Status;
        }

        public static void InstallService()
        {
            if (IsServiceInstalled()) return;

            string exePath = Environment.ProcessPath;
            // 重要：binPath 的值必须包含在转义的双引号内，因为路径可能包含空格
            // sc create 命令的 binPath 参数格式非常严格
            string binPath = $"\\\"{exePath}\\\" --service";
            
            string createArgs = $"create {ServiceName} binPath= \"{binPath}\" start= auto DisplayName= \"{DisplayName}\"";
            RunScCommand(createArgs);
            RunScCommand($"description {ServiceName} \"{Description}\"");
        }

        public static void UninstallService()
        {
            if (!IsServiceInstalled()) return;

            StopService();
            RunScCommand($"delete {ServiceName}");
        }

        public static void StartService()
        {
            if (!IsServiceInstalled()) return;
            try
            {
                using var sc = new ServiceController(ServiceName);
                if (sc.Status != ServiceControllerStatus.Running && sc.Status != ServiceControllerStatus.StartPending)
                {
                    sc.Start();
                    sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(10));
                }
            }
            catch
            {
                // 如果直接启动失败（可能无权限），尝试通过 sc.exe 启动（触发 UAC）
                RunScCommand($"start {ServiceName}");
            }
        }

        public static void StopService()
        {
            if (!IsServiceInstalled()) return;
            try
            {
                using var sc = new ServiceController(ServiceName);
                if (sc.Status != ServiceControllerStatus.Stopped && sc.Status != ServiceControllerStatus.StopPending)
                {
                    sc.Stop();
                    sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(10));
                }
            }
            catch
            {
                // 如果直接停止失败，尝试通过 sc.exe 停止（触发 UAC）
                RunScCommand($"stop {ServiceName}");
            }
        }

        private static void RunScCommand(string args)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "sc.exe",
                Arguments = args,
                Verb = "runas", // Request elevation if not already admin
                UseShellExecute = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };

            using var process = Process.Start(startInfo);
            process?.WaitForExit();
        }

        public static bool IsRunningAsService()
        {
            // Simple check: if --service is in command line args
            return Environment.GetCommandLineArgs().Contains("--service");
        }
    }
}
