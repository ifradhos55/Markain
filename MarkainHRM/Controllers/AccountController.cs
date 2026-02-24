using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OzarkLMS.Data;
using OzarkLMS.Models;
using OzarkLMS.ViewModels;
using System.Security.Claims;

namespace OzarkLMS.Controllers
{
    public class AccountController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _environment;
        private readonly IHttpClientFactory _httpClientFactory;

        public AccountController(AppDbContext context, IWebHostEnvironment environment, IHttpClientFactory httpClientFactory)
        {
            _context = context;
            _environment = environment;
            _httpClientFactory = httpClientFactory;
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult Login()
        {
            if (User.Identity!.IsAuthenticated)
            {
                return RedirectToAction("Index", "Home");
            }
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (ModelState.IsValid)
            {
                var user = await _context.Users
                    .IgnoreQueryFilters() // Need to check IsDeleted explicitly
                    .FirstOrDefaultAsync(u => u.Username == model.Username && u.Password == model.Password);
                    
                if (user != null)
                {
                    // Prevent deleted users from logging in
                    if (user.IsDeleted)
                    {
                        ModelState.AddModelError(string.Empty, "This account has been deactivated.");
                        return View(model);
                    }

                    var claims = new List<Claim>
                    {
                        new Claim(ClaimTypes.Name, user.Username),
                        new Claim(ClaimTypes.Role, user.Role),
                        new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                        new Claim("UserId", user.Id.ToString())
                    };

                    var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                    var authProperties = new AuthenticationProperties
                    {
                        IsPersistent = true
                    };

                    await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(claimsIdentity), authProperties);
                    return RedirectToAction("Index", "Home");
                }
                ModelState.AddModelError(string.Empty, "Invalid login attempt.");
            }
            return View(model);
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult Register()
        {
            if (User.Identity!.IsAuthenticated)
            {
                return RedirectToAction("Index", "Home");
            }
            return View();
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (ModelState.IsValid)
            {
                if (await _context.Users.AnyAsync(u => u.Username == model.Username))
                {
                    ModelState.AddModelError("Username", "Username already taken.");
                    return View(model);
                }

                var user = new User
                {
                    Username = model.Username,
                    Password = model.Password, // Note: Should be hashed in production
                    Role = model.Role
                };

                _context.Users.Add(user);
                await _context.SaveChangesAsync();

                // Auto-login after register
                var claims = new List<Claim>
                    {
                        new Claim(ClaimTypes.Name, user.Username),
                        new Claim(ClaimTypes.Role, user.Role),
                        new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                        new Claim("UserId", user.Id.ToString())
                    };

                var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(claimsIdentity));

                return RedirectToAction("Index", "Home");
            }
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Login", "Account");
        }


        private async Task SignInUser(User user)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(ClaimTypes.Role, user.Role),
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim("UserId", user.Id.ToString())
            };

            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var authProperties = new AuthenticationProperties { IsPersistent = true };

            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(claimsIdentity), authProperties);
        }

        private IActionResult RedirectToLocal(string? returnUrl)
        {
            if (Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }
            return RedirectToAction("Index", "Home");
        }


        [HttpGet]
        public async Task<IActionResult> Profile()
        {
            if (!User.Identity!.IsAuthenticated)
            {
                return RedirectToAction("Login");
            }

            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier) ?? User.FindFirst("UserId");
            if (userIdClaim == null) return RedirectToAction("Login");

            var currentUserId = int.Parse(userIdClaim.Value);
            
            // If id is provided, view that user's profile. Otherwise view own.
            // But the current route is just /Account/Profile. 
            // We need to support viewing others. Let's stick to "Own Profile" for now per existing code, 
            // OR if a query param ?userId=5 is passed.
            // The signature is just Profile(). Let's check query string manually or assume this is "My Profile".
            // Requirement: "The profile owner sees all their posts. Other users see only posts they are allowed to see."
            // This implies we CAN view other profiles.
            
            // Let's check if 'id' is in Query
            int targetUserId = currentUserId;
            if (Request.Query.ContainsKey("userId"))
            {
                int.TryParse(Request.Query["userId"], out targetUserId);
            }

            var user = await _context.Users
                .AsSplitQuery()
                .Include(u => u.Enrollments)
                    .ThenInclude(e => e.Course)
                .Include(u => u.Posts)
                    .ThenInclude(p => p.Votes)
                .Include(u => u.Posts)
                    .ThenInclude(p => p.Comments)
                        .ThenInclude(c => c.User)
                .Include(u => u.Posts)
                    .ThenInclude(p => p.Comments)
                        .ThenInclude(c => c.Votes)
                .Include(u => u.Posts)
                    .ThenInclude(p => p.Comments)
                        .ThenInclude(c => c.Replies)
                            .ThenInclude(r => r.User)
                .Include(u => u.Posts)
                    .ThenInclude(p => p.Comments)
                        .ThenInclude(c => c.Replies)
                            .ThenInclude(r => r.Votes)
                .Include(u => u.Followers)
                .Include(u => u.Following)
                .Include(u => u.SharedPosts)
                    .ThenInclude(sp => sp.Post)
                        .ThenInclude(p => p.User)
                .Include(u => u.SharedPosts)
                    .ThenInclude(sp => sp.Post)
                        .ThenInclude(p => p.Votes)
                .Include(u => u.SharedPosts)
                    .ThenInclude(sp => sp.Post)
                        .ThenInclude(p => p.Comments)
                            .ThenInclude(c => c.User)
                .FirstOrDefaultAsync(u => u.Id == targetUserId);

            if (user == null) return NotFound();

            var viewModel = new UserProfileViewModel
            {
                User = user,
                EnrolledCourses = user.Enrollments.Select(e => e.Course).ToList(),
                TaughtCourses = await _context.Courses.Where(c => c.InstructorId == targetUserId).ToListAsync(),
                ChatGroups = await _context.ChatGroupMembers
                    .Where(m => m.UserId == targetUserId)
                    .Select(m => m.ChatGroup)
                    .OrderByDescending(g => g.IsDefault)
                    .ToListAsync(),
                
                // Social Hub
                Posts = user.Posts.OrderByDescending(p => p.CreatedAt).ToList(),
                SharedPosts = user.SharedPosts.OrderByDescending(sp => sp.SharedAt).ToList(),
                FollowersCount = user.Followers.Count,
                FollowingCount = user.Following.Count,
                IsFollowing = await _context.Follows.AnyAsync(f => f.FollowerId == currentUserId && f.FollowingId == targetUserId)
            };

            // If Admin, load lists and clear course lists (per requirement to replace them)
            if (user.Role == "admin" && targetUserId == currentUserId)
            {
                viewModel.AllInstructors = await _context.Users.Where(u => u.Role == "instructor").ToListAsync();
                viewModel.AllStudents = await _context.Users.Where(u => u.Role == "student").ToListAsync();
                viewModel.EnrolledCourses.Clear(); 
            }

            ViewBag.CurrentUserId = currentUserId; // To check if Owner
            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateBio(string bio)
        {
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier) ?? User.FindFirst("UserId");
            if (userIdClaim == null) return RedirectToAction("Login");
            var userId = int.Parse(userIdClaim.Value);

            var user = await _context.Users.FindAsync(userId);
            if (user != null)
            {
                user.Bio = bio;
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Profile));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleFollow(int targetUserId)
        {
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier) ?? User.FindFirst("UserId");
            if (userIdClaim == null) return RedirectToAction("Login");
            var currentUserId = int.Parse(userIdClaim.Value);

            if (currentUserId == targetUserId) return RedirectToAction(nameof(Profile));

            var existingFollow = await _context.Follows.FindAsync(currentUserId, targetUserId);
            if (existingFollow != null)
            {
                _context.Follows.Remove(existingFollow);
            }
            else
            {
                _context.Follows.Add(new Follow { FollowerId = currentUserId, FollowingId = targetUserId });
            }
            
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Profile), new { userId = targetUserId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateUserSettings(UpdateUserSettingsViewModel model)
        {
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier) ?? User.FindFirst("UserId");
            if (userIdClaim == null) return RedirectToAction("Login");
            var userId = int.Parse(userIdClaim.Value);

            var user = await _context.Users.FindAsync(userId);
            if (user == null) {
                return NotFound();
            }

            // Check if user is trying to change username or password
            bool changingUsername = !string.IsNullOrWhiteSpace(model.NewUsername) && model.NewUsername != user.Username;
            bool changingPassword = !string.IsNullOrWhiteSpace(model.NewPassword);

            // Validate current password if changing username or password
            if (changingUsername || changingPassword)
            {
                if (string.IsNullOrWhiteSpace(model.CurrentPassword))
                {
                    TempData["Error"] = "Current password is required to change username or password.";
                    return RedirectToAction(nameof(Profile));
                }

                if (user.Password != model.CurrentPassword)
                {
                    TempData["Error"] = "Current password is incorrect.";
                    return RedirectToAction(nameof(Profile));
                }
            }

            // Update Username
            if (changingUsername)
            {
                // Check if username is already taken
                var existingUser = await _context.Users
                    .AnyAsync(u => u.Username == model.NewUsername && u.Id != userId);
                
                if (existingUser)
                {
                    TempData["Error"] = "Username is already taken.";
                    return RedirectToAction(nameof(Profile));
                }

                user.Username = model.NewUsername;
            }

            // Update Password
            if (changingPassword)
            {
                if (model.NewPassword != model.ConfirmNewPassword)
                {
                    TempData["Error"] = "New password and confirmation do not match.";
                    return RedirectToAction(nameof(Profile));
                }

                user.Password = model.NewPassword;
            }

            if (model.ProfilePictureFile != null && model.ProfilePictureFile.Length > 0)
            {
                var uploads = Path.Combine(_environment.WebRootPath, "uploads");
                if (!Directory.Exists(uploads)) Directory.CreateDirectory(uploads);

                var fileName = Guid.NewGuid() + Path.GetExtension(model.ProfilePictureFile.FileName);
                var filePath = Path.Combine(uploads, fileName);
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await model.ProfilePictureFile.CopyToAsync(stream);
                }
                user.ProfilePictureUrl = "/uploads/" + fileName;
            }
            else if (!string.IsNullOrEmpty(model.ProfilePictureUrl))
            {
                try
                {
                    var client = _httpClientFactory.CreateClient();
                    var response = await client.GetAsync(model.ProfilePictureUrl);
                    if (response.IsSuccessStatusCode)
                    {
                        var contentType = response.Content.Headers.ContentType?.MediaType;
                        if (contentType != null && contentType.StartsWith("image/"))
                        {
                            var uploads = Path.Combine(_environment.WebRootPath, "uploads");
                            if (!Directory.Exists(uploads)) Directory.CreateDirectory(uploads);

                            // Detect extension from content type or fallback to original URL
                            string extension = ".jpg"; // fallback
                            if (contentType == "image/png") extension = ".png";
                            else if (contentType == "image/gif") extension = ".gif";
                            else if (contentType == "image/webp") extension = ".webp";
                            else if (contentType == "image/jpeg") extension = ".jpg";
                            else {
                                var uri = new Uri(model.ProfilePictureUrl);
                                var ext = Path.GetExtension(uri.AbsolutePath);
                                if (!string.IsNullOrEmpty(ext)) extension = ext;
                            }

                            var fileName = Guid.NewGuid() + extension;
                            var filePath = Path.Combine(uploads, fileName);
                            
                            var imageBytes = await response.Content.ReadAsByteArrayAsync();
                            await System.IO.File.WriteAllBytesAsync(filePath, imageBytes);
                            
                            user.ProfilePictureUrl = "/uploads/" + fileName;
                        }
                        else 
                        {
                            // If it's not a direct image, we'll try to use it as is but warn for broken images
                            // In a more advanced version, we could scrape the page for an og:image tag
                            user.ProfilePictureUrl = model.ProfilePictureUrl;
                        }
                    }
                    else 
                    {
                        user.ProfilePictureUrl = model.ProfilePictureUrl;
                    }
                }
                catch
                {
                    // If download fails, fallback to storing the URL as is
                    user.ProfilePictureUrl = model.ProfilePictureUrl;
                }
            }
            else {
                // No picture update detected, keep existing or null
            }

            try {
                _context.Entry(user).State = EntityState.Modified;
                await _context.SaveChangesAsync();
            } catch (Exception ex) {
                TempData["Error"] = "Error saving settings: " + ex.Message;
                return RedirectToAction(nameof(Profile));
            }
            
            // If username changed, update authentication claims
            if (changingUsername)
            {
                await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.Name, user.Username),
                    new Claim(ClaimTypes.Role, user.Role),
                    new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                    new Claim("UserId", user.Id.ToString())
                };

                var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                var authProperties = new AuthenticationProperties { IsPersistent = true };

                await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, 
                    new ClaimsPrincipal(claimsIdentity), authProperties);

                TempData["Success"] = "Your settings have been updated successfully!";
            }
            else if (changingPassword || model.ProfilePictureFile != null || !string.IsNullOrEmpty(model.ProfilePictureUrl))
            {
                TempData["Success"] = "Your settings have been updated successfully!";
            }

            return RedirectToAction(nameof(Profile));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveSharedPost(int sharedPostId)
        {
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier) ?? User.FindFirst("UserId");
            if (userIdClaim == null) return RedirectToAction("Login");
            var userId = int.Parse(userIdClaim.Value);

            var sharedPost = await _context.SharedPosts.FindAsync(sharedPostId);
            if (sharedPost == null) return NotFound();

            if (sharedPost.UserId != userId && !User.IsInRole("admin"))
            {
                return Forbid();
            }

            _context.SharedPosts.Remove(sharedPost);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Profile));
        }

        public IActionResult AccessDenied()
        {
            return View();
        }

        public IActionResult LoginRedirect()
        {
             return RedirectToAction("Login", "Account");
        }
    }
}
