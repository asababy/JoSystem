using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace JoSystem.Models.Entities
{
    public class ServerConfig : INotifyPropertyChanged
    {
        [Key]
        public string Key { get; set; }
        
        private string _value;
        public string Value 
        { 
            get => _value; 
            set 
            {
                if (_value != value)
                {
                    _value = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value)));
                }
            }
        }
        
        private string _description;
        [NotMapped]
        public string Description 
        { 
            get => _description; 
            set 
            {
                if (_description != value)
                {
                    _description = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Description)));
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }
}
