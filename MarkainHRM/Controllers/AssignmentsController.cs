using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OzarkLMS.Data;
using OzarkLMS.Models;
using Microsoft.AspNetCore.Authorization;

namespace OzarkLMS.Controllers
{
    [Authorize]
    public class AssignmentsController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _environment;

        public AssignmentsController(AppDbContext context, IWebHostEnvironment environment)
        {
            _context = context;
            _environment = environment;
        }

        // GET: Assignments/Create?courseId=5&type=quiz
        [Authorize(Roles = "admin, instructor")]
        public async Task<IActionResult> Create(int? courseId, string? type)
        {
            if (courseId == null) return NotFound();

            var course = await _context.Courses.FindAsync(courseId);
            if (course == null) return NotFound();

            if (User.IsInRole("instructor"))
            {
                var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
                if (userIdClaim == null) return Forbid();
                var userId = int.Parse(userIdClaim.Value);

                if (course.InstructorId != userId) return Forbid();
            }

            ViewBag.Course = course;
            return View(new Assignment { CourseId = course.Id, Type = type ?? "assignment" });
        }

        // GET: Assignments/Edit/5 (To add questions)
        [Authorize(Roles = "admin, instructor")]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var assignment = await _context.Assignments
                .Include(a => a.Course)
                .Include(a => a.Questions)
                .ThenInclude(q => q.Options)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (assignment == null) return NotFound();

            if (User.IsInRole("instructor"))
            {
                var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
                if (userIdClaim == null) return Forbid();
                var userId = int.Parse(userIdClaim.Value);
                if (assignment.Course != null && assignment.Course.InstructorId != userId) return Forbid();
            }

            if (assignment == null) return NotFound();

            return View(assignment);
        }

        // POST: Assignments/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "admin, instructor")]
        public async Task<IActionResult> Create([Bind("CourseId,Title,DueDate,Type,MaxAttempts,Description,SubmissionType,Points")] Assignment assignment, IFormFile? attachment)
        {
            if (ModelState.IsValid)
            {
                // Ensure Date is UTC for Postgres
                assignment.DueDate = DateTime.SpecifyKind(assignment.DueDate, DateTimeKind.Utc);

                // Security Check
                if (User.IsInRole("instructor"))
                {
                    var courseCheck = await _context.Courses.FindAsync(assignment.CourseId);
                    if (courseCheck == null) return NotFound();

                    var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
                    if (userIdClaim == null) return Forbid();
                    var userId = int.Parse(userIdClaim.Value);

                    if (courseCheck.InstructorId != userId) return Forbid();
                }

                // Handle Attachment
                if (attachment != null && attachment.Length > 0)
                {
                    var uploads = Path.Combine(_environment.WebRootPath, "uploads");
                    if (!Directory.Exists(uploads)) Directory.CreateDirectory(uploads);

                    var fileName = Guid.NewGuid() + Path.GetExtension(attachment.FileName);
                    var filePath = Path.Combine(uploads, fileName);
                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await attachment.CopyToAsync(stream);
                    }
                    assignment.AttachmentUrl = "/uploads/" + fileName;
                }

                _context.Add(assignment);
                await _context.SaveChangesAsync();

                // Notify Enrolled Students
                var courseName = (await _context.Courses.FindAsync(assignment.CourseId))?.Name ?? "Course";
                var typeName = assignment.Type == "quiz" ? "Quiz" : "Assignment";
                var actionUrl = $"/Assignments/Take/{assignment.Id}";

                await NotifyStudents(assignment.CourseId,
                    $"New {typeName}: {assignment.Title}",
                    $"A new {typeName.ToLower()} '{assignment.Title}' has been added to {courseName}.",
                    actionUrl);

                // If it is a quiz, redirect to Edit page to add questions
                if (assignment.Type == "quiz")
                {
                    return RedirectToAction(nameof(Edit), new { id = assignment.Id });
                }

                return RedirectToAction("Details", "Courses", new { id = assignment.CourseId, tab = assignment.Type == "quiz" ? "quizzes" : "assignments" });
            }

            var course = await _context.Courses.FindAsync(assignment.CourseId);
            ViewBag.Course = course;
            return View(assignment);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "admin, instructor")]
        public async Task<IActionResult> AddQuestion([FromForm] int assignmentId, [FromForm] string text, [FromForm] int points)
        {
            var question = new Question { AssignmentId = assignmentId, Text = text, Points = points };
            _context.Questions.Add(question);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Edit), new { id = assignmentId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "admin, instructor")]
        public async Task<IActionResult> AddOption([FromForm] int questionId, [FromForm] int assignmentId, [FromForm] string text, [FromForm] bool isCorrect)
        {
            // Enforce Single Correct Option Logic
            if (isCorrect)
            {
                var existingOptions = await _context.QuestionOptions
                    .Where(o => o.QuestionId == questionId && o.IsCorrect)
                    .ToListAsync();

                foreach (var opt in existingOptions)
                {
                    opt.IsCorrect = false;
                }
            }

            var option = new QuestionOption { QuestionId = questionId, Text = text, IsCorrect = isCorrect };
            _context.QuestionOptions.Add(option);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Edit), new { id = assignmentId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteQuestion(int id, int assignmentId)
        {
            var q = await _context.Questions.FindAsync(id);
            if (q != null)
            {
                _context.Questions.Remove(q);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Edit), new { id = assignmentId });
        }
        // GET: Assignments/Take/5
        public async Task<IActionResult> Take(int? id)
        {
            if (id == null) return NotFound();

            var assignment = await _context.Assignments
                .Include(a => a.Questions)
                .ThenInclude(q => q.Options)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (assignment == null) return NotFound();

            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier) ?? User.FindFirst("UserId");
            if (userIdClaim != null)
            {
                var userId = int.Parse(userIdClaim.Value);
                var existingSubmission = await _context.Submissions
                    .FirstOrDefaultAsync(s => s.AssignmentId == id && s.StudentId == userId);
                ViewBag.ExistingSubmission = existingSubmission;
            }

            return View(assignment);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize] // Ensure user is logged in
        public async Task<IActionResult> SubmitQuiz(int assignmentId, Dictionary<int, int> answers)
        {
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier) ?? User.FindFirst("UserId");
            if (userIdClaim == null) return Unauthorized();
            var userId = int.Parse(userIdClaim.Value);

            // Null check answers just in case
            if (answers == null) answers = new Dictionary<int, int>();

            // Simple Auto-Grading Logic
            int score = 0;
            var questions = await _context.Questions.Include(q => q.Options).Where(q => q.AssignmentId == assignmentId).ToListAsync();

            foreach (var q in questions)
            {
                if (answers.TryGetValue(q.Id, out int selectedOptionId))
                {
                    var correctOption = q.Options.FirstOrDefault(o => o.IsCorrect);
                    if (correctOption != null && correctOption.Id == selectedOptionId)
                    {
                        score += q.Points;
                    }
                }
            }

            var submission = new Submission
            {
                AssignmentId = assignmentId,
                StudentId = userId,
                Score = score,
                Content = "Quiz Submission (Auto-Graded)",
                SubmittedAt = DateTime.UtcNow
            };

            _context.Submissions.Add(submission);
            await _context.SaveChangesAsync();

            var assignment = await _context.Assignments.FindAsync(assignmentId);
            if (assignment != null)
            {
                return RedirectToAction("Details", "Courses", new { id = assignment.CourseId, tab = "quizzes" });
            }
            return RedirectToAction("Index", "Courses");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitAssignment(int assignmentId, string? content, List<IFormFile> files)
        {
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier) ?? User.FindFirst("UserId");
            if (userIdClaim == null) return Unauthorized();
            var userId = int.Parse(userIdClaim.Value);

            var attachments = new List<SubmissionAttachment>();
            string? firstFileUrl = null;

            // Handle Multiple File Uploads
            if (files != null && files.Count > 0)
            {
                var uploads = Path.Combine(_environment.WebRootPath, "uploads");
                if (!Directory.Exists(uploads)) Directory.CreateDirectory(uploads);

                foreach (var file in files)
                {
                    if (file.Length > 0)
                    {
                        var fileName = Guid.NewGuid() + Path.GetExtension(file.FileName);
                        var filePath = Path.Combine(uploads, fileName);
                        using (var stream = new FileStream(filePath, FileMode.Create))
                        {
                            await file.CopyToAsync(stream);
                        }
                        
                        var fileUrl = "/uploads/" + fileName;
                        if (firstFileUrl == null) firstFileUrl = fileUrl; // Keep first for legacy

                        attachments.Add(new SubmissionAttachment
                        {
                            FileName = file.FileName,
                            FilePath = fileUrl,
                            UploadedAt = DateTime.UtcNow
                        });
                    }
                }
            }

            var submission = new Submission
            {
                AssignmentId = assignmentId,
                StudentId = userId,
                Content = content ?? "",
                AttachmentUrl = firstFileUrl, // Legacy support
                Attachments = attachments,
                SubmittedAt = DateTime.UtcNow
            };

            _context.Submissions.Add(submission);
            await _context.SaveChangesAsync();

            var assignment = await _context.Assignments.FindAsync(assignmentId);
            if (assignment != null)
            {
                TempData["ShowSubmissionBanner"] = true;
                return RedirectToAction("Take", new { id = assignmentId });
            }
            return RedirectToAction("Index", "Courses");
        }

        // GET: Assignments/Submissions/5
        [Authorize(Roles = "admin, instructor")]
        public async Task<IActionResult> Submissions(int? id)
        {
            if (id == null) return NotFound();

            var assignment = await _context.Assignments.FindAsync(id);
            if (assignment == null) return NotFound();

            var submissions = await _context.Submissions
                .Include(s => s.Student)
                .Include(s => s.Attachments)
                .Where(s => s.AssignmentId == id)
                .OrderByDescending(s => s.SubmittedAt)
                .ToListAsync();

            ViewBag.Submissions = submissions;
            return View(assignment);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "admin, instructor")]
        public async Task<IActionResult> GradeSubmission(int submissionId, int score, string? feedback)
        {
            var submission = await _context.Submissions.FindAsync(submissionId);
            if (submission != null)
            {
                submission.Score = score;
                submission.Feedback = feedback;
                await _context.SaveChangesAsync();

                return RedirectToAction(nameof(Submissions), new { id = submission.AssignmentId });
            }
            return NotFound();
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "admin, instructor")]
        public async Task<IActionResult> Delete(int id)
        {
            var assignment = await _context.Assignments.FindAsync(id);
            if (assignment == null) return NotFound();

            // RBAC Check
            if (User.IsInRole("instructor"))
            {
                var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
                if (userIdClaim == null) return Forbid();
                var userId = int.Parse(userIdClaim.Value);
                if (assignment.CourseId != 0) // Should check instructor of the course
                {
                    var course = await _context.Courses.FindAsync(assignment.CourseId);
                    if (course == null || course.InstructorId != userId) return Forbid();
                }
            }

            var courseId = assignment.CourseId;
            var tab = assignment.Type == "quiz" ? "quizzes" : "assignments";
            
            _context.Assignments.Remove(assignment);
            await _context.SaveChangesAsync();
            
            return RedirectToAction("Details", "Courses", new { id = courseId, tab = tab });
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
