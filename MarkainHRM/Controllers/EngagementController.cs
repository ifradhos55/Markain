using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OzarkLMS.Data;
using System.Security.Claims;

namespace OzarkLMS.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class EngagementController : ControllerBase
    {
        private readonly AppDbContext _context;

        public EngagementController(AppDbContext context)
        {
            _context = context;
        }

        [HttpPost("water")]
        public async Task<IActionResult> WaterTree()
        {
            var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdStr)) return Unauthorized();

            if (!int.TryParse(userIdStr, out var userId)) return BadRequest();

            var user = await _context.Users.FindAsync(userId);
            if (user == null) return NotFound();

            // Increment progress
            user.TreeProgress++;
            if (user.TreeProgress > 7)
            {
                user.TreeProgress = 0;
            }

            await _context.SaveChangesAsync();
            return Ok(new { progress = user.TreeProgress });
        }
    }
}
