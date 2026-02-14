using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using JoSystem.Data;
using JoSystem.Services;
using JoSystem.Helpers;
using JoSystem.Models.Entities;

namespace JoSystem.ViewModels
{
    public class SettingsViewModel : INotifyPropertyChanged
    {
        public ObservableCollection<ServerConfig> Configs { get; set; } = new ObservableCollection<ServerConfig>();

        public Dictionary<string, string> LanguageOptions { get; } = new Dictionary<string, string>
        {
            { "zh-CN", "中文 (简体)" },
            { "en-US", "English" },
            { "id-ID", "Bahasa Indonesia" }
        };

        public Dictionary<string, string> BooleanOptions { get; } = new Dictionary<string, string>
        {
            { "True", "True" },
            { "False", "False" }
        };

        public bool IsAdmin => ServiceHelper.IsAdmin();

        private string _selectedLanguage;
        public string SelectedLanguage
        {
            get => _selectedLanguage;
            set
            {
                if (_selectedLanguage != value)
                {
                    _selectedLanguage = value;
                    Raise(nameof(SelectedLanguage));
                    
                    // Update Configs collection
                    var langConfig = Configs.FirstOrDefault(c => c.Key == "Language");
                    if (langConfig != null)
                    {
                        langConfig.Value = value;
                    }

                    // Apply immediately for preview
                    LanguageManager.SetLanguage(value);
                    
                    // Refresh Config descriptions
                    RefreshConfigDescriptions();
                }
            }
        }

        private void RefreshConfigDescriptions()
        {
            foreach (var c in Configs)
            {
                try
                {
                    var resourceKey = $"Lang.Config.{c.Key}";
                    var translatedDesc = System.Windows.Application.Current.TryFindResource(resourceKey) as string;
                    if (!string.IsNullOrEmpty(translatedDesc))
                    {
                        c.Description = translatedDesc;
                    }
                }
                catch { /* Ignore */ }
            }
        }

        public class ViewCacheOption : INotifyPropertyChanged
        {
            public string ViewName { get; set; }
            private bool _isUnCached;
            public bool IsUnCached
            {
                get => _isUnCached;
                set
                {
                    if (_isUnCached != value)
                    {
                        _isUnCached = value;
                        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsUnCached)));
                        OnChanged?.Invoke();
                    }
                }
            }
            public Action OnChanged { get; set; }

            public event PropertyChangedEventHandler PropertyChanged;
        }

        public ObservableCollection<ViewCacheOption> ViewCacheOptions { get; set; } = new ObservableCollection<ViewCacheOption>();

        public ICommand SaveConfigCommand { get; }
        public ICommand ReloadConfigCommand { get; }
        public ICommand ResetToDefaultsCommand { get; }

        public SettingsViewModel()
        {
            SaveConfigCommand = new RelayCommand(SaveConfig);
            ReloadConfigCommand = new RelayCommand(LoadData);
            ResetToDefaultsCommand = new RelayCommand(ResetToDefaults);
            
            LoadData(null);
        }

        private void Raise(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        private void UpdateCachedViewsConfig()
        {
            // Do NOT update the Configs collection immediately.
        }

        private void LoadData(object obj)
        {
            try
            {
                using (var db = new AppDbContext())
                {
                    // Ensure default configs exist in DB
                    var existingKeys = db.ServerConfigs.Select(c => c.Key).ToList();
                    var defaults = GetDefaultConfigs();
                    var excludedKeys = new HashSet<string> { "BlacklistExtensions", "MaxDownloadSizeGB", "MaxUploadSizeGB", "RootDirectory" };

                    bool anyAdded = false;
                    foreach (var kvp in defaults)
                    {
                        if (!existingKeys.Contains(kvp.Key))
                        {
                            db.ServerConfigs.Add(new ServerConfig { Key = kvp.Key, Value = kvp.Value.Value, Description = kvp.Value.Desc });
                            anyAdded = true;
                        }
                    }
                    if (anyAdded) db.SaveChanges();

                    Configs.Clear();
                    foreach (var c in db.ServerConfigs.ToList()) 
                    {
                        if (excludedKeys.Contains(c.Key)) continue;
                        // Try to translate description
                        try
                        {
                            var resourceKey = $"Lang.Config.{c.Key}";
                            var translatedDesc = System.Windows.Application.Current.TryFindResource(resourceKey) as string;
                            if (!string.IsNullOrEmpty(translatedDesc))
                            {
                                c.Description = translatedDesc;
                            }
                        }
                        catch { /* Ignore if resource not found */ }
                        Configs.Add(c);
                    }

                    // Sync SelectedLanguage
                    var langConfig = Configs.FirstOrDefault(c => c.Key == "Language");
                    if (langConfig != null)
                    {
                        SelectedLanguage = langConfig.Value;
                    }

                    // Sync CachedViews checkboxes dynamically
                    var cachedConfig = Configs.FirstOrDefault(c => c.Key == "UnCachedViews");
                    var currentUnCachedViews = new List<string>();
                    
                    if (cachedConfig != null)
                    {
                        currentUnCachedViews = cachedConfig.Value.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries).ToList();
                    }
                    else
                    {
                        // Fallback to ConfigService default if not in DB
                        currentUnCachedViews = ConfigService.Current.UnCachedViews ?? new List<string>();
                    }

                    // Populate ViewCacheOptions from ConfigService.ViewRegistry
                    ViewCacheOptions.Clear();
                    foreach (var viewKey in AppConfig.ViewRegistry.Keys)
                    {
                        var isUnCached = currentUnCachedViews.Contains(viewKey);
                        var option = new ViewCacheOption 
                        { 
                            ViewName = viewKey, 
                            IsUnCached = isUnCached,
                            OnChanged = UpdateCachedViewsConfig
                        };
                        ViewCacheOptions.Add(option);
                    }
                }
            }
            catch (Exception ex)
            {
                var msg = (string)Application.Current.TryFindResource("Lang.Msg.LoadFail");
                MessageBox.Show($"{msg}: {ex.Message}");
            }
        }

        private Dictionary<string, (string Value, string Desc)> GetDefaultConfigs()
        {
            // Create a fresh AppConfig to get defaults
            var defaultConfig = new AppConfig();
            
            return new Dictionary<string, (string Value, string Desc)>
            {
                { "Language", (defaultConfig.Language, "界面语言") },
                { "LogDirectory", (defaultConfig.LogDirectory, "日志存放目录") },
                { "LogPageSize", (defaultConfig.LogPageSize.ToString(), "日志每页条数") },
                { "EnableHttps", (defaultConfig.EnableHttps.ToString(), "启用HTTPS") },
                { "HttpPort", (defaultConfig.HttpPort.ToString(), "HTTP端口") },
                { "HttpsPort", (defaultConfig.HttpsPort.ToString(), "HTTPS端口") },
                { "UnCachedViews", (string.Join("|", defaultConfig.UnCachedViews), "不缓存的视图(勾选以禁用缓存)") },
                { "ProtectedApiPaths", (string.Join("|", defaultConfig.ProtectedApiPaths), "受保护API(以|分隔)") },
                { "RunAsService", (defaultConfig.RunAsService.ToString(), "以服务模式运行") },
                { "EnableSwagger", (defaultConfig.EnableSwagger.ToString(), "启用Swagger文档（建议仅开发/内网环境打开）") },
                { "SwaggerIpWhitelist", (defaultConfig.SwaggerIpWhitelist, "Swagger访问IP白名单(支持;分隔, 支持CIDR/IP区间/通配符，如10.1.0.1-10.2.0.23, 10.1.*.5)") },
                { "MinLogLevel", (defaultConfig.MinLogLevel.ToString(), "最小日志级别 (0=Info, 1=Warning, 2=Error)") }
            };
        }

        private void ResetToDefaults(object obj)
        {
            if (MessageBox.Show("确定要恢复默认设置吗？所有更改将丢失。", "确认", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                try
                {
                    using (var db = new AppDbContext())
                    {
                        // Clear existing configs
                        db.ServerConfigs.RemoveRange(db.ServerConfigs);
                        db.SaveChanges();
                    }
                    
                    // Reload will re-populate from defaults (and save to DB if missing)
                    LoadData(null);
                    
                    MessageBox.Show("已恢复默认设置");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"恢复失败: {ex.Message}");
                }
            }
        }

        private void SaveConfig(object obj)
        {
            try
            {
                // Sync CachedViews from Checkboxes to Configs collection BEFORE saving
                var cachedViewsString = string.Join("|", ViewCacheOptions.Where(v => v.IsUnCached).Select(v => v.ViewName));
                var cachedConfig = Configs.FirstOrDefault(c => c.Key == "UnCachedViews");
                if (cachedConfig != null) 
                {
                    cachedConfig.Value = cachedViewsString;
                }

                using (var db = new AppDbContext())
                {
                    foreach (var cfg in Configs)
                    {
                        var dbCfg = db.ServerConfigs.FirstOrDefault(c => c.Key == cfg.Key);
                        if (dbCfg != null)
                        {
                            // 记录配置项更改日志
                            if (dbCfg.Value != cfg.Value)
                            {
                                LogService.Write($"配置项 [{cfg.Key}] 更改: {dbCfg.Value} -> {cfg.Value}", level: LogLevel.Warning);
                            }
                            dbCfg.Value = cfg.Value;
                        }
                        else
                        {
                            db.ServerConfigs.Add(cfg);
                        }
                    }

                    db.SaveChanges();
                }
                
                // 检查服务配置是否改变并应用
                ApplyServiceConfig();
                
                // 立即应用配置
                ConfigService.LoadFromDb();
                var msg = (string)Application.Current.TryFindResource("Lang.Msg.SaveSuccess");
                MessageBox.Show(msg);
            }
            catch (Exception ex)
            {
                var msg = (string)Application.Current.TryFindResource("Lang.Msg.SaveFail");
                MessageBox.Show($"{msg}: {ex.Message}");
            }
        }

        private void ApplyServiceConfig()
        {
            try
            {
                // 如果不是管理员，直接跳过服务相关的配置应用。
                // 界面上已经对非管理员禁用了此选项，此处跳过可避免保存其他配置时产生权限弹窗。
                if (!ServiceHelper.IsAdmin()) return;

                var runAsServiceConfig = Configs.FirstOrDefault(c => c.Key == "RunAsService");
                if (runAsServiceConfig == null) return;

                _ = bool.TryParse(runAsServiceConfig.Value, out bool shouldRunAsService);

                bool isInstalled = ServiceHelper.IsServiceInstalled();

                if (shouldRunAsService)
                {
                    if (!isInstalled)
                    {
                        ServiceHelper.InstallService();
                        // 安装后尝试启动
                        ServiceHelper.StartService();
                    }
                    else
                    {
                        // 已安装则确保启动
                        ServiceHelper.StartService();
                    }
                }
                else
                {
                    // 只要 shouldRunAsService 为 false，无论之前是什么状态，都尝试卸载（如果已安装）
                    if (isInstalled)
                    {
                        ServiceHelper.UninstallService();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"服务配置应用失败: {ex.Message}");
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }
}
