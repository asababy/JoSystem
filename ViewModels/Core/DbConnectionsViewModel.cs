using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using JoSystem.Data;
using JoSystem.Helpers;
using JoSystem.Models.Entities;
using Oracle.ManagedDataAccess.Client;
using System.Data.SqlClient;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace JoSystem.ViewModels.Core
{
    public class DbConnectionsViewModel : INotifyPropertyChanged
    {
        public ObservableCollection<DbConnectionConfig> DbConnections { get; } = new ObservableCollection<DbConnectionConfig>();

        public Dictionary<string, string> ProviderOptions { get; } = new Dictionary<string, string>
        {
            { "Oracle", "Oracle" },
            { "SqlServer", "SQL Server" }
        };

        private string _sqlText;
        public string SqlText
        {
            get => _sqlText;
            set
            {
                if (_sqlText != value)
                {
                    _sqlText = value;
                    Raise(nameof(SqlText));
                }
            }
        }

        private string _jsonResult;
        public string JsonResult
        {
            get => _jsonResult;
            set
            {
                if (_jsonResult != value)
                {
                    _jsonResult = value;
                    Raise(nameof(JsonResult));
                }
            }
        }

        private DbConnectionConfig _selectedConnection;
        public DbConnectionConfig SelectedConnection
        {
            get => _selectedConnection;
            set
            {
                if (_selectedConnection != value)
                {
                    _selectedConnection = value;
                    Raise(nameof(SelectedConnection));
                }
            }
        }

        public ICommand AddCommand { get; }
        public ICommand RemoveCommand { get; }
        public ICommand SaveCommand { get; }
        public ICommand ReloadCommand { get; }
        public ICommand TestCommand { get; }
        public ICommand ExecuteSqlCommand { get; }

        public DbConnectionsViewModel()
        {
            AddCommand = new RelayCommand(AddConnection);
            RemoveCommand = new RelayCommand(RemoveConnection);
            SaveCommand = new RelayCommand(SaveConnections);
            ReloadCommand = new RelayCommand(_ => LoadData());
            TestCommand = new RelayCommand(async p => await TestConnectionAsync(p as DbConnectionConfig ?? SelectedConnection));
            ExecuteSqlCommand = new RelayCommand(async _ => await ExecuteSqlAsync());

            LoadData();
        }

        private void LoadData()
        {
            try
            {
                using var db = new AppDbContext();
                var list = db.DbConnectionConfigs.OrderBy(c => c.Order).ThenBy(c => c.Id).ToList();

                if (!list.Any(c => c.Name == "WMS"))
                {
                    var defaultConn = new DbConnectionConfig
                    {
                        Name = "WMS",
                        Provider = "Oracle",
                        ConnectionString = "User Id=hcaiewms;Password=HcaiEwmsdb0112;Data Source=(DESCRIPTION=(ADDRESS=(PROTOCOL=TCP)(HOST=10.98.9.9)(PORT=1521))(CONNECT_DATA=(SID=hcaiewms)))",
                        Enabled = true,
                        Order = list.Count > 0 ? list.Max(c => c.Order) + 1 : 1
                    };
                    db.DbConnectionConfigs.Add(defaultConn);
                    db.SaveChanges();

                    list.Add(defaultConn);
                    list = list.OrderBy(c => c.Order).ThenBy(c => c.Id).ToList();
                }

                DbConnections.Clear();
                foreach (var conn in list)
                {
                    DbConnections.Add(conn);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载连接配置失败: {ex.Message}");
            }
        }

        private void SaveConnections(object obj)
        {
            try
            {
                using (var db = new AppDbContext())
                {
                    var existing = db.DbConnectionConfigs.ToList();

                    foreach (var conn in DbConnections)
                    {
                        DbConnectionConfig dbConn = null;
                        if (conn.Id != 0)
                        {
                            dbConn = existing.FirstOrDefault(c => c.Id == conn.Id);
                        }

                        if (dbConn != null)
                        {
                            dbConn.Name = conn.Name;
                            dbConn.Provider = conn.Provider;
                            dbConn.ConnectionString = conn.ConnectionString;
                            dbConn.Enabled = conn.Enabled;
                            dbConn.Order = conn.Order;
                        }
                        else
                        {
                            db.DbConnectionConfigs.Add(new DbConnectionConfig
                            {
                                Name = conn.Name,
                                Provider = conn.Provider,
                                ConnectionString = conn.ConnectionString,
                                Enabled = conn.Enabled,
                                Order = conn.Order
                            });
                        }
                    }

                    var toRemove = existing.Where(e => !DbConnections.Any(c => c.Id == e.Id)).ToList();
                    if (toRemove.Count > 0)
                    {
                        db.DbConnectionConfigs.RemoveRange(toRemove);
                    }

                    db.SaveChanges();
                }

                MessageBox.Show("保存成功");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"保存失败: {ex.Message}");
            }
        }

        private void AddConnection(object obj)
        {
            int nextOrder = DbConnections.Count > 0 ? DbConnections.Max(c => c.Order) + 1 : 1;
            var conn = new DbConnectionConfig
            {
                Name = "NewConnection",
                Provider = "Oracle",
                ConnectionString = "",
                Enabled = true,
                Order = nextOrder
            };
            DbConnections.Add(conn);
            SelectedConnection = conn;
        }

        private void RemoveConnection(object obj)
        {
            var target = obj as DbConnectionConfig ?? SelectedConnection;
            if (target == null) return;
            DbConnections.Remove(target);
            if (ReferenceEquals(SelectedConnection, target))
            {
                SelectedConnection = null;
            }
        }

        private static async System.Threading.Tasks.Task TestConnectionAsync(DbConnectionConfig target)
        {
            if (target == null)
            {
                MessageBox.Show("请先选择要测试的连接");
                return;
            }

            var conn = target;
            if (string.IsNullOrWhiteSpace(conn.ConnectionString))
            {
                MessageBox.Show("连接字符串不能为空");
                return;
            }

            try
            {
                switch (conn.Provider?.Trim())
                {
                    case "Oracle":
                        using (var c = new OracleConnection(conn.ConnectionString))
                        {
                            await c.OpenAsync();
                        }
                        break;
                    case "SqlServer":
                        using (var c = new SqlConnection(conn.ConnectionString))
                        {
                            await c.OpenAsync();
                        }
                        break;
                    default:
                        MessageBox.Show($"暂不支持的 Provider: {conn.Provider}");
                        return;
                }

                MessageBox.Show("连接成功");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"连接失败: {ex.Message}");
            }
        }

        private async System.Threading.Tasks.Task ExecuteSqlAsync()
        {
            var conn = SelectedConnection ?? DbConnections.FirstOrDefault(c => c.Enabled) ?? DbConnections.FirstOrDefault();
            if (conn == null)
            {
                MessageBox.Show("请先在上方配置要查询的连接");
                return;
            }

            if (SelectedConnection == null && conn != null)
            {
                SelectedConnection = conn;
            }

            if (string.IsNullOrWhiteSpace(SqlText))
            {
                MessageBox.Show("SQL 语句不能为空");
                return;
            }

            var sql = SqlText.Trim();
            if (!sql.StartsWith("select", StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show("当前仅支持 SELECT 查询");
                return;
            }

            if (string.IsNullOrWhiteSpace(conn.ConnectionString))
            {
                MessageBox.Show("连接字符串不能为空");
                return;
            }

            try
            {
                var rows = new List<Dictionary<string, object>>();
                const int maxRows = 200;

                switch (conn.Provider?.Trim())
                {
                    case "Oracle":
                        using (var c = new OracleConnection(conn.ConnectionString))
                        {
                            await c.OpenAsync();
                            using var cmd = c.CreateCommand();
                            cmd.CommandText = sql;
                            using var reader = await cmd.ExecuteReaderAsync();
                            var fieldCount = reader.FieldCount;
                            while (rows.Count < maxRows && await reader.ReadAsync())
                            {
                                var row = new Dictionary<string, object>(fieldCount, StringComparer.OrdinalIgnoreCase);
                                for (int i = 0; i < fieldCount; i++)
                                {
                                    var name = reader.GetName(i);
                                    object value = reader.IsDBNull(i) ? null : reader.GetValue(i);
                                    if (value is DateTime dt)
                                    {
                                        value = dt.ToString("yyyy-MM-dd HH:mm:ss");
                                    }
                                    row[name] = value;
                                }
                                rows.Add(row);
                            }
                        }
                        break;
                    case "SqlServer":
                        using (var c = new SqlConnection(conn.ConnectionString))
                        {
                            await c.OpenAsync();
                            using var cmd = c.CreateCommand();
                            cmd.CommandText = sql;
                            using var reader = await cmd.ExecuteReaderAsync();
                            var fieldCount = reader.FieldCount;
                            while (rows.Count < maxRows && await reader.ReadAsync())
                            {
                                var row = new Dictionary<string, object>(fieldCount, StringComparer.OrdinalIgnoreCase);
                                for (int i = 0; i < fieldCount; i++)
                                {
                                    var name = reader.GetName(i);
                                    object value = reader.IsDBNull(i) ? null : reader.GetValue(i);
                                    if (value is DateTime dt)
                                    {
                                        value = dt.ToString("yyyy-MM-dd HH:mm:ss");
                                    }
                                    row[name] = value;
                                }
                                rows.Add(row);
                            }
                        }
                        break;
                    default:
                        MessageBox.Show($"暂不支持的 Provider: {conn.Provider}");
                        return;
                }

                JsonResult = JsonConvert.SerializeObject(rows, Formatting.Indented);
            }
            catch (Exception ex)
            {
                JsonResult = string.Empty;
                MessageBox.Show($"执行查询失败: {ex.Message}");
            }
        }

        private void Raise(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }
}
