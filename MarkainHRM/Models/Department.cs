using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OzarkLMS.Models
{
    public class Department
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? Description { get; set; }

        // Manager of the department (Reference to a User)
        public int? ManagerId { get; set; }
        
        [ForeignKey("ManagerId")]
        public User? Manager { get; set; }

        public ICollection<User> Employees { get; set; } = new List<User>();

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
