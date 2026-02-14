using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using Microsoft.EntityFrameworkCore;
using JoSystem.Data;
using JoSystem.Helpers;
using JoSystem.Services;
using JoSystem.Models.Entities;

namespace JoSystem.ViewModels.Core
{
    public class RoleSelectionItem : INotifyPropertyChanged
    {
        public Role Role { get; set; }
        
        private bool _isSelected;
        public bool IsSelected 
        { 
            get => _isSelected; 
            set 
            { 
                if (_isSelected != value)
                {
                    _isSelected = value; 
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
                }
            } 
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }

    public class UserDisplayItem : INotifyPropertyChanged
    {
        public User User { get; set; }
        
        private bool _isSelected;
        public bool IsSelected 
        { 
            get => _isSelected; 
            set 
            { 
                if (_isSelected != value)
                {
                    _isSelected = value; 
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
                }
            } 
        }

        public string RoleNames => string.Join(", ", User.Roles?.Select(r => r.Name) ?? new List<string>());

        public event PropertyChangedEventHandler PropertyChanged;
        
        public void RefreshRoleNames()
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(RoleNames)));
        }
    }

    public class UserManagementViewModel : INotifyPropertyChanged
    {
        // Data Collections
        public ObservableCollection<UserDisplayItem> Users { get; set; } = new ObservableCollection<UserDisplayItem>();
        private List<UserDisplayItem> _allUsersCache = new List<UserDisplayItem>(); // For filtering
        
        public ObservableCollection<RoleSelectionItem> AvailableRoles { get; set; } = new ObservableCollection<RoleSelectionItem>();
        public ObservableCollection<Role> FilterRoles { get; set; } = new ObservableCollection<Role>();

        // Filter Properties
        private string _searchText;
        public string SearchText 
        { 
            get => _searchText; 
            set 
            { 
                _searchText = value; 
                Raise(nameof(SearchText)); 
                ApplyFilters();
            } 
        }

        private Role _selectedFilterRole;
        public Role SelectedFilterRole 
        { 
            get => _selectedFilterRole; 
            set 
            { 
                _selectedFilterRole = value; 
                Raise(nameof(SelectedFilterRole)); 
                ApplyFilters();
            } 
        }

        // Form Properties
        private string _usernameInput;
        public string UsernameInput 
        { 
            get => _usernameInput; 
            set { _usernameInput = value; Raise(nameof(UsernameInput)); } 
        }

        private string _passwordInput;
        public string PasswordInput 
        { 
            get => _passwordInput; 
            set { _passwordInput = value; Raise(nameof(PasswordInput)); } 
        }

        // State Properties
        private UserDisplayItem _selectedUserItem;
        public UserDisplayItem SelectedUserItem 
        { 
            get => _selectedUserItem; 
            set 
            { 
                // Only handle single selection from DataGrid click here
                // Note: Checkbox selection is handled via event subscription
                if (_selectedUserItem != value)
                {
                    _selectedUserItem = value; 
                    Raise(nameof(SelectedUserItem));
                    
                    if (_selectedUserItem != null)
                    {
                        // Clear other selections if single row clicked? 
                        // Or sync checkbox? Let's keep them separate but related.
                        // For simplicity, clicking a row selects it for Edit Mode.
                        // Batch Mode is triggered by Checkboxes.
                        
                        LoadUserForEdit(_selectedUserItem.User);
                    }
                    else
                    {
                        // If nothing selected, and no checkboxes checked, reset to Add Mode
                        if (!Users.Any(u => u.IsSelected))
                        {
                            ResetForm();
                        }
                    }
                }
            } 
        }

        private bool _isEditMode;
        public bool IsEditMode 
        { 
            get => _isEditMode; 
            set { _isEditMode = value; Raise(nameof(IsEditMode)); } 
        }

        private bool _isBatchMode;
        public bool IsBatchMode 
        { 
            get => _isBatchMode; 
            set { _isBatchMode = value; Raise(nameof(IsBatchMode)); } 
        }

        public string FormTitle 
        {
            get
            {
                if (IsBatchMode) return "批量分配角色"; // Batch Role Assignment
                if (IsEditMode) return "编辑用户"; // Edit User
                return "添加用户"; // Add User
            }
        }

        // Commands
        public ICommand AddUserCommand { get; }
        public ICommand UpdateUserCommand { get; }
        public ICommand BatchAssignCommand { get; }
        public ICommand CancelEditCommand { get; }
        public ICommand DeleteUserCommand { get; }
        public ICommand ResetFilterCommand { get; }

        public UserManagementViewModel()
        {
            AddUserCommand = new RelayCommand(AddUser);
            UpdateUserCommand = new RelayCommand(UpdateUser);
            BatchAssignCommand = new RelayCommand(BatchAssignRoles);
            CancelEditCommand = new RelayCommand(CancelEdit);
            DeleteUserCommand = new RelayCommand(DeleteUser);
            ResetFilterCommand = new RelayCommand(ResetFilter);
            
            LoadRoles(); // Load all available roles first
            LoadUsers();
        }

        private void Raise(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
        public event PropertyChangedEventHandler PropertyChanged;

        private void LoadRoles()
        {
            try
            {
                using (var db = new AppDbContext())
                {
                    AvailableRoles.Clear();
                    FilterRoles.Clear();
                    
                    if (db.Database.CanConnect())
                    {
                         var roles = db.Roles.ToList();
                         foreach (var r in roles)
                         {
                             AvailableRoles.Add(new RoleSelectionItem { Role = r, IsSelected = false });
                             FilterRoles.Add(r);
                         }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LoadRoles failed: {ex.Message}");
            }
        }

        private void LoadUsers()
        {
            try
            {
                using (var db = new AppDbContext())
                {
                    // Clear list first to ensure UI update triggers
                    // Or better, create a new list and swap?
                    // ObservableCollection doesn't support AddRange, so loop is fine.
                    // But if we clear and re-add, it might be flickering.
                    // However, the issue might be that db.SaveChanges() in AddUser hasn't fully committed or context issues?
                    // Actually, AddUser uses a NEW context, and LoadUsers uses a NEW context.
                    // So data should be visible.
                    
                    var users = db.Users.Include(u => u.Roles).ToList();
                    
                    // Update cache
                    _allUsersCache.Clear();
                    foreach (var u in users)
                    {
                        var item = new UserDisplayItem { User = u, IsSelected = false };
                        item.PropertyChanged += UserItem_PropertyChanged;
                        _allUsersCache.Add(item);
                    }
                    
                    // Update ObservableCollection on UI Thread if needed, but we are likely on UI thread.
                    // Let's force refresh via ApplyFilters
                    ApplyFilters();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Load users failed: {ex.Message}");
            }
        }

        private void UserItem_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(UserDisplayItem.IsSelected))
            {
                UpdateFormState();
            }
        }

        private void UpdateFormState()
        {
            var selectedItems = _allUsersCache.Where(u => u.IsSelected).ToList();
            
            if (selectedItems.Count > 1)
            {
                // Batch Mode
                IsBatchMode = true;
                IsEditMode = false;
                UsernameInput = "";
                PasswordInput = "";
                // Clear roles selection initially or keep previous? Better clear for safety.
                foreach (var r in AvailableRoles) r.IsSelected = false;
            }
            else if (selectedItems.Count == 1)
            {
                // Edit Mode via Checkbox
                IsBatchMode = false;
                LoadUserForEdit(selectedItems[0].User);
            }
            else
            {
                // No checkbox selection
                // Check if grid row is selected (handled in SelectedUserItem setter usually)
                if (SelectedUserItem == null)
                {
                    ResetForm();
                }
            }
            Raise(nameof(FormTitle));
        }

        private void LoadUserForEdit(User user)
        {
            UsernameInput = user.Username;
            PasswordInput = ""; // Don't show hash
            IsEditMode = true;
            IsBatchMode = false;
            Raise(nameof(FormTitle));

            // Load roles
            try 
            {
                using (var db = new AppDbContext())
                {
                    var dbUser = db.Users.Include(u => u.Roles).FirstOrDefault(u => u.Id == user.Id);
                    if (dbUser != null)
                    {
                        var userRoleIds = dbUser.Roles.Select(r => r.Id).ToList();
                        foreach (var item in AvailableRoles)
                        {
                            item.IsSelected = userRoleIds.Contains(item.Role.Id);
                        }
                    }
                }
            }
            catch (Exception ex) { Console.WriteLine(ex.Message); }
        }

        private void ResetForm()
        {
            UsernameInput = "";
            PasswordInput = "";
            IsEditMode = false;
            IsBatchMode = false;
            foreach (var roleItem in AvailableRoles) roleItem.IsSelected = false;
            Raise(nameof(FormTitle));
        }

        private void ApplyFilters()
        {
            Users.Clear();
            var query = _allUsersCache.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                query = query.Where(u => u.User.Username.Contains(SearchText, StringComparison.OrdinalIgnoreCase));
            }

            if (SelectedFilterRole != null)
            {
                query = query.Where(u => u.User.Roles.Any(r => r.Id == SelectedFilterRole.Id));
            }

            foreach (var item in query)
            {
                Users.Add(item);
            }
        }

        private void ResetFilter(object obj)
        {
            SearchText = "";
            SelectedFilterRole = null;
        }

        private void AddUser(object obj)
        {
            if (string.IsNullOrWhiteSpace(UsernameInput) || string.IsNullOrWhiteSpace(PasswordInput))
            {
                MessageBox.Show("Username and password required");
                return;
            }

            try
            {
                using (var db = new AppDbContext())
                {
                    if (db.Users.Any(u => u.Username == UsernameInput))
                    {
                        MessageBox.Show("User already exists");
                        return;
                    }

                    var newUser = new User
                    {
                        Username = UsernameInput,
                        PasswordHash = DbService.HashPassword(PasswordInput),
                        IsAdmin = false
                    };

                    // Add roles
                    var selectedRoleIds = AvailableRoles.Where(r => r.IsSelected).Select(r => r.Role.Id).ToList();
                    if (selectedRoleIds.Any())
                    {
                        var roles = db.Roles.Where(r => selectedRoleIds.Contains(r.Id)).ToList();
                        foreach (var r in roles) newUser.Roles.Add(r);
                    }

                    db.Users.Add(newUser);
                    db.SaveChanges();

                    MessageBox.Show($"User {UsernameInput} created");
                }

                // Reload MUST happen after DB context is closed to avoid locking issues
                LoadUsers(); 
                ResetForm();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to add user: {ex.Message}");
            }
        }

        private void UpdateUser(object obj)
        {
            User targetUser = null;
            if (IsBatchMode) return; // Should use BatchAssign
            
            // Determine target user
            if (SelectedUserItem != null) targetUser = SelectedUserItem.User;
            else if (_allUsersCache.Count(u => u.IsSelected) == 1) targetUser = _allUsersCache.First(u => u.IsSelected).User;

            if (targetUser == null) return;

            try
            {
                using (var db = new AppDbContext())
                {
                    var user = db.Users.Include(u => u.Roles).FirstOrDefault(u => u.Id == targetUser.Id);
                    if (user != null)
                    {
                        if (!string.IsNullOrWhiteSpace(PasswordInput))
                        {
                            user.PasswordHash = DbService.HashPassword(PasswordInput);
                        }

                        // Update Roles
                        user.Roles.Clear();
                        var selectedRoleIds = AvailableRoles.Where(r => r.IsSelected).Select(r => r.Role.Id).ToList();
                        if (selectedRoleIds.Any())
                        {
                            var roles = db.Roles.Where(r => selectedRoleIds.Contains(r.Id)).ToList();
                            foreach (var r in roles) user.Roles.Add(r);
                        }

                        db.SaveChanges();
                        LoadUsers();
                        ResetForm();
                        MessageBox.Show($"User {user.Username} updated");
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Update failed: {ex.Message}");
            }
        }

        private void BatchAssignRoles(object obj)
        {
            var selectedItems = _allUsersCache.Where(u => u.IsSelected).ToList();
            if (!selectedItems.Any()) return;

            try
            {
                using (var db = new AppDbContext())
                {
                    var userIds = selectedItems.Select(u => u.User.Id).ToList();
                    var users = db.Users.Include(u => u.Roles).Where(u => userIds.Contains(u.Id)).ToList();
                    
                    var selectedRoleIds = AvailableRoles.Where(r => r.IsSelected).Select(r => r.Role.Id).ToList();
                    var rolesToAssign = db.Roles.Where(r => selectedRoleIds.Contains(r.Id)).ToList();

                    foreach (var user in users)
                    {
                        // Replace roles strategy
                        user.Roles.Clear();
                        foreach (var r in rolesToAssign) user.Roles.Add(r);
                    }

                    db.SaveChanges();
                    LoadUsers();
                    ResetForm();
                    MessageBox.Show($"Updated roles for {users.Count} users");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Batch update failed: {ex.Message}");
            }
        }

        private void CancelEdit(object obj)
        {
            ResetForm();
            // Uncheck all
            foreach (var item in _allUsersCache) item.IsSelected = false;
            SelectedUserItem = null;
        }

        private void DeleteUser(object obj)
        {
            // Logic to support deleting selected user via button in row
            if (obj is UserDisplayItem item)
            {
                var user = item.User;
                if (user.Username == "admin")
                {
                    MessageBox.Show("Cannot delete admin");
                    return;
                }

                if (MessageBox.Show($"Delete user {user.Username}?", "Confirm", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    using (var db = new AppDbContext())
                    {
                        var dbUser = db.Users.Find(user.Id);
                        if (dbUser != null)
                        {
                            db.Users.Remove(dbUser);
                            db.SaveChanges();
                            LoadUsers();
                            ResetForm();
                        }
                    }
                }
            }
        }
    }
}
