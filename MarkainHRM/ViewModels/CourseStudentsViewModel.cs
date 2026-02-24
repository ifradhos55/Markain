using OzarkLMS.Models;
using System.Collections.Generic;

namespace OzarkLMS.ViewModels
{
    public class CourseStudentsViewModel
    {
        public Course Course { get; set; }
        public List<User> EnrolledStudents { get; set; }
        public List<User> AvailableStudents { get; set; }
    }
}
