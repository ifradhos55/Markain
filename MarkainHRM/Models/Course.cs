using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace OzarkLMS.Models
{
    public class Course
    {
        public int Id { get; set; }

        [Required]
        public string Name { get; set; } = string.Empty;

        [Required]
        public string Code { get; set; } = string.Empty;

        public string Term { get; set; } = "Fall 2024";

        public int? InstructorId { get; set; }
        public User? Instructor { get; set; }

        public string Color { get; set; } = "bg-blue-500";

        public string Icon { get; set; } = "💻";

        public List<Assignment> Assignments { get; set; } = new List<Assignment>();
        public List<Module> Modules { get; set; } = new List<Module>();
        public List<Enrollment> Enrollments { get; set; } = new List<Enrollment>();
        public List<Meeting> Meetings { get; set; } = new List<Meeting>();
    }
}
