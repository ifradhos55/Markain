using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OzarkLMS.Data;
using OzarkLMS.Models;
using OzarkLMS.ViewModels;
using Microsoft.AspNetCore.Authorization;

namespace OzarkLMS.Controllers
{
    public class CoursesController : Controller
    {
        private readonly AppDbContext _context;

        public CoursesController(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier) ?? User.FindFirst("UserId");
            var userRole = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;

            if (userRole == "admin")
            {
                return View(await _context.Courses.Include(c => c.Instructor).ToListAsync());
            }
            else if (userRole == "instructor" && userIdClaim != null)
            {
                var userId = int.Parse(userIdClaim.Value);
                return View(await _context.Courses
                    .Include(c => c.Instructor)
                    .Where(c => c.InstructorId == userId)
                    .ToListAsync());
            }
            else if (userIdClaim != null)
            {
                // Student: Show enrolled courses
                var userId = int.Parse(userIdClaim.Value);
                var enrolledCourseIds = await _context.Enrollments
                    .Where(e => e.StudentId == userId)
                    .Select(e => e.CourseId)
                    .ToListAsync();

                return View(await _context.Courses
                    .Include(c => c.Instructor)
                    .Where(c => enrolledCourseIds.Contains(c.Id))
                    .ToListAsync());
            }

            return View(new List<Course>());
        }

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var course = await _context.Courses
                .Include(c => c.Modules)
                    .ThenInclude(m => m.Items)
                .Include(c => c.Assignments)
                .Include(c => c.Meetings)
                .Include(c => c.Enrollments)
                    .ThenInclude(e => e.Student)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (course == null) return NotFound();

            // Grading Logic
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier) ?? User.FindFirst("UserId");
            var userRole = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;

            if (userIdClaim != null)
            {
                 int userId = int.Parse(userIdClaim.Value);
                 
                 // Always Get Current User's Submissions (for "Your Grades" section)
                 var submissions = await _context.Submissions
                     .Include(s => s.Assignment)
                     .Where(s => s.StudentId == userId && s.Assignment.CourseId == id)
                     .ToListAsync();
                 
                 ViewBag.StudentSubmissions = submissions;
                 
                 // Calculate Grade: Average of Percentages
                 if (submissions.Any()) {
                      double sumPercentages = 0;
                      int count = 0;
                      foreach(var sub in submissions)
                      {
                          if(sub.Assignment != null)
                          {
                              int maxPoints = sub.Assignment.Points > 0 ? sub.Assignment.Points : 100;
                              if (sub.Score.HasValue)
                              {
                                  double percentage = (double)sub.Score.Value / maxPoints * 100;
                                  sumPercentages += percentage;
                                  count++;
                              }
                          }
                      }
                      ViewBag.CurrentGrade = count > 0 ? Math.Round(sumPercentages / count, 1) : 0;
                 } else {
                     ViewBag.CurrentGrade = 0;
                 }

                 if (userRole == "admin" || userRole == "instructor")
                 {
                     // Gradebook View: Get all submissions for this course
                     var allSubmissions = await _context.Submissions
                         .Include(s => s.Assignment)
                         .Include(s => s.Student)
                         .Where(s => s.Assignment.CourseId == id)
                         .ToListAsync();
                     
                     ViewBag.AllSubmissions = allSubmissions;
                 }
            }

            return View(course);
        }
        
        // GET: Courses/Create
        [Authorize(Roles = "admin, instructor")]
        public async Task<IActionResult> Create()
        {
            if (User.IsInRole("admin"))
            {
                ViewBag.Instructors = await _context.Users.Where(u => u.Role == "instructor").ToListAsync();
            }
            return View();
        }

        // POST: Courses/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "admin, instructor")]
        public async Task<IActionResult> Create([Bind("Id,Name,Code,Term,Color,Icon,InstructorId")] Course course)
        {
            // If instructor, force assign to self
            if (User.IsInRole("instructor"))
            {
                var userId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier).Value);
                course.InstructorId = userId;
            }

            if (ModelState.IsValid)
            {
                _context.Add(course);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            
            if (User.IsInRole("admin"))
            {
                ViewBag.Instructors = await _context.Users.Where(u => u.Role == "instructor").ToListAsync();
            }
            return View(course);
        }
        // GET: Courses/Edit/5
        [Authorize(Roles = "admin, instructor")]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var course = await _context.Courses.FindAsync(id);
            if (course == null) return NotFound();

            // Security Check: Instructors can only edit their own courses
            if (User.IsInRole("instructor"))
            {
                var userId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier).Value);
                if (course.InstructorId != userId) return Forbid();
            }

            if (User.IsInRole("admin"))
            {
                ViewBag.Instructors = await _context.Users.Where(u => u.Role == "instructor").ToListAsync();
            }
            return View(course);
        }

        // POST: Courses/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "admin, instructor")]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Name,Code,Term,Color,Icon,InstructorId")] Course course)
        {
            if (id != course.Id) return NotFound();

            // Security Check: Instructors can only edit their own courses
            if (User.IsInRole("instructor"))
            {
                var originalCourse = await _context.Courses.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id);
                if (originalCourse == null) return NotFound();
                
                var userId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier).Value);
                if (originalCourse.InstructorId != userId) return Forbid();

                // Prevent Instructor from changing the InstructorId
                course.InstructorId = userId; 
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(course);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!_context.Courses.Any(e => e.Id == course.Id)) return NotFound();
                    else throw;
                }
                return RedirectToAction(nameof(Index));
            }
            
            if (User.IsInRole("admin"))
            {
                ViewBag.Instructors = await _context.Users.Where(u => u.Role == "instructor").ToListAsync();
            }
            return View(course);
        }

        // GET: Courses/ManageStudents/5
        [Authorize(Roles = "admin, instructor")]
        public async Task<IActionResult> ManageStudents(int? id)
        {
            if (id == null) return NotFound();

            var course = await _context.Courses
                .Include(c => c.Enrollments)
                    .ThenInclude(e => e.Student)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (course == null) return NotFound();

            // Security Check
            if (User.IsInRole("instructor"))
            {
                var userId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier).Value);
                if (course.InstructorId != userId) return Forbid();
            }

            var enrolledStudentIds = course.Enrollments.Select(e => e.StudentId).ToList();
            
            // Get all students NOT enrolled in this course
            var availableStudents = await _context.Users
                .Where(u => u.Role == "student" && !enrolledStudentIds.Contains(u.Id))
                .ToListAsync();

            var viewModel = new CourseStudentsViewModel
            {
                Course = course,
                EnrolledStudents = course.Enrollments.Select(e => e.Student).ToList(),
                AvailableStudents = availableStudents
            };

            return View(viewModel);
        }

        // POST: Courses/AddStudent
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddStudent(int courseId, int studentId)
        {
            var enrollment = new Enrollment { CourseId = courseId, StudentId = studentId };
            _context.Enrollments.Add(enrollment);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(ManageStudents), new { id = courseId });
        }

        // POST: Courses/RemoveStudent
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveStudent(int courseId, int studentId)
        {
            var enrollment = await _context.Enrollments
                .FirstOrDefaultAsync(e => e.CourseId == courseId && e.StudentId == studentId);
            
            if (enrollment != null)
            {
                _context.Enrollments.Remove(enrollment);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(ManageStudents), new { id = courseId });
        }

        // POST: Courses/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "admin, instructor")]
        public async Task<IActionResult> Delete(int id)
        {
            var course = await _context.Courses.FindAsync(id);
            if (course == null) return NotFound();

            // RBAC: Instructors can only delete their own courses
            if (User.IsInRole("instructor"))
            {
                var userId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier).Value);
                if (course.InstructorId != userId) return Forbid();
            }

            _context.Courses.Remove(course);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        // POST: Courses/AddMeeting
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "admin, instructor")]
        public async Task<IActionResult> AddMeeting(int courseId, string name, DateTime startTime, DateTime endTime, string url)
        {
            var meeting = new Meeting
            {
                CourseId = courseId,
                Name = name,
                StartTime = DateTime.SpecifyKind(startTime, DateTimeKind.Utc),
                EndTime = DateTime.SpecifyKind(endTime, DateTimeKind.Utc),
                Url = url
            };

            if (ModelState.IsValid)
            {
                _context.Meetings.Add(meeting);
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Details), new { id = courseId, tab = "meetings" });
        }

        // POST: Courses/DeleteMeeting
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "admin, instructor")]
        public async Task<IActionResult> DeleteMeeting(int id, int courseId)
        {
            var meeting = await _context.Meetings.FindAsync(id);
            if (meeting != null)
            {
                _context.Meetings.Remove(meeting);
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Details), new { id = courseId, tab = "meetings" });
        }
    }
}
