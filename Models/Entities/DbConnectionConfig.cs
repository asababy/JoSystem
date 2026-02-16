using System.ComponentModel.DataAnnotations;

namespace JoSystem.Models.Entities
{
    public class DbConnectionConfig
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string Name { get; set; }

        [Required]
        public string Provider { get; set; }

        [Required]
        public string ConnectionString { get; set; }

        public bool Enabled { get; set; }

        public int Order { get; set; }
    }
}

