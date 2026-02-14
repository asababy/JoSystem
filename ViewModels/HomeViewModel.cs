using JoSystem.Helpers;
using JoSystem.Models.DTOs;
using JoSystem.Services;
using JoSystem.Services.Hosting;
using Microsoft.VisualBasic.FileIO;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Win32;

namespace JoSystem.ViewModels
{
    public class HomeViewModel : INotifyPropertyChanged, IDisposable
    {
        #region 常量配置
        private const int BrowserOpenDelay = 1200;
        #endregion

        #region 字段
        private readonly Dispatcher _uiDispatcher;
        private WebServer _server;

        private FileSystemWatcher _watcher;
        private DispatcherTimer _debounceTimer;

        private bool _isStarting;
        private bool _isStopping;
        private DispatcherTimer _serviceStatusTimer;
        #endregion

        #region INotifyPropertyChanged
        public event PropertyChangedEventHandler PropertyChanged;
        private void Raise(string name)
        {
            if (_uiDispatcher.CheckAccess())
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
            else
                _uiDispatcher.Invoke(() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name)));
        }
        #endregion

        #region 构造函数
        public HomeViewModel()
        {
            _uiDispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;

            SelectedDirectory = ConfigService.Current.AbsoluteRootDirectory;
            EnsureDirectory(SelectedDirectory);

            InitWatcher();

            FileSystemItems = new ObservableCollection<FileSystemItem>();
            LoadFiles();

            Status = "服务器已停止";
            CanStart = true;
            CanStop = false;

            InitServiceTimer();
        }

        private void InitServiceTimer()
        {
            _serviceStatusTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(2)
            };
            _serviceStatusTimer.Tick += async (s, e) => await CheckServiceStatusAsync();
            _serviceStatusTimer.Start();
        }

        private Task CheckServiceStatusAsync()
        {
            if (!ConfigService.Current.RunAsService)
            {
                bool currentRunning = _server != null && _server.IsRunning;
                if (currentRunning != IsRunning)
                {
                    IsRunning = currentRunning;
                    if (IsRunning)
                    {
                        Status = "服务器正在运行";
                        var ip = GetLocalIPv4Address();
                        string protocol = ConfigService.Current.EnableHttps ? "https" : "http";
                        int port = ConfigService.Current.EnableHttps ? ConfigService.Current.HttpsPort : ConfigService.Current.HttpPort;
                        ServerUrl = $"{protocol}://{ip}:{port}/";
                        CanStart = false;
                        CanStop = true;
                    }
                    else
                    {
                        Status = "服务器已停止";
                        ServerUrl = "";
                        CanStart = true;
                        CanStop = false;
                    }
                }
                return Task.CompletedTask;
            }

            bool isInstalled = ServiceHelper.IsServiceInstalled();
            if (!isInstalled)
            {
                if (IsRunning && _server == null)
                {
                    IsRunning = false;
                    Status = "服务未安装";
                }
                return Task.CompletedTask;
            }

            var status = ServiceHelper.GetServiceStatus();
            bool isServiceRunning = status == System.ServiceProcess.ServiceControllerStatus.Running;

            if (isServiceRunning != IsRunning || (isServiceRunning && _server == null))
            {
                IsRunning = isServiceRunning;
                if (isServiceRunning)
                {
                    Status = "服务正在运行";
                    var ip = GetLocalIPv4Address();
                    string protocol = ConfigService.Current.EnableHttps ? "https" : "http";
                    int port = ConfigService.Current.EnableHttps ? ConfigService.Current.HttpsPort : ConfigService.Current.HttpPort;
                    ServerUrl = $"{protocol}://{ip}:{port}/";
                    CanStart = false;
                    CanStop = true;
                }
                else
                {
                    Status = "服务已停止";
                    ServerUrl = "";
                    CanStart = true;
                    CanStop = false;
                }
            }
            return Task.CompletedTask;
        }
        #endregion

        #region 属性
        private string _selectedDirectory;
        public string SelectedDirectory
        {
            get => _selectedDirectory;
            set
            {
                if (_selectedDirectory != value)
                {
                    _selectedDirectory = value;
                    Raise(nameof(SelectedDirectory));

                    if (_watcher != null)
                    {
                        _watcher.EnableRaisingEvents = false;
                        _watcher.Path = value;
                        _watcher.EnableRaisingEvents = true;
                    }
                }
            }
        }

        public ObservableCollection<FileSystemItem> FileSystemItems { get; set; }
        public FileSystemItem SelectedItem { get; set; }

        private string _status;
        public string Status { get => _status; set { _status = value; Raise(nameof(Status)); } }

        private string _serverUrl;
        public string ServerUrl { get => _serverUrl; set { _serverUrl = value; Raise(nameof(ServerUrl)); } }

        private bool _isRunning;
        public bool IsRunning
        {
            get => _isRunning;
            set
            {
                _isRunning = value;
                Raise(nameof(IsRunning));
                Raise(nameof(ServerStatus));
                Raise(nameof(IsServiceMode));
                Raise(nameof(IsServiceModeAndRunning));
            }
        }

        public static bool IsServiceMode => ConfigService.Current.RunAsService;
        public bool IsServiceModeAndRunning => IsServiceMode && IsRunning;

        public string ServerStatus => IsRunning ? "Running" : "Stopped";

        private bool _canStart;
        public bool CanStart { get => _canStart; set { _canStart = value; Raise(nameof(CanStart)); } }

        private bool _canStop;
        public bool CanStop { get => _canStop; set { _canStop = value; Raise(nameof(CanStop)); } }

        private string _copyButtonText = "Idle";
        public string CopyButtonText
        {
            get => _copyButtonText;
            set { _copyButtonText = value; Raise(nameof(CopyButtonText)); }
        }
        #endregion

        #region 命令
        public ICommand BrowseCommand => new RelayCommand(_ => Browse());
        public ICommand ResetCommand => new RelayCommand(_ => Reset());
        public ICommand UpCommand => new RelayCommand(_ => GoUp());
        public ICommand DeleteCommand => new RelayCommand(p => Delete(p as string));
        public ICommand CopyUrlCommand => new RelayCommand(_ => CopyUrl());

        public ICommand StartServerCommand => new RelayCommand(async _ =>
        {
            if (!_isStarting) await StartServerAsync();
        }, _ => CanStart && !_isStarting);

        public ICommand StopServerCommand => new RelayCommand(async _ =>
        {
            if (!_isStopping) await StopServerAsync();
        }, _ => CanStop && !_isStopping);
        #endregion

        #region 文件加载
        private void EnsureDirectory(string path)
        {
            try
            {
                if (!Directory.Exists(path))
                    Directory.CreateDirectory(path);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "错误");
                SelectedDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            }
        }

        public void LoadFiles()
        {
            try
            {
                if (!Directory.Exists(SelectedDirectory)) return;

                _uiDispatcher.Invoke(() =>
                {
                    FileSystemItems.Clear();

                    var parent = Directory.GetParent(SelectedDirectory);
                    if (parent != null)
                        FileSystemItems.Add(new FileSystemItem(parent.FullName, ".. 上一级", true));

                    foreach (var d in Directory.GetDirectories(SelectedDirectory).OrderBy(p => Path.GetFileName(p)))
                        FileSystemItems.Add(new FileSystemItem(new DirectoryInfo(d)));

                    foreach (var f in Directory.GetFiles(SelectedDirectory).OrderBy(p => Path.GetFileName(p)))
                        FileSystemItems.Add(new FileSystemItem(new FileInfo(f)));
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载文件失败: {ex.Message}", "警告", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        #endregion

        #region 目录操作
        private void Browse()
        {
            var dlg = new OpenFileDialog
            {
                CheckFileExists = false,
                FileName = "请选择文件夹",
                InitialDirectory = SelectedDirectory
            };

            if (dlg.ShowDialog() == true)
            {
                SelectedDirectory = Path.GetDirectoryName(dlg.FileName) ?? SelectedDirectory;
                LoadFiles();
            }
        }

        private void Reset()
        {
            SelectedDirectory = ConfigService.Current.AbsoluteRootDirectory;
            LoadFiles();
        }

        private void GoUp()
        {
            var parent = Directory.GetParent(SelectedDirectory);
            if (parent == null) return;

            SelectedDirectory = parent.FullName;
            LoadFiles();
        }

        private async void CopyUrl()
        {
            if (string.IsNullOrEmpty(ServerUrl)) return;
            try
            {
                Clipboard.SetText(ServerUrl);

                CopyButtonText = "Copied";

                await Task.Delay(3000);
                CopyButtonText = "Idle";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"复制失败: {ex.Message}");
            }
        }

        public void OpenItem(FileSystemItem item)
        {
            if (item == null) return;

            try
            {
                if (item.IsVirtualParent || item.IsDirectory)
                {
                    SelectedDirectory = item.FullName;
                    LoadFiles();
                    return;
                }

                if (MessageBox.Show($"打开文件？\n{item.Name}", "确认打开",
                        MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = item.FullName,
                        UseShellExecute = true
                    });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"打开项失败: {ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public void Delete(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return;

            try
            {
                if (Directory.Exists(path))
                    FileSystem.DeleteDirectory(path, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
                else if (File.Exists(path))
                    FileSystem.DeleteFile(path, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);

                LoadFiles();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"删除失败: {ex.Message}");
            }
        }
        #endregion

        #region 文件监听（防抖）
        private void InitWatcher()
        {
            _watcher = new FileSystemWatcher(SelectedDirectory)
            {
                EnableRaisingEvents = true,
                NotifyFilter = NotifyFilters.FileName |
                               NotifyFilters.DirectoryName |
                               NotifyFilters.LastWrite
            };

            _debounceTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(300)
            };
            _debounceTimer.Tick += (_, __) =>
            {
                _debounceTimer.Stop();
                LoadFiles();
            };

            _watcher.Created += (_, __) => StartDebounce();
            _watcher.Deleted += (_, __) => StartDebounce();
            _watcher.Renamed += (_, __) => StartDebounce();
            _watcher.Changed += (_, __) => StartDebounce();
        }

        private void StartDebounce()
        {
            if (!_debounceTimer.IsEnabled)
                _debounceTimer.Start();
        }
        #endregion

        #region 服务器控制
        public async Task StartServerAsync()
        {
            if (_isStarting) return;

            _isStarting = true;
            CanStart = false;
            CanStop = false;

            Status = "服务器启动中...";
            ServerUrl = "";

            try
            {
                int httpPort = ConfigService.Current.HttpPort;
                int httpsPort = ConfigService.Current.HttpsPort;
                bool enableHttps = ConfigService.Current.EnableHttps;

                if (IsPortInUse(httpPort))
                {
                    throw new Exception($"HTTP 端口 {httpPort} 已被占用，请在设置中更改端口。");
                }
                if (enableHttps && IsPortInUse(httpsPort))
                {
                    throw new Exception($"HTTPS 端口 {httpsPort} 已被占用，请在设置中更改端口。");
                }

                if (ConfigService.Current.RunAsService)
                {
                    if (!ServiceHelper.IsServiceInstalled())
                    {
                        throw new Exception("服务未安装，请在设置中开启“以服务模式运行”并保存。");
                    }
                    ServiceHelper.StartService();
                }
                else
                {
                    if (_server != null)
                    {
                        await _server.StopAsync();
                        _server = null;
                    }

                    _server = new WebServer(SelectedDirectory);
                    await _server.StartAsync();

                    var ip = GetLocalIPv4Address();
                    string protocol = ConfigService.Current.EnableHttps ? "https" : "http";
                    int port = ConfigService.Current.EnableHttps ? ConfigService.Current.HttpsPort : ConfigService.Current.HttpPort;

                    var url = $"{protocol}://{ip}:{port}/";
                    ServerUrl = url;

                    await Task.Delay(BrowserOpenDelay);
                    OpenBrowser(url);

                    Status = "服务器已启动";
                    IsRunning = true;
                    LogService.Write($"服务器已启动: {url}", "System");
                    CanStop = true;
                }
            }
            catch (Exception ex)
            {
                Status = $"启动失败：{ex.Message}";
                LogService.WriteError($"服务器启动失败: {ex.Message}");
                CanStart = true;
            }
            finally
            {
                _isStarting = false;
                CommandManager.InvalidateRequerySuggested();
            }
        }

        public async Task StopServerAsync()
        {
            if (_isStopping) return;

            _isStopping = true;
            CanStart = false;
            CanStop = false;

            Status = "服务器停止中...";

            try
            {
                if (ConfigService.Current.RunAsService)
                {
                    ServiceHelper.StopService();
                }
                else
                {
                    if (_server != null)
                    {
                        await _server.StopAsync();
                        _server = null;
                    }

                    Status = "服务器已停止";
                    LogService.Write("服务器已停止", "System");
                    ServerUrl = "";
                    IsRunning = false;
                    CanStart = true;
                }
            }
            catch (Exception ex)
            {
                Status = $"停止失败：{ex.Message}";
                LogService.WriteError($"服务器停止失败: {ex.Message}");
                CanStart = true;
            }
            finally
            {
                _isStopping = false;
                CommandManager.InvalidateRequerySuggested();
            }
        }

        private void OpenBrowser(string url)
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            catch { }
        }

        private bool IsPortInUse(int port)
        {
            try
            {
                var ipProperties = System.Net.NetworkInformation.IPGlobalProperties.GetIPGlobalProperties();
                var listeners = ipProperties.GetActiveTcpListeners();
                return listeners.Any(l => l.Port == port);
            }
            catch { return false; }
        }

        private string GetLocalIPv4Address()
        {
            try
            {
                var host = Dns.GetHostEntry(Dns.GetHostName());
                var ip = host.AddressList.FirstOrDefault(a =>
                    a.AddressFamily == AddressFamily.InterNetwork &&
                    !IPAddress.IsLoopback(a) &&
                    !a.ToString().StartsWith("169.254.")
                );
                return ip?.ToString() ?? "localhost";
            }
            catch
            {
                return "localhost";
            }
        }
        #endregion

        #region IDisposable
        public void Dispose()
        {
            _server?.StopAsync();
            _server = null;

            _watcher?.Dispose();
            _debounceTimer?.Stop();
            FileSystemItems?.Clear();
        }
        #endregion
    }
}

