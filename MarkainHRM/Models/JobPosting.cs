using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OzarkLMS.Models
{
    public class JobPosting
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(200)]
        public string Title { get; set; } = string.Empty;

        [Required]
        public string Description { get; set; } = string.Empty;

        public string? Requirements { get; set; }

        [Required]
        [MaxLength(100)]
        public string Location { get; set; } = "Remote";

        [Required]
        [MaxLength(50)]
        public string EmploymentType { get; set; } = "Full-Time"; // Full-Time, Part-Time, Contract, Intern

        [Column(TypeName = "decimal(18,2)")]
        public decimal? SalaryRangeMin { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? SalaryRangeMax { get; set; }

        // Linked Department
        public int DepartmentId { get; set; }
        public Department? Department { get; set; }

        public DateTime PostedDate { get; set; } = DateTime.UtcNow;
        public DateTime? ClosingDate { get; set; }
        
        public bool IsActive { get; set; } = true;

        public ICollection<JobApplication> Applications { get; set; } = new List<JobApplication>();
    }
}
