using System;

namespace OzarkLMS.Models
{
    public class CalendarEvent
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public DateTime Start { get; set; }
        public DateTime? End { get; set; }
        public bool IsFullDay { get; set; } = false;
        public string Color { get; set; } = "blue";
        
        public int? CourseId { get; set; } // Linked to course?
        public Course? Course { get; set; }
        
        public int UserId { get; set; }
        public User? User { get; set; }
    }
}
