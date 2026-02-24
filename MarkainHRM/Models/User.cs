using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OzarkLMS.Models
{
    public class User
    {
        public int Id { get; set; }


        [Required]
        public string Username { get; set; } = string.Empty;

        [Required]
        public string Password { get; set; } = string.Empty; // In a real app, hash this!

        [Required]
        public string Role { get; set; } = "student"; // student, instructor, admin, hr_admin, manager, employee

        // HR & Employment Details
        public string? JobTitle { get; set; }
        public string? EmployeeId { get; set; } // Internal ID (e.g. EMP-001)
        public string? EmploymentStatus { get; set; } = "Full-Time"; // Full-Time, Part-Time, Contract
        
        [Column(TypeName = "decimal(18,2)")]
        public decimal Salary { get; set; } = 0.00m;
        
        public DateTime? HireDate { get; set; }
        public DateTime? ProbationEndDate { get; set; }

        // Contact Info
        public string? Phone { get; set; }
        public string? Address { get; set; }
        public string? EmergencyContactName { get; set; }
        public string? EmergencyContactPhone { get; set; }

        // Department Link
        public int? DepartmentId { get; set; }
        [ForeignKey("DepartmentId")]
        public Department? Department { get; set; }

        public string? ProfilePictureUrl { get; set; }
        public int TreeProgress { get; set; } = 0; // 0 to 7

        // Social Hub Extensions
        [MaxLength(150)]
        public string? Bio { get; set; }

        // Soft Delete
        public bool IsDeleted { get; set; } = false;

        public ICollection<Post> Posts { get; set; } = new List<Post>();
        
        [InverseProperty("Follower")]
        public ICollection<Follow> Following { get; set; } = new List<Follow>();

        [InverseProperty("Following")]
        public ICollection<Follow> Followers { get; set; } = new List<Follow>();

        public List<Enrollment> Enrollments { get; set; } = new List<Enrollment>();
        public List<ChatGroupMember> ChatGroups { get; set; } = new List<ChatGroupMember>();
        public ICollection<SharedPost> SharedPosts { get; set; } = new List<SharedPost>();
    }
}
