using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using JoSystem.Services;
using JoSystem.Views.Core;
using System.Collections.Generic;
using System.Windows.Controls;

namespace JoSystem.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private object _currentView;

        // View Cache Dictionary
        private readonly Dictionary<string, UserControl> _viewCache = new Dictionary<string, UserControl>();

        public SidebarViewModel SidebarViewModel { get; }

        public object CurrentView
        {
            get => _currentView;
            set { _currentView = value; OnPropertyChanged(); }
        }

        public MainViewModel()
        {
            SidebarViewModel = new SidebarViewModel();
            SidebarViewModel.NavigationRequested += OnNavigationRequested;
            
            // Default View
            SidebarViewModel.ActiveViewName = "Home"; // Set initial state in Sidebar
            OnNavigationRequested("Home"); // Trigger initial navigation
        }

        private void OnNavigationRequested(string destination)
        {
            if (!AppConfig.ViewRegistry.ContainsKey(destination)) return;

            // Check if caching is enabled for this view (default is cached, unless in UnCachedViews)
            bool isCached = ConfigService.Current.UnCachedViews == null || !ConfigService.Current.UnCachedViews.Contains(destination);

            if (isCached)
            {
                // Check cache
                if (!_viewCache.ContainsKey(destination))
                {
                    // Create and cache
                    var viewType = AppConfig.ViewRegistry[destination];
                    var view = (UserControl)Activator.CreateInstance(viewType);
                    _viewCache[destination] = view;
                }
                CurrentView = _viewCache[destination];
            }
            else
            {
                // Clear from cache if it exists (to free memory if user toggled off)
                if (_viewCache.ContainsKey(destination))
                {
                    _viewCache.Remove(destination);
                }

                // Create new instance
                var viewType = AppConfig.ViewRegistry[destination];
                CurrentView = Activator.CreateInstance(viewType);
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
