using OzarkLMS.Models;

namespace OzarkLMS.ViewModels
{
    public class AdminDashboardViewModel
    {
        public List<User> Students { get; set; }
        public List<User> Instructors { get; set; }
    }
}
