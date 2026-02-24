using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OzarkLMS.Data;
using OzarkLMS.Models;

namespace OzarkLMS.Controllers
{
    public class RecruitmentController : Controller
    {
        private readonly AppDbContext _context;

        public RecruitmentController(AppDbContext context)
        {
            _context = context;
        }

        // GET: /Recruitment (Public Job Board)
        [AllowAnonymous]
        public async Task<IActionResult> Index()
        {
            var jobs = await _context.JobPostings
                .Include(j => j.Department)
                .Include(j => j.Applications)
                    .ThenInclude(a => a.Candidate)
                .Where(j => j.IsActive && (j.ClosingDate == null || j.ClosingDate > DateTime.UtcNow))
                .OrderByDescending(j => j.PostedDate)
                .ToListAsync();

            return View(jobs);
        }

        // GET: /Recruitment/Details/5
        [AllowAnonymous]
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var job = await _context.JobPostings
                .Include(j => j.Department)
                .Include(j => j.Applications)
                    .ThenInclude(a => a.Candidate)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (job == null) return NotFound();

            return View(job);
        }

        // POST: /Recruitment/Apply
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Apply(int JobPostingId, [Bind("FirstName,LastName,Email,Phone,LinkedInUrl")] Candidate candidate, IFormFile Resume, string CoverLetter)
        {
            try
            {
                if (Resume != null && Resume.ContentType != "application/pdf")
                {
                    ModelState.AddModelError("Resume", "Only PDF resumes are accepted.");
                }

                if (ModelState.IsValid)
                {
                    string? resumePath = null;
                    // Handle PDF upload
                    if (Resume != null && Resume.Length > 0)
                    {
                        var fileName = Guid.NewGuid().ToString() + Path.GetExtension(Resume.FileName);
                        var uploadsDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "resumes");
                        
                        if (!Directory.Exists(uploadsDir))
                        {
                            Directory.CreateDirectory(uploadsDir);
                        }

                        var filePath = Path.Combine(uploadsDir, fileName);
                        using (var stream = new FileStream(filePath, FileMode.Create))
                        {
                            await Resume.CopyToAsync(stream);
                        }
                        resumePath = "/uploads/resumes/" + fileName;
                    }

                    // Check if candidate exists by email
                    var existingCandidate = await _context.Candidates.FirstOrDefaultAsync(c => c.Email == candidate.Email);
                    
                    if (existingCandidate != null)
                    {
                        // Update existing candidate info
                        existingCandidate.FirstName = candidate.FirstName;
                        existingCandidate.LastName = candidate.LastName;
                        existingCandidate.Phone = candidate.Phone;
                        existingCandidate.LinkedInUrl = candidate.LinkedInUrl;
                        if (resumePath != null)
                        {
                            existingCandidate.ResumeUrl = resumePath;
                        }
                        candidate = existingCandidate; 
                    }
                    else
                    {
                        if (resumePath != null)
                        {
                            candidate.ResumeUrl = resumePath;
                        }
                        _context.Add(candidate);
                        await _context.SaveChangesAsync();
                    }

                    var application = new JobApplication
                    {
                        JobPostingId = JobPostingId,
                        CandidateId = candidate.Id,
                        CoverLetter = CoverLetter,
                        Status = "New",
                        AppliedDate = DateTime.UtcNow
                    };

                    _context.Add(application);
                    await _context.SaveChangesAsync();
                    
                    TempData["SuccessMessage"] = "Application submitted successfully!";
                    return RedirectToAction(nameof(Index));
                }
                TempData["ErrorMessage"] = "Please ensure all fields are correct and a PDF resume is attached.";
            }
            catch (Exception ex)
            {
                // Catch the real error and show it
                TempData["ErrorMessage"] = "Error: " + ex.Message + (ex.InnerException != null ? " | " + ex.InnerException.Message : "");
            }
            return RedirectToAction(nameof(Details), new { id = JobPostingId });
        }

        // GET: /Recruitment/AdminDashboard
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> AdminDashboard()
        {
            // Show stats and recent applications
            var activeJobsCount = await _context.JobPostings.CountAsync(j => j.IsActive);
            var totalApplications = await _context.JobApplications.CountAsync();
            var newApplications = await _context.JobApplications.CountAsync(a => a.Status == "New");

            ViewBag.ActiveJobsCount = activeJobsCount;
            ViewBag.TotalApplications = totalApplications;
            ViewBag.NewApplications = newApplications;

            var recentApplications = await _context.JobApplications
                .Include(a => a.JobPosting)
                .Include(a => a.Candidate)
                .OrderByDescending(a => a.AppliedDate)
                .Take(10)
                .ToListAsync();

            // Recent Listings section
            ViewBag.RecentListings = await _context.JobPostings
                .Include(j => j.Department)
                .Include(j => j.Applications)
                    .ThenInclude(a => a.Candidate)
                .OrderByDescending(j => j.PostedDate)
                .Take(5)
                .ToListAsync();

            return View(recentApplications);
        }

        // GET: /Recruitment/Create
        [Authorize(Roles = "admin")]
        public IActionResult Create()
        {
            ViewBag.Departments = _context.Departments.ToList();
            return View();
        }

        // POST: /Recruitment/Create
        [HttpPost]
        [Authorize(Roles = "admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Title,Description,Requirements,Location,EmploymentType,SalaryRangeMin,SalaryRangeMax,DepartmentId,ClosingDate,IsActive")] JobPosting jobPosting)
        {
            if (ModelState.IsValid)
            {
                jobPosting.PostedDate = DateTime.UtcNow;
                _context.Add(jobPosting);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(AdminDashboard));
            }
            ViewBag.Departments = _context.Departments.ToList();
            return View(jobPosting);
        }

        // POST: /Recruitment/UpdateStatus
        [HttpPost]
        [Authorize(Roles = "admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStatus(int ApplicationId, string Status)
        {
            var application = await _context.JobApplications.FindAsync(ApplicationId);
            if (application != null)
            {
                application.Status = Status; // e.g., "Accepted", "Denied"
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = $"Application {Status} successfully.";
                return RedirectToAction(nameof(AdminDashboard));
            }
            return NotFound();
        }
    }
}
