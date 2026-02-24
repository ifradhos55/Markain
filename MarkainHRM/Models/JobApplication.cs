using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OzarkLMS.Models
{
    public class JobApplication
    {
        public int Id { get; set; }

        public int JobPostingId { get; set; }
        public JobPosting? JobPosting { get; set; }

        public int CandidateId { get; set; }
        public Candidate? Candidate { get; set; }

        public DateTime AppliedDate { get; set; } = DateTime.UtcNow;

        [Required]
        [MaxLength(50)]
        public string Status { get; set; } = "New"; // New, Screening, Interview, Offer, Hired, Rejected

        public string? CoverLetter { get; set; }
    }
}
