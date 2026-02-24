using System;
using System.ComponentModel.DataAnnotations;

namespace OzarkLMS.Models
{
    public class Assignment
    {
        public int Id { get; set; }

        public int CourseId { get; set; } // FK
        public Course? Course { get; set; } // Navigation Property

        [Required]
        public string Title { get; set; } = string.Empty;

        public DateTime DueDate { get; set; }

        public string Type { get; set; } = "assignment"; // assignment, quiz
        
        public string? Description { get; set; } // Instructor instructions
        
        public string SubmissionType { get; set; } = "File"; // File, Text, Both
        
        public string? AttachmentUrl { get; set; } // Instructor uploaded file

        public int Points { get; set; } = 100;

        public string MaxAttempts { get; set; } = "Unlimited";
        
        public List<Question> Questions { get; set; } = new List<Question>();
    }
}
