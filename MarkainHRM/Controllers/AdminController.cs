using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OzarkLMS.Data;
using OzarkLMS.Models;
using OzarkLMS.ViewModels;
using Microsoft.AspNetCore.Authorization;

namespace OzarkLMS.Controllers
{
    [Authorize(Roles = "admin")]
    public class AdminController : Controller
    {
        private readonly AppDbContext _context;

        public AdminController(AppDbContext context)
        {
            _context = context;
        }

        // GET: Admin/Dashboard
        public async Task<IActionResult> Dashboard()
        {
            var students = await _context.Users.Where(u => u.Role == "student").ToListAsync();
            var instructors = await _context.Users.Where(u => u.Role == "instructor").ToListAsync();
            var courses = await _context.Courses.ToListAsync();
            
            // HR Stats
            var employeeCount = await _context.Users.CountAsync(u => u.Role == "employee" || u.Role == "manager" || u.Role == "hr_admin");
            var departmentCount = await _context.Departments.CountAsync();

            ViewBag.StudentCount = students.Count;
            ViewBag.InstructorCount = instructors.Count;
            ViewBag.CourseCount = courses.Count;
            ViewBag.EmployeeCount = employeeCount;
            ViewBag.DepartmentCount = departmentCount;

            var viewModel = new AdminDashboardViewModel
            {
                Students = students,
                Instructors = instructors
            };

            return View(viewModel);
        }

        // GET: Admin/CreateUser
        public IActionResult CreateUser()
        {
            return View();
        }

        // POST: Admin/CreateUser
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateUser([Bind("Username,Password,Role")] User user)
        {
            if (ModelState.IsValid)
            {
                // In a real app, check for duplicates and hash password
                if (await _context.Users.IgnoreQueryFilters().AnyAsync(u => u.Username == user.Username))
                {
                    ModelState.AddModelError("Username", "Username already exists");
                    return View(user);
                }

                _context.Add(user);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Dashboard));
            }
            return View(user);
        }

        // POST: Admin/CreateInstructor
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateInstructor(string username, string password)
        {
             if (await _context.Users.IgnoreQueryFilters().AnyAsync(u => u.Username == username))
             {
                 // In a real app we'd pass an error back, for now just redirect
                 return RedirectToAction(nameof(Dashboard));
             }

             var user = new User
             {
                 Username = username,
                 Password = password, // Note: Should be hashed
                 Role = "instructor"
             };
             _context.Users.Add(user);
             await _context.SaveChangesAsync();
             return RedirectToAction(nameof(Dashboard));
        }
        // POST: Admin/DeleteUser/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteUser(int id)
        {
            var user = await _context.Users
                .IgnoreQueryFilters() // Include soft-deleted users
                .FirstOrDefaultAsync(u => u.Id == id);
                
            if (user == null) return RedirectToAction(nameof(Dashboard));

            // Soft Delete: Mark user as deleted instead of removing
            user.IsDeleted = true;

            // Unassign Courses (if Instructor)
            var courses = _context.Courses.Where(c => c.InstructorId == id);
            foreach (var course in courses)
            {
                course.InstructorId = null;
            }

            _context.Update(user);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Dashboard));
        }

        // POST: Admin/EditUser
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditUser(int id, string username, string password)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null) return RedirectToAction(nameof(Dashboard));

            // Check if username is taken by another user
             if (await _context.Users.IgnoreQueryFilters().AnyAsync(u => u.Username == username && u.Id != id))
             {
                 // In a real app, we'd pass an error. For now, just return.
                 return RedirectToAction(nameof(Dashboard));
             }

            user.Username = username;
            
            // Only update password if provided
            if (!string.IsNullOrWhiteSpace(password))
            {
                user.Password = password; // In a real app, hash this!
            }

            _context.Update(user);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Dashboard));
        }
    }
}
