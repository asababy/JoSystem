using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Serialization;
using JoSystem.Data;
using JoSystem.Models.Entities;
using JoSystem.Views;
using JoSystem.Views.Core;

namespace JoSystem.Services
{
    public class AppConfig
    {
        public static readonly Dictionary<string, Type> ViewRegistry = new Dictionary<string, Type>
        {
            { "Home", typeof(HomeView) },
            { "Logs", typeof(LogView) },
            { "Settings", typeof(SettingsView) },
            { "Users", typeof(UserManagementView) },
            { "Roles", typeof(RoleManagementView) },
            { "DbConnections", typeof(JoSystem.Views.Core.DbConnectionsView) }
        };

        public string RootDirectory { get; set; } = @"D:\DLFiles";

        public string LogDirectory { get; set; } = @"%LocalAppData%\FileServer\Files";

        [JsonIgnore]
        public string AbsoluteRootDirectory
        {
            get
            {
                if (string.IsNullOrWhiteSpace(RootDirectory))
                    return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DLFiles");

                string expandedPath = Environment.ExpandEnvironmentVariables(RootDirectory);

                if (Path.IsPathRooted(expandedPath))
                    return expandedPath;

                return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, expandedPath);
            }
        }

        [JsonIgnore]
        public string AbsoluteLogDirectory
        {
            get
            {
                if (string.IsNullOrWhiteSpace(LogDirectory))
                    return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");

                string expandedPath = Environment.ExpandEnvironmentVariables(LogDirectory);

                if (Path.IsPathRooted(expandedPath))
                    return expandedPath;

                return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, expandedPath);
            }
        }

        public double MaxUploadSizeGB { get; set; } = 3.0;

        public double MaxDownloadSizeGB { get; set; } = 10.0;

        public int LogPageSize { get; set; } = 10;

        public int MinLogLevel { get; set; } = 0;

        public bool EnableHttps { get; set; } = false;

        public string Language { get; set; } = "zh-CN";

        public int HttpPort { get; set; } = 5000;

        public int HttpsPort { get; set; } = 5001;

        public List<DbConnectionConfig> DbConnections { get; set; } = new List<DbConnectionConfig>();

        public List<string> UnCachedViews { get; set; } = new List<string>();

        public Dictionary<string, string> PreviewFileTypes { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { ".txt", "text/plain; charset=utf-8" },
            { ".log", "text/plain; charset=utf-8" },
            { ".ini", "text/plain; charset=utf-8" },
            { ".md", "text/plain; charset=utf-8" },
            { ".cs", "text/plain; charset=utf-8" },
            { ".js", "text/plain; charset=utf-8" },
            { ".css", "text/plain; charset=utf-8" },
            { ".conf", "text/plain; charset=utf-8" },
            { ".json", "application/json; charset=utf-8" },
            { ".xml", "text/xml; charset=utf-8" },
            { ".html", "text/html; charset=utf-8" },
            { ".htm", "text/html; charset=utf-8" },
            { ".pdf", "application/pdf" },
            { ".jpg", "image/jpeg" },
            { ".jpeg", "image/jpeg" },
            { ".png", "image/png" },
            { ".gif", "image/gif" },
            { ".svg", "image/svg+xml" },
            { ".mp4", "video/mp4" },
            { ".mp3", "audio/mpeg" }
        };

        public List<string> BlacklistExtensions { get; set; } = new List<string>
        {
           ".bat", ".cmd", ".sh"
        };

        public List<string> ProtectedApiPaths { get; set; } = new List<string>
        {
            "/api/delete"
        };

        public bool RunAsService { get; set; } = false;

        public bool EnableSwagger { get; set; } = false;

        public string SwaggerIpWhitelist { get; set; } = "0.0.0.0/0;::/0";

        [JsonIgnore]
        public long MaxUploadSizeBytes => (long)(MaxUploadSizeGB * 1024 * 1024 * 1024);

        [JsonIgnore]
        public long MaxDownloadSizeBytes => (long)(MaxDownloadSizeGB * 1024 * 1024 * 1024);
    }

    public static class ConfigService
    {
        public static AppConfig Current { get; private set; } = new AppConfig();

        static ConfigService()
        {
            Load();
        }

        public static void Load()
        {
            Current = new AppConfig();
            LoadFromDb();
        }

        public static void LoadFromDb()
        {
            try
            {
                string dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "fileserver.db");
                if (!File.Exists(dbPath)) return;

                using (var db = new AppDbContext())
                {
                    if (!db.Database.CanConnect()) return;

                    var configs = db.ServerConfigs.ToList();
                    foreach (var cfg in configs)
                    {
                        switch (cfg.Key)
                        {
                            case "Language":
                                if (!string.IsNullOrWhiteSpace(cfg.Value)) Current.Language = cfg.Value;
                                break;
                            case "RootDirectory":
                                if (!string.IsNullOrWhiteSpace(cfg.Value)) Current.RootDirectory = cfg.Value;
                                break;
                            case "LogDirectory":
                                if (!string.IsNullOrWhiteSpace(cfg.Value)) Current.LogDirectory = cfg.Value;
                                break;
                            case "MaxUploadSizeGB":
                                if (double.TryParse(cfg.Value, out double ul)) Current.MaxUploadSizeGB = ul;
                                break;
                            case "MaxDownloadSizeGB":
                                if (double.TryParse(cfg.Value, out double dl)) Current.MaxDownloadSizeGB = dl;
                                break;
                            case "LogPageSize":
                                if (int.TryParse(cfg.Value, out int ps)) Current.LogPageSize = ps;
                                break;
                            case "EnableHttps":
                                if (bool.TryParse(cfg.Value, out bool https)) Current.EnableHttps = https;
                                break;
                            case "HttpPort":
                                if (int.TryParse(cfg.Value, out int httpPort)) Current.HttpPort = httpPort;
                                break;
                            case "HttpsPort":
                                if (int.TryParse(cfg.Value, out int httpsPort)) Current.HttpsPort = httpsPort;
                                break;
                            case "UnCachedViews":
                                if (!string.IsNullOrWhiteSpace(cfg.Value))
                                {
                                    Current.UnCachedViews = cfg.Value.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries)
                                                                   .Select(s => s.Trim())
                                                                   .ToList();
                                }
                                break;
                            case "BlacklistExtensions":
                                if (!string.IsNullOrWhiteSpace(cfg.Value))
                                {
                                    Current.BlacklistExtensions = cfg.Value.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries)
                                                                           .Select(s => s.Trim())
                                                                           .ToList();
                                }
                                break;
                            case "ProtectedApiPaths":
                                if (!string.IsNullOrWhiteSpace(cfg.Value))
                                {
                                    Current.ProtectedApiPaths = cfg.Value.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries)
                                                                         .Select(s => s.Trim())
                                                                         .ToList();
                                }
                                break;
                            case "RunAsService":
                                if (bool.TryParse(cfg.Value, out bool runAsService)) Current.RunAsService = runAsService;
                                break;
                            case "EnableSwagger":
                                if (bool.TryParse(cfg.Value, out bool enableSwagger)) Current.EnableSwagger = enableSwagger;
                                break;
                            case "SwaggerIpWhitelist":
                                if (!string.IsNullOrWhiteSpace(cfg.Value)) Current.SwaggerIpWhitelist = cfg.Value;
                                break;
                            case "MinLogLevel":
                                if (int.TryParse(cfg.Value, out int minLevel)) Current.MinLogLevel = minLevel;
                                break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                try { LogService.WriteError($"从数据库加载配置失败: {ex.Message}"); } catch { }
            }
        }
    }
}
