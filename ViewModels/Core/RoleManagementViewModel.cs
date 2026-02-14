using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using JoSystem.Data;
using JoSystem.Helpers;
using JoSystem.Models.Entities;

namespace JoSystem.ViewModels.Core
{
    public class RoleManagementViewModel : INotifyPropertyChanged
    {
        public ObservableCollection<Role> Roles { get; set; } = new ObservableCollection<Role>();

        private string _nameInput;
        public string NameInput 
        { 
            get => _nameInput; 
            set { _nameInput = value; Raise(nameof(NameInput)); } 
        }

        private string _descriptionInput;
        public string DescriptionInput 
        { 
            get => _descriptionInput; 
            set { _descriptionInput = value; Raise(nameof(DescriptionInput)); } 
        }

        private Role _selectedRole;
        public Role SelectedRole 
        { 
            get => _selectedRole; 
            set 
            { 
                _selectedRole = value; 
                Raise(nameof(SelectedRole)); 
                
                if (_selectedRole != null)
                {
                    NameInput = _selectedRole.Name;
                    DescriptionInput = _selectedRole.Description;
                    IsEditMode = true;
                }
                else
                {
                    NameInput = "";
                    DescriptionInput = "";
                    IsEditMode = false;
                }
            } 
        }

        private bool _isEditMode;
        public bool IsEditMode 
        { 
            get => _isEditMode; 
            set { _isEditMode = value; Raise(nameof(IsEditMode)); } 
        }

        public ICommand AddRoleCommand { get; }
        public ICommand UpdateRoleCommand { get; }
        public ICommand CancelEditCommand { get; }
        public ICommand DeleteRoleCommand { get; }

        public RoleManagementViewModel()
        {
            AddRoleCommand = new RelayCommand(AddRole);
            UpdateRoleCommand = new RelayCommand(UpdateRole);
            CancelEditCommand = new RelayCommand(CancelEdit);
            DeleteRoleCommand = new RelayCommand(DeleteRole);
            
            LoadRoles();
        }

        private void Raise(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        public event PropertyChangedEventHandler PropertyChanged;

        private void LoadRoles()
        {
            try
            {
                using (var db = new AppDbContext())
                {
                    // Ensure table exists (simple check/creation if needed, though EF EnsureCreated should handle it if DB didn't exist)
                    // In a real app with migrations, this isn't needed. 
                    // Here we assume DB schema is updated or we handle errors.
                    
                    Roles.Clear();
                    foreach (var r in db.Roles.ToList()) Roles.Add(r);
                }
            }
            catch (Exception ex)
            {
                // If table doesn't exist, this will fail. 
                // We might want to try EnsureCreated here if it's a "first run with new version" scenario?
                // But db.Database.EnsureCreated() only creates if DB doesn't exist.
                // Since we added a table, we technically need a migration.
                // For this "demo/prototype" environment, I'll catch and show error.
                MessageBox.Show($"Load roles failed: {ex.Message}");
            }
        }

        private void AddRole(object obj)
        {
            if (string.IsNullOrWhiteSpace(NameInput))
            {
                MessageBox.Show((string)Application.Current.TryFindResource("Lang.Msg.RoleNameEmpty") ?? "Role name cannot be empty");
                return;
            }

            try
            {
                using (var db = new AppDbContext())
                {
                    if (db.Roles.Any(r => r.Name == NameInput))
                    {
                        MessageBox.Show((string)Application.Current.TryFindResource("Lang.Msg.RoleExists") ?? "Role already exists");
                        return;
                    }

                    var newRole = new Role
                    {
                        Name = NameInput,
                        Description = DescriptionInput
                    };

                    db.Roles.Add(newRole);
                    db.SaveChanges();

                    Roles.Add(newRole);
                    
                    var msg = (string)Application.Current.TryFindResource("Lang.Msg.RoleCreated") ?? "Role created";
                    MessageBox.Show(msg);
                    
                    NameInput = "";
                    DescriptionInput = "";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to add role: {ex.Message}");
            }
        }

        private void UpdateRole(object obj)
        {
            if (SelectedRole == null) return;

            try
            {
                using (var db = new AppDbContext())
                {
                    var role = db.Roles.FirstOrDefault(r => r.Id == SelectedRole.Id);
                    if (role != null)
                    {
                        role.Name = NameInput;
                        role.Description = DescriptionInput;
                        db.SaveChanges();

                        // Update UI object
                        SelectedRole.Name = NameInput;
                        SelectedRole.Description = DescriptionInput;
                        // Force refresh if needed or just reload
                        LoadRoles();

                        var msg = (string)Application.Current.TryFindResource("Lang.Msg.RoleUpdated") ?? "Role updated";
                        MessageBox.Show(msg);
                        
                        CancelEdit(null);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Update failed: {ex.Message}");
            }
        }

        private void CancelEdit(object obj)
        {
            SelectedRole = null;
        }

        private void DeleteRole(object obj)
        {
            if (obj is Role role)
            {
                var confirmMsg = (string)Application.Current.TryFindResource("Lang.Msg.DeleteRoleConfirm") ?? $"Delete role {role.Name}?";
                if (MessageBox.Show(confirmMsg, 
                    "Confirm", 
                    MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                {
                    try
                    {
                        using (var db = new AppDbContext())
                        {
                            var dbRole = db.Roles.FirstOrDefault(r => r.Id == role.Id);
                            if (dbRole != null)
                            {
                                db.Roles.Remove(dbRole);
                                db.SaveChanges();
                                Roles.Remove(role);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Delete failed: {ex.Message}");
                    }
                }
            }
        }
    }
}
