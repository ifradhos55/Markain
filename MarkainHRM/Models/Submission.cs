using System;

namespace OzarkLMS.Models
{
    public class Submission
    {
        public int Id { get; set; }
        
        public int AssignmentId { get; set; }
        public Assignment? Assignment { get; set; }
        
        public int StudentId { get; set; }
        public User? Student { get; set; }
        
        public string Content { get; set; } = string.Empty; // Text submission
        public string? AttachmentUrl { get; set; } // File submission path
        
        public int? Score { get; set; } // Points earned
        public string? Feedback { get; set; } // Instructor feedback
        
        public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;
        
        public List<SubmissionAttachment> Attachments { get; set; } = new List<SubmissionAttachment>();
    }
}
