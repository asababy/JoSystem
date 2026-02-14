using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using JoSystem.Helpers;

namespace JoSystem.ViewModels
{
    public class SidebarViewModel : INotifyPropertyChanged
    {
        private bool _isSidebarExpanded = true;
        private string _activeViewName;

        public event Action<string> NavigationRequested;

        public bool IsSidebarExpanded
        {
            get => _isSidebarExpanded;
            set { _isSidebarExpanded = value; OnPropertyChanged(); }
        }

        public string ActiveViewName
        {
            get => _activeViewName;
            set { _activeViewName = value; OnPropertyChanged(); }
        }

        public ICommand NavigateCommand { get; }
        public ICommand ToggleSidebarCommand { get; }

        public SidebarViewModel()
        {
            NavigateCommand = new RelayCommand(Navigate);
            ToggleSidebarCommand = new RelayCommand(_ => IsSidebarExpanded = !IsSidebarExpanded);
        }

        private void Navigate(object destination)
        {
            string dest = destination?.ToString();
            if (ActiveViewName == dest) return;

            ActiveViewName = dest;
            NavigationRequested?.Invoke(dest);
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
