using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace JoSystem.Models.Entities
{
    public class Role
    {
        [Key]
        public int Id { get; set; }
        
        [Required]
        public string Name { get; set; }
        
        public string Description { get; set; }
        
        public virtual ICollection<User> Users { get; set; } = new List<User>();
    }
}
