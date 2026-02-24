using System;

namespace OzarkLMS.Models
{
    public class SubmissionAttachment
    {
        public int Id { get; set; }

        public int SubmissionId { get; set; }
        public Submission? Submission { get; set; }

        public string FileName { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty; // Stored path or URL
        
        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    }
}
