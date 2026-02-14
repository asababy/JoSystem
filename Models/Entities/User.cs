using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace JoSystem.Models.Entities
{
    public class User
    {
        [Key]
        public int Id { get; set; }
        
        [Required]
        public string Username { get; set; }
        
        [Required]
        public string PasswordHash { get; set; }
        
        public bool IsAdmin { get; set; }

        public virtual ICollection<Role> Roles { get; set; } = new List<Role>();
    }
}
