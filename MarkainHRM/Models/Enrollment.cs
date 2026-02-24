using System;

namespace OzarkLMS.Models
{
    public class Enrollment
    {
        public int Id { get; set; }
        public int CourseId { get; set; }
        public int StudentId { get; set; } // Map to User.Id
        public DateTime EnrolledDate { get; set; } = DateTime.UtcNow;

        // Navigation Properties
        public Course Course { get; set; }
        public User Student { get; set; }
    }
}
