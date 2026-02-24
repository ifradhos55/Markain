using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OzarkLMS.Data;
using OzarkLMS.Models;
using Microsoft.AspNetCore.Authorization;

namespace OzarkLMS.Controllers
{
    [Authorize(Roles = "admin, instructor")]
    public class ModulesController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _environment;

        public ModulesController(AppDbContext context, IWebHostEnvironment environment)
        {
            _context = context;
            _environment = environment;
        }

        // GET: Modules/Create?courseId=5
        public IActionResult Create(int? courseId)
        {
            if (courseId == null) return NotFound();

            // Security Check
            if (User.IsInRole("instructor"))
            {
                var course = _context.Courses.Find(courseId);
                if (course == null) return NotFound();
                var userId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier).Value);
                if (course.InstructorId != userId) return Forbid();
            }

            ViewBag.CourseId = courseId;
            return View();
        }

        // POST: Modules/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("CourseId,Title")] Module module, IFormFile? file, string? displayMode)
        {
            ModelState.Remove("Course");
            ModelState.Remove("Items");

            if (ModelState.IsValid)
            {
                if (User.IsInRole("instructor"))
                {
                    var course = await _context.Courses.FindAsync(module.CourseId);
                    if (course == null) return NotFound();
                    var userId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier).Value);
                    if (course.InstructorId != userId) return Forbid();
                }

                _context.Add(module);
                await _context.SaveChangesAsync(); // Save to get Module Id

                // Notify new Module
                var courseName = (await _context.Courses.FindAsync(module.CourseId))?.Name ?? "Course";
                await NotifyStudents(module.CourseId, $"New Module: {module.Title}", $"New module '{module.Title}' added to {courseName}.");

                // Handle optional initial file upload
                if (file != null && file.Length > 0)
                {
                    await AddFileItem(module.Id, file, file.FileName, "file", displayMode ?? "link");
                }

                return RedirectToAction("Details", "Courses", new { id = module.CourseId });
            }
            ViewBag.CourseId = module.CourseId;
            return View(module);
        }

        // GET: Modules/AddItem?moduleId=5
        public async Task<IActionResult> AddItem(int? moduleId)
        {
            if (moduleId == null) return NotFound();
            var module = await _context.Modules.Include(m => m.Course).FirstOrDefaultAsync(m => m.Id == moduleId);
            if (module == null) return NotFound();

            if (User.IsInRole("instructor"))
            {
                var userId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier).Value);
                if (module.Course.InstructorId != userId) return Forbid();
            }

            ViewBag.Module = module;
            return View();
        }

        // POST: Modules/AddItem
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddItem(int moduleId, string title, string type, IFormFile? file, string? displayMode)
        {
            var module = await _context.Modules.Include(m => m.Course).FirstOrDefaultAsync(m => m.Id == moduleId);
            if (module == null) return NotFound();

            if (User.IsInRole("instructor"))
            {
                var userId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier).Value);
                if (module.Course.InstructorId != userId) return Forbid();
            }

            if (type == "file" && file != null && file.Length > 0)
            {
                await AddFileItem(moduleId, file, title, type, displayMode ?? "link");
            }
            else
            {
                // Handle non-file items (like pages or links)
                var item = new ModuleItem
                {
                    ModuleId = moduleId,
                    Title = title,
                    Type = type,
                    DisplayMode = displayMode ?? "link"
                };
                _context.ModuleItems.Add(item);
                await _context.SaveChangesAsync();

                // Notify Enrolled Students (Non-file item)
                await NotifyStudents(module.CourseId, $"New Content: {title}", $"New content '{title}' added to {module.Course.Name}.", type == "link" || type == "page" ? $"/Courses/Details/{module.CourseId}" : null);
            }

            return RedirectToAction("Details", "Courses", new { id = module.CourseId });
        }

        private async Task AddFileItem(int moduleId, IFormFile file, string title, string type, string displayMode)
        {
            var uploads = Path.Combine(_environment.WebRootPath, "uploads");
            if (!Directory.Exists(uploads)) Directory.CreateDirectory(uploads);

            var fileName = Guid.NewGuid() + Path.GetExtension(file.FileName);
            var filePath = Path.Combine(uploads, fileName);
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }
            var contentUrl = "/uploads/" + fileName;

            var item = new ModuleItem
            {
                ModuleId = moduleId,
                Title = title,
                Type = type,
                ContentUrl = contentUrl,
                DisplayMode = displayMode
            };

            _context.ModuleItems.Add(item);
            await _context.SaveChangesAsync();

            // Notify Enrolled Students
            var module = await _context.Modules.Include(m => m.Course).FirstOrDefaultAsync(m => m.Id == moduleId);
            if (module != null)
            {
                await NotifyStudents(module.CourseId, $"New Content: {title}", $"New content has been added to {module.Course.Name}: {title}");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var module = await _context.Modules.FindAsync(id);
            if (module == null) return NotFound();

            // RBAC Check
            if (User.IsInRole("instructor"))
            {
                var course = await _context.Courses.FindAsync(module.CourseId);
                var userId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
                if (course == null || course.InstructorId != userId) return Forbid();
            }

            var courseId = module.CourseId;
            _context.Modules.Remove(module);
            await _context.SaveChangesAsync();
            return RedirectToAction("Details", "Courses", new { id = courseId, tab = "home" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteItem(int id)
        {
            var item = await _context.ModuleItems.Include(i => i.Module).FirstOrDefaultAsync(i => i.Id == id);
            if (item == null) return NotFound();

            // RBAC Check
            if (User.IsInRole("instructor"))
            {
                var course = await _context.Courses.FindAsync(item.Module.CourseId);
                var userId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
                if (course == null || course.InstructorId != userId) return Forbid();
            }

            var courseId = item.Module.CourseId;
            _context.ModuleItems.Remove(item);
            await _context.SaveChangesAsync();
            return RedirectToAction("Details", "Courses", new { id = courseId, tab = "home" });
        }

        private async Task NotifyStudents(int courseId, string title, string message, string? actionUrl = null)
        {
            var enrollments = await _context.Enrollments
                .Where(e => e.CourseId == courseId)
                .Select(e => e.StudentId)
                .ToListAsync();

            var senderId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "0");

            var notifications = enrollments.Select(studentId => new Notification
            {
                RecipientId = studentId,
                SenderId = senderId,
                Title = title,
                Message = message,
                SentDate = DateTime.UtcNow,
                IsRead = false,
                ActionUrl = actionUrl
            });

            _context.Notifications.AddRange(notifications);
            await _context.SaveChangesAsync();
        }
    }
}
