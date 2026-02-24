using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using OzarkLMS.Data;
using OzarkLMS.Models;
using OzarkLMS.ViewModels;
using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace OzarkLMS.Controllers
{
    public class HomeController : Controller
    {
        private readonly AppDbContext _context;

        public HomeController(AppDbContext context)
        {
            _context = context;
        }

        [Authorize]
        public async Task<IActionResult> Index()
        {
            var username = User.Identity?.Name;
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == username);

            if (user == null)
            {
                await HttpContext.SignOutAsync(Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme);
                return RedirectToAction("Login", "Account");
            }

            var stickyNotes = await _context.StickyNotes.Where(n => n.UserId == user.Id).ToListAsync();
            var announcements = await _context.DashboardAnnouncements.OrderByDescending(a => a.Date).ToListAsync();

            List<Course> courses;
            if (User.IsInRole("admin"))
            {
                courses = await _context.Courses
                    .Include(c => c.Instructor)
                    .Include(c => c.Assignments)
                    .ToListAsync();
            }
            else if (User.IsInRole("instructor"))
            {
                courses = await _context.Courses
                    .Include(c => c.Instructor)
                    .Include(c => c.Assignments)
                    .Where(c => c.InstructorId == user.Id)
                    .ToListAsync();
            }
            else if (User.IsInRole("manager"))
            {
                // Fetch Manager's Department and Team
                var department = await _context.Departments
                    .Include(d => d.Employees)
                    .FirstOrDefaultAsync(d => d.ManagerId == user.Id);

                var teamMembers = department?.Employees.ToList() ?? new List<User>();

                // Mocking data for missing modules (Phase 2-4)
                var managerViewModel = new ManagerDashboardViewModel
                {
                    Manager = user,
                    Department = department ?? new Department { Name = "Unassigned" },
                    TeamMembers = teamMembers,
                    Headcount = teamMembers.Count,
                    PresentToday = teamMembers.Count > 0 ? teamMembers.Count - 1 : 0, // Mock: 1 absent
                    PendingLeaveRequests = 3, // Mock
                    UpcomingReviews = 2, // Mock 
                    AverageTeamPerformance = 4.2 // Mock (out of 5)
                };
                
                return View("ManagerDashboard", managerViewModel);
            }
            else // Student
            {
                var enrolledCourseIds = await _context.Enrollments
                    .Where(e => e.StudentId == user.Id)
                    .Select(e => e.CourseId)
                    .ToListAsync();

                courses = await _context.Courses
                    .Include(c => c.Instructor)
                    .Include(c => c.Assignments)
                    .Where(c => enrolledCourseIds.Contains(c.Id))
                    .ToListAsync();
            }
            // Fetch all assignments from all courses for the todo list
            var upcomingAssignments = await _context.Assignments.ToListAsync(); // Needs filtering if we had filtering logic

            // Fetch personal calendar events
            var calendarEvents = await _context.CalendarEvents
                .Where(e => e.UserId == user.Id)
                .OrderBy(e => e.Start)
                .ToListAsync();

            var viewModel = new DashboardViewModel
            {
                User = user,
                Courses = courses,
                UpcomingAssignments = upcomingAssignments,
                StickyNotes = stickyNotes,
                Announcements = announcements,
                CalendarEvents = calendarEvents
            };

            return View(viewModel);
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddStickyNote(string content, string color)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier) ?? User.FindFirst("UserId");
            var userId = userIdClaim != null ? int.Parse(userIdClaim.Value) : 0;
            var note = new StickyNote
            {
                Content = content,
                Color = color,
                UserId = userId
            };
            _context.StickyNotes.Add(note);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteStickyNote(int id)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier) ?? User.FindFirst("UserId");
            var userId = userIdClaim != null ? int.Parse(userIdClaim.Value) : 0;
            var note = await _context.StickyNotes.FindAsync(id);
            if (note != null && note.UserId == userId)
            {
                _context.StickyNotes.Remove(note);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStickyNote(int id, string content)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier) ?? User.FindFirst("UserId");
            var userId = userIdClaim != null ? int.Parse(userIdClaim.Value) : 0;
            var note = await _context.StickyNotes.FindAsync(id);
            if (note != null && note.UserId == userId)
            {
                note.Content = content;
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [Authorize(Roles = "admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddAnnouncement(string title, DateTime date)
        {
            var announcement = new DashboardAnnouncement
            {
                Title = title,
                Date = DateTime.SpecifyKind(date, DateTimeKind.Utc)
            };
            _context.DashboardAnnouncements.Add(announcement);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [Authorize(Roles = "admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteAnnouncement(int id)
        {
            var announcement = await _context.DashboardAnnouncements.FindAsync(id);
            if (announcement != null)
            {
                _context.DashboardAnnouncements.Remove(announcement);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }


    }
}
