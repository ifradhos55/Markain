using OzarkLMS.Models;

namespace OzarkLMS.ViewModels
{
    public class ManagerDashboardViewModel
    {
        public User Manager { get; set; }
        public Department Department { get; set; }
        public List<User> TeamMembers { get; set; }
        
        // Stats
        public int Headcount { get; set; }
        public int PresentToday { get; set; }
        public int PendingLeaveRequests { get; set; }
        public int UpcomingReviews { get; set; }
        public double AverageTeamPerformance { get; set; }

        public List<string> RecentActivities { get; set; } = new List<string>();
    }
}
