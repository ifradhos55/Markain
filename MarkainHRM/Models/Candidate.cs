using System.ComponentModel.DataAnnotations;

namespace OzarkLMS.Models
{
    public class Candidate
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string FirstName { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string LastName { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Phone]
        public string? Phone { get; set; }

        public string? ResumeUrl { get; set; }
        public string? LinkedInUrl { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<JobApplication> Applications { get; set; } = new List<JobApplication>();
    }
}
