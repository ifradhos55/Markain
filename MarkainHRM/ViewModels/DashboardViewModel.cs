using OzarkLMS.Models;

namespace OzarkLMS.ViewModels
{
    public class DashboardViewModel
    {
        public User User { get; set; }
        public List<Course> Courses { get; set; }
        public List<Assignment> UpcomingAssignments { get; set; }
        public List<StickyNote> StickyNotes { get; set; } = new List<StickyNote>();
        public List<DashboardAnnouncement> Announcements { get; set; }
        public List<CalendarEvent> CalendarEvents { get; set; } = new List<CalendarEvent>();
    }
}
