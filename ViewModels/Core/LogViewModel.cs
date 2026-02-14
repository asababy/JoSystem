using JoSystem.Models.DTOs;
using JoSystem.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using System.Windows.Input;
using JoSystem.Helpers;

namespace JoSystem.ViewModels.Core
{
    public class LogViewModel : INotifyPropertyChanged
    {
        public ICollectionView LogItemsView { get; private set; }
        public ICommand ClearCommand { get; }
        public ICommand SearchCommand { get; }
        public ICommand OpenLogFolderCommand { get; }
        public ICommand PrevPageCommand { get; }
        public ICommand NextPageCommand { get; }
        public ICommand FirstPageCommand { get; }
        public ICommand LastPageCommand { get; }
        public ICommand JumpCommand { get; }

        private int _currentPage = 1;
        public int CurrentPage
        {
            get => _currentPage;
            set 
            { 
                _currentPage = value; 
                Raise(nameof(CurrentPage)); 
                Raise(nameof(PageInfo));
                JumpPageText = value.ToString();
            }
        }

        private string _jumpPageText = "1";
        public string JumpPageText
        {
            get => _jumpPageText;
            set { _jumpPageText = value; Raise(nameof(JumpPageText)); }
        }

        private int _totalCount = 0;
        public int TotalCount
        {
            get => _totalCount;
            set { _totalCount = value; Raise(nameof(TotalCount)); Raise(nameof(TotalPages)); Raise(nameof(PageInfo)); }
        }

        public int TotalPages => (int)Math.Ceiling((double)TotalCount / Math.Max(1, ConfigService.Current.LogPageSize));

        public string PageInfo => $"{CurrentPage} / {Math.Max(1, TotalPages)}";

        private bool _isSearchView = true; // 默认开启分页视图模式
        public bool IsSearchView
        {
            get => _isSearchView;
            set { _isSearchView = value; Raise(nameof(IsSearchView)); }
        }

        private string _searchKeyword;
        public string SearchKeyword
        {
            get => _searchKeyword;
            set { _searchKeyword = value; Raise(nameof(SearchKeyword)); }
        }

        private DateTime? _searchStartTime;
        public DateTime? SearchStartTime
        {
            get => _searchStartTime;
            set { _searchStartTime = value; Raise(nameof(SearchStartTime)); }
        }

        private DateTime? _searchEndTime;
        public DateTime? SearchEndTime
        {
            get => _searchEndTime;
            set { _searchEndTime = value; Raise(nameof(SearchEndTime)); }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void Raise(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        public LogViewModel()
        {
            // 初始化时执行搜索以显示第一页
            ExecuteSearch();

            // 搜索命令
            SearchCommand = new RelayCommand(_ =>
            {
                CurrentPage = 1;
                ExecuteSearch();
            });

            // 分页命令
            FirstPageCommand = new RelayCommand(_ =>
            {
                CurrentPage = 1;
                ExecuteSearch();
            }, _ => CurrentPage > 1);

            LastPageCommand = new RelayCommand(_ =>
            {
                CurrentPage = TotalPages;
                ExecuteSearch();
            }, _ => CurrentPage < TotalPages);

            PrevPageCommand = new RelayCommand(_ =>
            {
                if (CurrentPage > 1)
                {
                    CurrentPage--;
                    ExecuteSearch();
                }
            }, _ => CurrentPage > 1);

            NextPageCommand = new RelayCommand(_ =>
            {
                if (CurrentPage < TotalPages)
                {
                    CurrentPage++;
                    ExecuteSearch();
                }
            }, _ => CurrentPage < TotalPages);

            JumpCommand = new RelayCommand(_ =>
            {
                if (int.TryParse(JumpPageText, out int page))
                {
                    if (page < 1) page = 1;
                    if (page > TotalPages) page = TotalPages;
                    CurrentPage = page;
                    ExecuteSearch();
                }
            });

            // 清空日志
            ClearCommand = new RelayCommand(_ =>
            {
                LogService.Clear();
                CurrentPage = 1;
                ExecuteSearch();
            });

            // 打开日志目录
            OpenLogFolderCommand = new RelayCommand(_ =>
            {
                try
                {
                    var logDir = ConfigService.Current.AbsoluteLogDirectory;
                    if (System.IO.Directory.Exists(logDir))
                    {
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = logDir,
                            UseShellExecute = true
                        });
                    }
                }
                catch { }
            });
        }

        private void ExecuteSearch()
        {
            int pageSize = ConfigService.Current.LogPageSize > 0 ? ConfigService.Current.LogPageSize : 10;
            var result = LogService.GetLogs(SearchKeyword, null, null, SearchStartTime, SearchEndTime, CurrentPage, pageSize);

            TotalCount = result.Total;
            LogItemsView = CollectionViewSource.GetDefaultView(result.Items);
            Raise(nameof(LogItemsView));
        }
    }
}
