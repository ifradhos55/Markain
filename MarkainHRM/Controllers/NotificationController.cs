using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OzarkLMS.Data;
using OzarkLMS.Models;
using System.Security.Claims;

namespace OzarkLMS.Controllers
{
    [Authorize]
    public class NotificationController : Controller
    {
        private readonly AppDbContext _context;

        public NotificationController(AppDbContext context)
        {
            _context = context;
        }

        // GET: /Notification
        public async Task<IActionResult> Index()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier) ?? User.FindFirst("UserId");
            if (userIdClaim == null) return RedirectToAction("Login", "Account");
            var userId = int.Parse(userIdClaim.Value);
            var notifications = await _context.Notifications
                .Where(n => n.RecipientId == userId || n.RecipientId == null) // Broadcast + Direct
                .OrderByDescending(n => n.SentDate)
                .Include(n => n.Sender)
                .ToListAsync();

            return View(notifications);
        }

        // GET: /Notification/GetDropdownPartial
        public async Task<IActionResult> GetDropdownPartial()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier) ?? User.FindFirst("UserId");
            if (userIdClaim == null) return Unauthorized();
            
            var userId = int.Parse(userIdClaim.Value);
            var notifications = await _context.Notifications
                .Where(n => n.RecipientId == userId || n.RecipientId == null)
                .OrderByDescending(n => n.SentDate)
                .Take(5) // Limit for dropdown
                .Include(n => n.Sender)
                .ToListAsync();

            return PartialView("_NotificationList", notifications);
        }

        // GET: /Notification/Send (Admin/Instructor Only)
        [Authorize(Roles = "admin, instructor")]
        public async Task<IActionResult> Send()
        {
            ViewBag.Students = await _context.Users.Where(u => u.Role == "student").ToListAsync();
            return View();
        }

        // POST: /Notification/Create
        [HttpPost]
        [Authorize(Roles = "admin, instructor")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(string title, string message, int? recipientId, string? actionUrl)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier) ?? User.FindFirst("UserId");
            var senderId = userIdClaim != null ? int.Parse(userIdClaim.Value) : 0;

            if (recipientId != null)
            {
                // Direct Message
                var notification = new Notification
                {
                    Title = title,
                    Message = message,
                    SenderId = senderId,
                    RecipientId = recipientId,
                    ActionUrl = actionUrl,
                    SentDate = DateTime.UtcNow
                };
                _context.Notifications.Add(notification);
            }
            else
            {
                // Broadcast to All Students
                var students = await _context.Users.Where(u => u.Role == "student").Select(u => u.Id).ToListAsync();
                var notifications = students.Select(studentId => new Notification
                {
                    Title = title,
                    Message = message,
                    SenderId = senderId,
                    RecipientId = studentId, // Assign to specific student
                    ActionUrl = actionUrl,
                    SentDate = DateTime.UtcNow
                });
                _context.Notifications.AddRange(notifications);
            }

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> MarkRead(int id)
        {
            var notification = await _context.Notifications.FindAsync(id);
            if (notification != null)
            {
                notification.IsRead = true;
                await _context.SaveChangesAsync();
            }
            return Ok();
        }

        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            var notification = await _context.Notifications.FindAsync(id);
            if (notification != null)
            {
                // Ensure only recipient can delete their own
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (userIdClaim != null && (notification.RecipientId == int.Parse(userIdClaim) || notification.RecipientId == null))
                {
                    _context.Notifications.Remove(notification);
                    await _context.SaveChangesAsync();
                }
            }
            return Ok();
        }

        [HttpPost]
        public async Task<IActionResult> ClearAll()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userIdClaim != null)
            {
                var userId = int.Parse(userIdClaim);
                var notifications = await _context.Notifications
                    .Where(n => n.RecipientId == userId)
                    .ToListAsync();

                _context.Notifications.RemoveRange(notifications);
                await _context.SaveChangesAsync();
            }
            return Ok();
        }
    }
}
