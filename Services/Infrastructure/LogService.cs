using JoSystem.Models.DTOs;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;

namespace JoSystem.Services
{
    public enum LogLevel
    {
        Info = 0,
        Warning = 1,
        Error = 2
    }

    public static class LogService
    {
        private static readonly object _lock = new object();
        private static string _logDir;
        private static bool _initialized = false;
        private static ObservableCollection<string> _items = new ObservableCollection<string>();

        public static ObservableCollection<string> Items
        {
            get
            {
                EnsureInitialized();
                return _items;
            }
        }

        private static void EnsureInitialized()
        {
            if (_initialized) return;
            lock (_lock)
            {
                if (_initialized) return;
                InitializeLogPath();
                LoadRecentLogs();
                _initialized = true;
            }
        }

        private static void InitializeLogPath()
        {
            try
            {
                _logDir = ConfigService.Current.AbsoluteLogDirectory;
                
                if (!Directory.Exists(_logDir))
                {
                    Directory.CreateDirectory(_logDir);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Log path init failed: " + ex.Message);
                _logDir = AppDomain.CurrentDomain.BaseDirectory;
            }
        }

        private static string GetCurrentLogFile()
        {
            return Path.Combine(_logDir, $"log_{DateTime.Now:yyyyMMdd}.log");
        }

        private static IEnumerable<string> GetAllLogFiles()
        {
            if (!Directory.Exists(_logDir)) return Enumerable.Empty<string>();
            return Directory.GetFiles(_logDir, "log_*.log").OrderByDescending(f => f);
        }

        private static void LoadRecentLogs()
        {
            try
            {
                var files = GetAllLogFiles();
                int count = 0;
                int limit = ConfigService.Current.LogPageSize > 0 ? ConfigService.Current.LogPageSize : 100;

                foreach (var file in files)
                {
                    if (!File.Exists(file)) continue;

                    var lines = File.ReadLines(file).Reverse();
                    foreach (var line in lines)
                    {
                        _items.Add(line);
                        count++;
                        if (count >= limit) break;
                    }
                    if (count >= limit) break;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Load logs failed: " + ex.Message);
            }
        }

        public static void WriteDownloadLog(string clientIp, string mac, string filePath, long size, string user = null)
        {
            Write($"IP={clientIp}, MAC={mac}, 下载文件: {filePath}, 大小={size}字节", user, LogLevel.Info);
        }

        public static void Write(string message, string user = null, LogLevel level = LogLevel.Info)
        {
            if ((int)level < ConfigService.Current.MinLogLevel) return;

            EnsureInitialized();
            string userPart = string.IsNullOrEmpty(user) ? "[匿名]" : $"[{user}]";
            string levelPart = $"[{level.ToString().ToUpper()}]";
            string line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {levelPart} {userPart} {message}";

            lock (_lock)
            {
                try
                {
                    File.AppendAllText(GetCurrentLogFile(), line + Environment.NewLine);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to write log to file: {ex.Message}");
                }
            }

            if (Application.Current != null)
            {
                Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                {
                    _items.Insert(0, line);
                    
                    int limit = ConfigService.Current.LogPageSize > 0 ? ConfigService.Current.LogPageSize : 100;
                    if (_items.Count > limit) 
                    {
                        while (_items.Count > limit)
                        {
                            _items.RemoveAt(_items.Count - 1);
                        }
                    }
                }));
            }
        }

        public static void WriteWarning(string message, string user = "System")
        {
            Write(message, user, LogLevel.Warning);
        }

        public static void WriteError(string message)
        {
            Write(message, "System", LogLevel.Error);
        }

        public static void Clear()
        {
            EnsureInitialized();
            lock (_lock)
            {
                var files = GetAllLogFiles();
                foreach (var file in files)
                {
                    try
                    {
                        File.Delete(file);
                    }
                    catch { }
                }
            }

            App.Current.Dispatcher.Invoke(() => _items.Clear());
        }

        public static void OpenLogFolder()
        {
            EnsureInitialized();
            try
            {
                if (Directory.Exists(_logDir))
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = _logDir,
                        UseShellExecute = true,
                        Verb = "open"
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.Message);
            }
        }

        public static ObservableCollection<string> GetLogItems() => Items;

        public static PagedResult<string> GetLogs(string keyword, string level, string source, DateTime? startTime, DateTime? endTime, int pageIndex, int pageSize)
        {
            EnsureInitialized();
            lock (_lock)
            {
                var files = GetAllLogFiles().ToList();
                
                if (startTime.HasValue || endTime.HasValue)
                {
                    files = files.Where(f =>
                    {
                        var fileName = Path.GetFileNameWithoutExtension(f);
                        if (fileName.StartsWith("log_") && fileName.Length == 12)
                        {
                            if (DateTime.TryParseExact(fileName.Substring(4), "yyyyMMdd", null, System.Globalization.DateTimeStyles.None, out DateTime fileDate))
                            {
                                if (startTime.HasValue && fileDate.Date < startTime.Value.Date) return false;
                                if (endTime.HasValue && fileDate.Date > endTime.Value.Date) return false;
                                return true;
                            }
                        }
                        return true;
                    }).ToList();
                }

                var query = files.SelectMany(file => 
                {
                    try
                    {
                        return File.ReadLines(file).Reverse();
                    }
                    catch
                    {
                        return Enumerable.Empty<string>();
                    }
                });

                if (!string.IsNullOrEmpty(keyword))
                {
                    query = query.Where(l => l.Contains(keyword, StringComparison.OrdinalIgnoreCase));
                }

                if (!string.IsNullOrEmpty(level) && !level.Equals("All", StringComparison.OrdinalIgnoreCase) && !level.Equals("全部", StringComparison.OrdinalIgnoreCase))
                {
                    string levelTag = $"[{level.ToUpper()}]";
                    query = query.Where(l => l.Contains(levelTag, StringComparison.OrdinalIgnoreCase));
                }

                if (!string.IsNullOrEmpty(source))
                {
                    query = query.Where(l =>
                    {
                        int firstClose = l.IndexOf(']');
                        if (firstClose == -1) return false;
                        int secondOpen = l.IndexOf('[', firstClose);
                        if (secondOpen == -1) return false;
                        int secondClose = l.IndexOf(']', secondOpen);
                        if (secondClose == -1) return false;
                        int thirdOpen = l.IndexOf('[', secondClose);
                        if (thirdOpen == -1) return false;
                        int thirdClose = l.IndexOf(']', thirdOpen);
                        if (thirdClose == -1) return false;

                        string src = l.Substring(thirdOpen + 1, thirdClose - thirdOpen - 1);
                        return src.Contains(source, StringComparison.OrdinalIgnoreCase);
                    });
                }

                if (startTime.HasValue || endTime.HasValue)
                {
                    query = query.Where(l =>
                    {
                        if (l.Length >= 21 && l[0] == '[' && l[20] == ']')
                        {
                            if (DateTime.TryParse(l.Substring(1, 19), out DateTime t))
                            {
                                if (startTime.HasValue && t < startTime.Value) return false;
                                if (endTime.HasValue && t > endTime.Value) return false;
                                return true;
                            }
                        }
                        return false;
                    });
                }

                var data = query.Skip((pageIndex - 1) * pageSize).Take(pageSize).ToList();
                int total = query.Count(); 

                return new PagedResult<string>
                {
                    Items = data,
                    Total = total,
                    PageIndex = pageIndex,
                    PageSize = pageSize
                };
            }
        }
    }
}
