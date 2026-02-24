using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OzarkLMS.Data;
using OzarkLMS.Models;
using System.Security.Claims;

namespace OzarkLMS.Controllers
{
    [Authorize]
    public class CalendarController : Controller
    {
        private readonly AppDbContext _context;

        public CalendarController(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier) ?? User.FindFirst("UserId");
            if (userIdClaim == null) return RedirectToAction("Login", "Account");
            var userId = int.Parse(userIdClaim.Value);
            var events = await _context.CalendarEvents
                                       .Where(e => e.UserId == userId)
                                       .OrderBy(e => e.Start)
                                       .ToListAsync();
            return View(events);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(string title, DateTime start, DateTime? end)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier) ?? User.FindFirst("UserId");
            var userId = userIdClaim != null ? int.Parse(userIdClaim.Value) : 0; // Should be authorized, but safe fallback
            var evt = new CalendarEvent
            {
                Title = title,
                Start = DateTime.SpecifyKind(start, DateTimeKind.Utc),
                End = end.HasValue ? DateTime.SpecifyKind(end.Value, DateTimeKind.Utc) : null,
                Color = "bg-blue-500", // Default color
                UserId = userId
            };
            _context.CalendarEvents.Add(evt);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier) ?? User.FindFirst("UserId");
            var userId = userIdClaim != null ? int.Parse(userIdClaim.Value) : 0;
            var evt = await _context.CalendarEvents.FindAsync(id);
            if (evt != null && evt.UserId == userId)
            {
                _context.CalendarEvents.Remove(evt);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
