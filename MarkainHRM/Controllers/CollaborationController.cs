using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OzarkLMS.Data;
using OzarkLMS.Models;
using OzarkLMS.ViewModels;
using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;
using OzarkLMS.Hubs;

namespace OzarkLMS.Controllers
{
    [Authorize]
    public class CollaborationController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IHubContext<VoteHub> _voteHub;
        private readonly IWebHostEnvironment _environment;

        public CollaborationController(AppDbContext context, IHubContext<VoteHub> voteHub, IWebHostEnvironment environment)
        {
            _context = context;
            _voteHub = voteHub;
            _environment = environment;
        }

        private int GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier) ?? User.FindFirst("UserId");
            return userIdClaim != null ? int.Parse(userIdClaim.Value) : 0;
        }

        // GET: /Collaboration
        public async Task<IActionResult> Index()
        {
            var userId = GetCurrentUserId();
            var currentUser = await _context.Users.FindAsync(userId);

            if (currentUser == null) return RedirectToAction("Login", "Account");

            // Groups I'm in
            var chatGroups = await _context.ChatGroupMembers
                .Where(m => m.UserId == userId)
                .Include(m => m.ChatGroup)
                    .ThenInclude(g => g.Members)
                .Select(m => m.ChatGroup)
                .ToListAsync();

            // Private Chats
            var privateChats = await _context.PrivateChats
                .Include(p => p.User1)
                .Include(p => p.User2)
                .Where(p => p.User1Id == userId || p.User2Id == userId)
                .OrderByDescending(p => p.LastActivityDate)
                .ToListAsync();

            // Unread counts logic via Notifications (original design)
            var notifications = await _context.Notifications
                .Where(n => n.RecipientId == userId && !n.IsRead && n.ActionUrl != null && n.ActionUrl.StartsWith("/Collaboration/"))
                .ToListAsync();

            var groupUnread = new Dictionary<int, int>();
            foreach (var g in chatGroups)
            {
                int count = notifications.Count(n => n.ActionUrl == $"/Collaboration/Details/{g.Id}");
                groupUnread[g.Id] = count;
            }

            var privateUnread = new Dictionary<int, int>();
            foreach (var p in privateChats)
            {
                int count = notifications.Count(n => n.ActionUrl == $"/Collaboration/PrivateDetails/{p.Id}");
                privateUnread[p.Id] = count;
            }

            var viewModel = new SocialHubViewModel
            {
                CurrentUser = currentUser,
                Feed = new List<Post>(), // Social feed removed
                ChatGroups = chatGroups,
                PrivateChats = privateChats,
                GroupUnread = groupUnread,
                PrivateUnread = privateUnread
            };

            return View(viewModel);
        }

        // POST: /Collaboration/Vote
        // POST: /Collaboration/Vote
        [HttpPost]
        public async Task<IActionResult> Vote(int postId, int value)
        {
            // Value: 1 (Up), -1 (Down)
            if (value != 1 && value != -1) return BadRequest();

            var userId = GetCurrentUserId();

            // Transaction for Atomic Updates
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var post = await _context.Posts.FirstOrDefaultAsync(p => p.Id == postId);
                if (post == null) return NotFound();

                var existingVote = await _context.PostVotes.FirstOrDefaultAsync(pv => pv.PostId == postId && pv.UserId == userId);

                int userVote = 0; // Final state for the user
                int upvoteChange = 0;
                int downvoteChange = 0;

                if (existingVote != null)
                {
                    if (existingVote.Value == value)
                    {
                        // Toggle Off (Remove vote)
                        _context.PostVotes.Remove(existingVote);
                        userVote = 0;

                        if (value == 1) { upvoteChange = -1; }
                        else { downvoteChange = -1; }
                    }
                    else
                    {
                        // Switch Vote
                        userVote = value;
                        existingVote.Value = value;

                        if (value == 1) 
                        {
                            // Switching Down -> Up
                            upvoteChange = 1;
                            downvoteChange = -1;
                        }
                        else 
                        {
                            // Switching Up -> Down
                            upvoteChange = -1;
                            downvoteChange = 1;
                        }
                    }
                }
                else
                {
                    // New Vote
                    _context.PostVotes.Add(new PostVote { PostId = postId, UserId = userId, Value = value });
                    userVote = value;

                    if (value == 1) { upvoteChange = 1; }
                    else { downvoteChange = 1; }
                }

                // Update Post aggregates
                post.UpvoteCount += upvoteChange;
                post.DownvoteCount += downvoteChange;

                await _context.SaveChangesAsync();

                // Notification Logic (only for new upvotes)
                if (value == 1 && existingVote == null && post.UserId != userId)
                {
                     bool alreadyNotified = await _context.Notifications.AnyAsync(n => 
                            n.RecipientId == post.UserId && 
                            n.SenderId == userId && 
                            n.Title == "New Upvote" && 
                            n.ActionUrl.Contains($"#post-{postId}") &&
                            n.SentDate > DateTime.UtcNow.AddMinutes(-10));

                        if (!alreadyNotified)
                        {
                            _context.Notifications.Add(new Notification
                            {
                                RecipientId = post.UserId,
                                SenderId = userId,
                                Title = "New Upvote",
                                Message = $"{User.Identity.Name} upvoted your post.",
                                ActionUrl = $"/Collaboration#post-{postId}",
                                SentDate = DateTime.UtcNow
                            });
                             await _context.SaveChangesAsync();
                        }
                }

                await transaction.CommitAsync();

                // Broadcast Updated Score
                var newScore = post.UpvoteCount - post.DownvoteCount;
                await _voteHub.Clients.All.SendAsync("ReceiveVoteUpdate", postId, newScore, userVote, userId);

                return Json(new { score = newScore, userVote = userVote });
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        // POST: /Collaboration/AddComment
        // POST: /Collaboration/AddComment
        [HttpPost]
        public async Task<IActionResult> AddComment(int postId, string content, int? parentCommentId = null)
        {
            if (string.IsNullOrWhiteSpace(content)) return BadRequest();

            var userId = GetCurrentUserId();
            var comment = new PostComment
            {
                PostId = postId,
                UserId = userId,
                Content = content,
                ParentCommentId = parentCommentId,
                CreatedAt = DateTime.UtcNow
            };

            _context.PostComments.Add(comment);

            // Notifications
            var post = await _context.Posts.FindAsync(postId);
            if (post != null && post.UserId != userId)
            {
                // Notify Post Owner
                _context.Notifications.Add(new Notification
                {
                    RecipientId = post.UserId,
                    SenderId = userId,
                    Title = "New Comment",
                    Message = $"{User.Identity.Name} commented on your post.",
                    ActionUrl = $"/Collaboration#comment-{comment.Id}", // Link to specific comment
                    SentDate = DateTime.UtcNow
                });
            }

            if (parentCommentId.HasValue)
            {
                var parent = await _context.PostComments.FindAsync(parentCommentId.Value);
                if (parent != null && parent.UserId != userId && (post == null || parent.UserId != post.UserId)) // Avoid double notify if post owner == comment owner
                {
                    // Notify Parent Comment Owner
                     _context.Notifications.Add(new Notification
                    {
                        RecipientId = parent.UserId,
                        SenderId = userId,
                        Title = "New Reply",
                        Message = $"{User.Identity.Name} replied to your comment.",
                        ActionUrl = $"/Collaboration#comment-{comment.Id}",
                        SentDate = DateTime.UtcNow
                    });
                }
            }

            await _context.SaveChangesAsync();

            var user = await _context.Users.FindAsync(userId);
            
            var commentData = new { 
                success = true, 
                user = user.Username, 
                avatar = user.ProfilePictureUrl, 
                content = comment.Content, 
                date = comment.CreatedAt.ToLocalTime().ToString("MMM dd HH:mm"),
                parentId = parentCommentId,
                id = comment.Id
            };

            await _voteHub.Clients.All.SendAsync("ReceiveNewComment", postId, commentData);

            return Json(commentData);
        }

        // POST: /Collaboration/VoteComment
        [HttpPost]
        public async Task<IActionResult> VoteComment(int commentId, int value)
        {
            // Value: 1 (Up), -1 (Down)
            if (value != 1 && value != -1) return BadRequest();

            var userId = GetCurrentUserId();

            // Transaction for Atomic Updates
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var comment = await _context.PostComments.FirstOrDefaultAsync(c => c.Id == commentId);
                if (comment == null) return NotFound();

                var existingVote = await _context.PostCommentVotes.FirstOrDefaultAsync(cv => cv.CommentId == commentId && cv.UserId == userId);

                int userVote = 0;
                int upvoteChange = 0;
                int downvoteChange = 0;

                if (existingVote != null)
                {
                    if (existingVote.Value == value)
                    {
                        // Toggle Off
                        _context.PostCommentVotes.Remove(existingVote);
                        userVote = 0;

                        if (value == 1) { upvoteChange = -1; }
                        else { downvoteChange = -1; }
                    }
                    else
                    {
                        // Switch Vote
                        userVote = value;
                        existingVote.Value = value;

                        if (value == 1) 
                        {
                            upvoteChange = 1;
                            downvoteChange = -1;
                        }
                        else 
                        {
                            upvoteChange = -1;
                            downvoteChange = 1;
                        }
                    }
                }
                else
                {
                    // New Vote
                    _context.PostCommentVotes.Add(new PostCommentVote { CommentId = commentId, UserId = userId, Value = value });
                    userVote = value;

                    if (value == 1) { upvoteChange = 1; }
                    else { downvoteChange = 1; }
                }

                // Update Comment aggregates
                comment.UpvoteCount += upvoteChange;
                comment.DownvoteCount += downvoteChange;

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                // Broadcast Updated Score
                var newScore = comment.UpvoteCount - comment.DownvoteCount;
                await _voteHub.Clients.All.SendAsync("ReceiveCommentVoteUpdate", commentId, newScore, userVote, userId);

                return Json(new { score = newScore, userVote = userVote });
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        // POST: /Collaboration/EditComment
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditComment(int commentId, string content)
        {
            if (string.IsNullOrWhiteSpace(content)) return BadRequest();
            var userId = GetCurrentUserId();
            var comment = await _context.PostComments.FindAsync(commentId);
            
            if (comment == null) return NotFound();
            if (comment.UserId != userId) return Forbid();

            comment.Content = content;
            // comment.LastEditedDate = DateTime.UtcNow; // If model supported it
            await _context.SaveChangesAsync();
            
            
            await _voteHub.Clients.All.SendAsync("ReceiveCommentEdited", commentId, comment.Content);

            return Json(new { success = true, content = comment.Content });
        }

        // POST: /Collaboration/DeleteComment
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteComment(int commentId)
        {
            var userId = GetCurrentUserId();
            var comment = await _context.PostComments.FindAsync(commentId);
            
            if (comment == null) return NotFound();
            if (comment.UserId != userId && !User.IsInRole("admin")) return Forbid();

            _context.PostComments.Remove(comment);
            await _context.SaveChangesAsync();
            
            await _voteHub.Clients.All.SendAsync("ReceiveCommentDeleted", commentId, comment.PostId);

            return Json(new { success = true });
        }

        // POST: /Collaboration/CreatePost
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreatePost(string? content, IFormFile? media, IFormFile? attachment)
        {
            if (string.IsNullOrWhiteSpace(content) && media == null && attachment == null) 
                return RedirectToAction(nameof(Index));

            var userId = GetCurrentUserId();

            string? imageUrl = null;
            string? attachmentUrl = null;

            // Handle Image
            if (media != null && media.Length > 0)
            {
                var uploads = Path.Combine(_environment.WebRootPath, "uploads");
                if (!Directory.Exists(uploads)) Directory.CreateDirectory(uploads);
                var fileName = Guid.NewGuid().ToString() + Path.GetExtension(media.FileName);
                await using (var stream = new FileStream(Path.Combine(uploads, fileName), FileMode.Create))
                {
                    await media.CopyToAsync(stream);
                }
                imageUrl = "/uploads/" + fileName;
            }

            // Handle Doc Attachment (Reuse logic or separate?)
            // For now let's just use 'attachment' arg if provided
            if (attachment != null && attachment.Length > 0)
            {
                 var uploads = Path.Combine(_environment.WebRootPath, "uploads");
                if (!Directory.Exists(uploads)) Directory.CreateDirectory(uploads);
                var fileName = Guid.NewGuid().ToString() + Path.GetExtension(attachment.FileName);
                await using (var stream = new FileStream(Path.Combine(uploads, fileName), FileMode.Create))
                {
                    await attachment.CopyToAsync(stream);
                }
                attachmentUrl = "/uploads/" + fileName;
            }

            var post = new Post
            {
                UserId = userId,
                Content = content ?? "",
                ImageUrl = imageUrl,
                AttachmentUrl = attachmentUrl,
                CreatedAt = DateTime.UtcNow
            };

            // Requirement: By default all posts have 1 upvote (from the author)
            post.Votes.Add(new OzarkLMS.Models.PostVote { UserId = userId, Value = 1 });

            _context.Posts.Add(post);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        // POST: /Collaboration/CreateGroup
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateGroup(string name, string description, IFormFile? photo)
        {
            if (string.IsNullOrWhiteSpace(name)) return RedirectToAction(nameof(Index));

            var userId = GetCurrentUserId();
            string? photoUrl = null;

            if (photo != null && photo.Length > 0)
            {
                var uploads = Path.Combine(_environment.WebRootPath, "uploads");
                if (!Directory.Exists(uploads)) Directory.CreateDirectory(uploads);
                var fileName = Guid.NewGuid().ToString() + Path.GetExtension(photo.FileName);
                await using (var stream = new FileStream(Path.Combine(uploads, fileName), FileMode.Create))
                {
                    await photo.CopyToAsync(stream);
                }
                photoUrl = "/uploads/" + fileName;
            }

            var group = new ChatGroup
            {
                Name = name,
                Description = description ?? "",
                CreatedById = userId,
                OwnerId = userId, // Creator is owner
                IsDefault = false,
                GroupPhotoUrl = photoUrl
            };

            _context.ChatGroups.Add(group);
            await _context.SaveChangesAsync();

            // Creator is automatically a member
            _context.ChatGroupMembers.Add(new ChatGroupMember
            {
                ChatGroupId = group.Id,
                UserId = userId,
                JoinedDate = DateTime.UtcNow,
                ViewMode = "List"
            });
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Details), new { id = group.Id });
        }

        // GET: /Collaboration/Details/5
        public async Task<IActionResult> Details(int id)
        {
            var userId = GetCurrentUserId();
            var isAdmin = User.IsInRole("admin");

            var group = await _context.ChatGroups
                .Include(g => g.Messages)
                    .ThenInclude(m => m.Sender)
                .Include(g => g.Members)
                    .ThenInclude(m => m.User) // Include User info for members list
                .FirstOrDefaultAsync(g => g.Id == id);

            if (group == null) return NotFound();

            // Check Visibility
            var isMember = group.Members.Any(m => m.UserId == userId);
            if (!isMember)
            {
                // Requirement: "Group chats are visible only to users who are members" (EXCEPT Admin)
                if (!isAdmin)
                {
                    // If it's a default chat and they aren't a member (rare race condition or bug), fix it
                    if (group.IsDefault)
                    {
                        _context.ChatGroupMembers.Add(new ChatGroupMember { ChatGroupId = group.Id, UserId = userId, JoinedDate = DateTime.UtcNow });
                        await _context.SaveChangesAsync();
                        isMember = true;
                    }
                    else
                    {
                        return Forbid();
                    }
                }
            }

            // Mark notifications as read for this group
            var unreadNotes = await _context.Notifications
                .Where(n => n.RecipientId == userId && !n.IsRead && n.ActionUrl == $"/Collaboration/Details/{id}")
                .ToListAsync();
            
            if (unreadNotes.Any())
            {
                foreach(var note in unreadNotes) note.IsRead = true;
                await _context.SaveChangesAsync();
            }

            return View(group);
        }

        // POST: /Collaboration/PostMessage
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PostMessage(int groupId, string? message, IFormFile? file)
        {
            if (string.IsNullOrWhiteSpace(message) && file == null) return RedirectToAction(nameof(Details), new { id = groupId });

            var userId = GetCurrentUserId();
            // Validate membership/access again
            var group = await _context.ChatGroups.FindAsync(groupId);
            if (group == null) return NotFound();

            var isMember = await _context.ChatGroupMembers.AnyAsync(m => m.ChatGroupId == groupId && m.UserId == userId);
            if (!isMember && !User.IsInRole("admin")) return Forbid();

            string? attachmentUrl = null;
            string? originalName = null;
            string? contentType = null;
            long size = 0;

            if (file != null && file.Length > 0)
            {
                var uploads = Path.Combine(_environment.WebRootPath, "uploads");
                if (!Directory.Exists(uploads)) Directory.CreateDirectory(uploads);

                var fileName = Guid.NewGuid().ToString() + Path.GetExtension(file.FileName);
                var filePath = Path.Combine(uploads, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }
                attachmentUrl = "/uploads/" + fileName;
                originalName = file.FileName;
                contentType = file.ContentType;
                size = file.Length;
            }

            var chatMessage = new ChatMessage
            {
                GroupId = groupId,
                SenderId = userId,
                Message = message ?? (file != null ? "" : ""), // Allowing empty message if file exists
                AttachmentUrl = attachmentUrl,
                AttachmentOriginalName = originalName,
                AttachmentContentType = contentType,
                AttachmentSize = size,
                SentDate = DateTime.UtcNow
            };

            _context.ChatMessages.Add(chatMessage);

            // Update LastActivityDate
            group.LastActivityDate = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            // Notify Group Members (excluding sender)
            var otherMembers = await _context.ChatGroupMembers
                .Where(m => m.ChatGroupId == groupId && m.UserId != userId)
                .Select(m => m.UserId)
                .ToListAsync();

            var notifications = otherMembers.Select(memberId => new Notification
            {
                RecipientId = memberId,
                SenderId = userId,
                Title = $"New Message in {group.Name}",
                Message = message?.Length > 50 ? message.Substring(0, 47) + "..." : (string.IsNullOrWhiteSpace(message) ? "Sent an attachment" : message),
                SentDate = DateTime.UtcNow,
                IsRead = false,
                ActionUrl = $"/Collaboration/Details/{groupId}"
            });

            _context.Notifications.AddRange(notifications);
            await _context.SaveChangesAsync();

            // Send SignalR notification for chat list reordering
            await _voteHub.Clients.All.SendAsync("ReceiveChatUpdate", groupId, false, group.LastActivityDate.ToString("o"));

            return RedirectToAction(nameof(Details), new { id = groupId });
        }

        // POST: /Collaboration/EditMessage
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditMessage(int messageId, string newContent)
        {
            var userId = GetCurrentUserId();
            var message = await _context.ChatMessages.Include(m => m.Group).FirstOrDefaultAsync(m => m.Id == messageId);

            if (message == null) return NotFound();
            if (message.SenderId != userId) return Forbid();

            // Store edit history? Not required yet.
            message.Message = newContent;
            message.LastEditedDate = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Details), new { id = message.Group.Id });
        }

        // POST: /Collaboration/EditPost
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditPost(int postId, string newContent)
        {
            var userId = GetCurrentUserId();
            var post = await _context.Posts.FindAsync(postId);
            if (post == null) return NotFound();

            if (post.UserId != userId && !User.IsInRole("admin")) return Forbid();

            post.Content = newContent;
            // post.LastEditedDate = DateTime.UtcNow; // If model supports it
            await _context.SaveChangesAsync();

            await _voteHub.Clients.All.SendAsync("ReceivePostEdited", postId, newContent);

            return RedirectToAction(nameof(Index));
        }

        // POST: /Collaboration/ShareOnFeed
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ShareOnFeed(int postId)
        {
            var userId = GetCurrentUserId();
            var post = await _context.Posts.FindAsync(postId);
            if (post == null) return NotFound();

            // Check if already shared
            var existingShare = await _context.SharedPosts
                .FirstOrDefaultAsync(sp => sp.UserId == userId && sp.PostId == postId);

            if (existingShare != null)
            {
                return Json(new { success = false, message = "You already shared this post." });
            }

            var sharedPost = new SharedPost
            {
                UserId = userId,
                PostId = postId,
                SharedAt = DateTime.UtcNow
            };

            _context.SharedPosts.Add(sharedPost);
            await _context.SaveChangesAsync();

            // Optional: Notify Post owner
            if (post.UserId != userId)
            {
                _context.Notifications.Add(new Notification
                {
                    RecipientId = post.UserId,
                    SenderId = userId,
                    Title = "Post Shared",
                    Message = $"{User.Identity.Name} shared your post to their feed.",
                    ActionUrl = $"/Account/Profile?userId={userId}",
                    SentDate = DateTime.UtcNow
                });
                await _context.SaveChangesAsync();
            }

            return Json(new { success = true });
        }

        // POST: /Collaboration/DeletePost
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeletePost(int postId)
        {
            var userId = GetCurrentUserId();
            var post = await _context.Posts.FindAsync(postId);
            if (post == null) return NotFound();

            if (post.UserId != userId && !User.IsInRole("admin")) return Forbid();

            _context.Posts.Remove(post);
            await _context.SaveChangesAsync();

            await _voteHub.Clients.All.SendAsync("ReceivePostDeleted", postId);

            return RedirectToAction(nameof(Index));
        }


        // POST: /Collaboration/DeleteMessage
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteMessage(int messageId)
        {
            var userId = GetCurrentUserId();
            var message = await _context.ChatMessages.Include(m => m.Group).FirstOrDefaultAsync(m => m.Id == messageId);

            if (message == null) return NotFound();

            // Sender can delete. Admin maybe? Requirement says "Delete their own messages". 
            // Let's allow Admin too just in case, but primary is sender.
            bool isAdmin = User.IsInRole("admin");
            if (message.SenderId != userId && !isAdmin) return Forbid();

            // Soft Delete
            message.IsDeleted = true;
            // Clear content for compliance/safety (optional but good practice for soft delete if we want to hide it completely)
            message.Message = "";
            message.AttachmentUrl = null;

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Details), new { id = message.GroupId });
        }

        // GET: /Collaboration/StartPrivateChat?targetUserId=5
        [HttpGet]
        public async Task<IActionResult> StartPrivateChat(int targetUserId)
        {
            var userId = GetCurrentUserId();
            if (targetUserId == userId) return RedirectToAction(nameof(Index));

            var targetUser = await _context.Users.FindAsync(targetUserId);
            if (targetUser == null) return RedirectToAction(nameof(Index));

            // Check for existing chat
            var existingChat = await _context.PrivateChats
                .FirstOrDefaultAsync(c => (c.User1Id == userId && c.User2Id == targetUser.Id) ||
                                          (c.User1Id == targetUser.Id && c.User2Id == userId));

            if (existingChat != null)
            {
                return RedirectToAction("PrivateDetails", new { id = existingChat.Id });
            }

            // Create new Private Chat
            var newChat = new PrivateChat
            {
                User1Id = userId,
                User2Id = targetUser.Id,
                LastActivityDate = DateTime.UtcNow
            };

            _context.PrivateChats.Add(newChat);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(PrivateDetails), new { id = newChat.Id });
        }

        // POST: /Collaboration/StartPrivateChat
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> StartPrivateChatPost(string targetUsername)
        {
            var userId = GetCurrentUserId();
            var targetUser = await _context.Users.FirstOrDefaultAsync(u => u.Username == targetUsername);

            if (targetUser == null) return RedirectToAction(nameof(Index));
            if (targetUser.Id == userId) return RedirectToAction(nameof(Index));

            var existingChat = await _context.PrivateChats
                .FirstOrDefaultAsync(c => (c.User1Id == userId && c.User2Id == targetUser.Id) ||
                                          (c.User1Id == targetUser.Id && c.User2Id == userId));

            if (existingChat != null)
            {
                return RedirectToAction("PrivateDetails", new { id = existingChat.Id });
            }

            var newChat = new PrivateChat
            {
                User1Id = userId,
                User2Id = targetUser.Id,
                LastActivityDate = DateTime.UtcNow
            };

            _context.PrivateChats.Add(newChat);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(PrivateDetails), new { id = newChat.Id });
        }

        // GET: /Collaboration/PrivateDetails/5
        public async Task<IActionResult> PrivateDetails(int id)
        {
            var userId = GetCurrentUserId();
            var chat = await _context.PrivateChats
                .Include(c => c.User1)
                .Include(c => c.User2)
                .Include(c => c.Messages)
                    .ThenInclude(m => m.Sender)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (chat == null) return NotFound();

            // Security Check
            if (chat.User1Id != userId && chat.User2Id != userId && !User.IsInRole("admin"))
            {
                return Forbid();
            }

            // Mark notifications as read for this private chat
            var unreadNotes = await _context.Notifications
                .Where(n => n.RecipientId == userId && !n.IsRead && n.ActionUrl == $"/Collaboration/PrivateDetails/{id}")
                .ToListAsync();
            
            if (unreadNotes.Any())
            {
                foreach(var note in unreadNotes) note.IsRead = true;
                await _context.SaveChangesAsync();
            }

            return View(chat);
        }

        // POST: /Collaboration/PostPrivateMessage
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PostPrivateMessage(int chatId, string? message, IFormFile? file)
        {
            if (string.IsNullOrWhiteSpace(message) && file == null) return RedirectToAction(nameof(PrivateDetails), new { id = chatId });

            var userId = GetCurrentUserId();
            var chat = await _context.PrivateChats.FindAsync(chatId);
            if (chat == null) return NotFound();

            if (chat.User1Id != userId && chat.User2Id != userId && !User.IsInRole("admin")) return Forbid();

            string? attachmentUrl = null;
            string? originalName = null;
            string? contentType = null;
            long size = 0;

            if (file != null && file.Length > 0)
            {
                var uploads = Path.Combine(_environment.WebRootPath, "uploads");
                if (!Directory.Exists(uploads)) Directory.CreateDirectory(uploads);

                var fileName = Guid.NewGuid().ToString() + Path.GetExtension(file.FileName);
                var filePath = Path.Combine(uploads, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }
                attachmentUrl = "/uploads/" + fileName;
                originalName = file.FileName;
                contentType = file.ContentType;
                size = file.Length;
            }

            var privateMessage = new PrivateMessage
            {
                PrivateChatId = chatId,
                SenderId = userId,
                Message = message ?? (file != null ? "" : ""),
                AttachmentUrl = attachmentUrl,
                AttachmentOriginalName = originalName,
                AttachmentContentType = contentType,
                AttachmentSize = size,
                SentDate = DateTime.UtcNow
            };

            _context.PrivateMessages.Add(privateMessage);
            chat.LastActivityDate = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            // Notify Recipient
            var recipientId = chat.User1Id == userId ? chat.User2Id : chat.User1Id;
            var sender = await _context.Users.FindAsync(userId);
            var senderName = sender?.Username ?? "Someone";

            _context.Notifications.Add(new Notification
            {
                RecipientId = recipientId,
                SenderId = userId,
                Title = $"Private Message from {senderName}",
                Message = message?.Length > 50 ? message.Substring(0, 47) + "..." : (string.IsNullOrWhiteSpace(message) ? "Sent an attachment" : message),
                SentDate = DateTime.UtcNow,
                IsRead = false,
                ActionUrl = $"/Collaboration/PrivateDetails/{chatId}"
            });
            await _context.SaveChangesAsync();

            // Send SignalR notification for chat list reordering
            await _voteHub.Clients.All.SendAsync("ReceiveChatUpdate", chatId, true, chat.LastActivityDate.ToString("o"));

            return RedirectToAction(nameof(PrivateDetails), new { id = chatId });
        }

        // POST: /Collaboration/DeletePrivateMessage
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeletePrivateMessage(int messageId)
        {
            var userId = GetCurrentUserId();
            var message = await _context.PrivateMessages.Include(m => m.Chat).FirstOrDefaultAsync(m => m.Id == messageId);

            if (message == null) return NotFound();
            if (message.SenderId != userId && !User.IsInRole("admin")) return Forbid();

            message.IsDeleted = true;
            message.Message = "";
            message.AttachmentUrl = null;

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(PrivateDetails), new { id = message.Chat.Id });
        }

        // POST: /Collaboration/EditPrivateMessage
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditPrivateMessage(int messageId, string newContent)
        {
            var userId = GetCurrentUserId();
            var message = await _context.PrivateMessages.Include(m => m.Chat).FirstOrDefaultAsync(m => m.Id == messageId);

            if (message == null) return NotFound();
            if (message.SenderId != userId) return Forbid();

            message.Message = newContent;
            message.LastEditedDate = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(PrivateDetails), new { id = message.Chat.Id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleViewMode(int groupId, string viewMode)
        {
            var userId = GetCurrentUserId();
            var member = await _context.ChatGroupMembers.FirstOrDefaultAsync(m => m.ChatGroupId == groupId && m.UserId == userId);

            if (member != null)
            {
                member.ViewMode = viewMode;
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Details), new { id = groupId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateGroupPhoto(int groupId, IFormFile? photo, string? photoUrl)
        {
            var group = await _context.ChatGroups.FindAsync(groupId);
            if (group == null) return NotFound();

            var currentUserId = GetCurrentUserId();
            bool isOwner = group.OwnerId == currentUserId;
            bool isAdmin = User.IsInRole("admin");

            if (!isOwner && !isAdmin) return Forbid();

            if (photo != null && photo.Length > 0)
            {
                var uploads = Path.Combine(_environment.WebRootPath, "uploads");
                if (!Directory.Exists(uploads)) Directory.CreateDirectory(uploads);
                var fileName = Guid.NewGuid().ToString() + Path.GetExtension(photo.FileName);
                await using (var stream = new FileStream(Path.Combine(uploads, fileName), FileMode.Create))
                {
                    await photo.CopyToAsync(stream);
                }
                group.GroupPhotoUrl = "/uploads/" + fileName;
            }
            else if (!string.IsNullOrEmpty(photoUrl))
            {
                group.GroupPhotoUrl = photoUrl;
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Details), new { id = groupId });
        }

        // POST: /Collaboration/AddMember
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddMember(int groupId, string username)
        {
            var targetUser = await _context.Users.FirstOrDefaultAsync(u => u.Username == username);
            if (targetUser == null) return RedirectToAction(nameof(Details), new { id = groupId });

            var group = await _context.ChatGroups.Include(g => g.Members).FirstOrDefaultAsync(g => g.Id == groupId);
            if (group == null) return NotFound();

            var currentUserId = GetCurrentUserId();

            // Check permissions: Creator/Owner can add members.
            // Requirement 2: "The creator can add or remove members" (We interpreted Creator as Owner here)
            // Also Admin? It says "Only admin accounts can manage or remove [the default chat]". 
            // For custom chats, Owner can manage.
            bool isOwner = group.OwnerId == currentUserId;
            bool isAdmin = User.IsInRole("admin");

            if (group.IsDefault)
            {
                // Only Admin can manage Default Chat
                if (!isAdmin) return Forbid();
            }
            else
            {
                // Only Owner or Admin can manage Custom Chat
                if (!isOwner && !isAdmin) return Forbid();
            }

            if (!group.Members.Any(m => m.UserId == targetUser.Id))
            {
                _context.ChatGroupMembers.Add(new ChatGroupMember
                {
                    ChatGroupId = groupId,
                    UserId = targetUser.Id
                });

                // Notification
                var senderName = User.Identity!.Name;
                _context.Notifications.Add(new Notification
                {
                    RecipientId = targetUser.Id,
                    Title = "Added to Group",
                    Message = $"{senderName} added you to '{group.Name}'",
                    SenderId = currentUserId
                });

                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Details), new { id = groupId });
        }

        // POST: /Collaboration/RemoveMember
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveMember(int groupId, int memberId)
        {
            var group = await _context.ChatGroups.Include(g => g.Members).FirstOrDefaultAsync(g => g.Id == groupId);
            if (group == null) return NotFound();

            var currentUserId = GetCurrentUserId();
            bool isOwner = group.OwnerId == currentUserId;
            bool isAdmin = User.IsInRole("admin");

            if (group.IsDefault)
            {
                if (!isAdmin) return Forbid();
            }
            else
            {
                if (!isOwner && !isAdmin) return Forbid();
            }

            // Cannot remove Owner (Owner must leave or transfer ownership first, or delete group)
            if (memberId == group.OwnerId)
            {
                // Maybe show error "Owner cannot be removed"?
                return RedirectToAction(nameof(Details), new { id = groupId });
            }

            var member = group.Members.FirstOrDefault(m => m.UserId == memberId);
            if (member != null)
            {
                _context.ChatGroupMembers.Remove(member);
                // Notification?
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Details), new { id = groupId });
        }

        // POST: /Collaboration/LeaveGroup
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> LeaveGroup(int groupId)
        {
            var userId = GetCurrentUserId();
            var group = await _context.ChatGroups.Include(g => g.Members).FirstOrDefaultAsync(g => g.Id == groupId);
            if (group == null) return NotFound();

            // Requirement 1: "Cannot be deleted or left" (for Default Chat)
            if (group.IsDefault)
            {
                return BadRequest("Cannot leave the default organization chat.");
            }

            var membership = group.Members.FirstOrDefault(m => m.UserId == userId);
            if (membership == null) return RedirectToAction(nameof(Index)); // Already not a member

            _context.ChatGroupMembers.Remove(membership);
            await _context.SaveChangesAsync();

            // Ownership Transfer Logic
            // Requirement 4: "If the creator [Owner] of a group chat leaves... ownership is automatically transferred"
            if (group.OwnerId == userId)
            {
                // Get remaining members
                // Re-fetch members to be safe since we just removed one? 
                // Tracking should handle it, but let's look at the in-memory list which now excludes the removed one (if using EF correctly)
                // Actually `membership` was removed from context, but `group.Members` list might still have it depending on tracking.
                // Safest to query DB for remaining members

                var remainingMembers = await _context.ChatGroupMembers
                    .Where(m => m.ChatGroupId == groupId && m.UserId != userId)
                    .OrderBy(m => m.JoinedDate) // "based on join order"
                    .ToListAsync();

                if (remainingMembers.Any())
                {
                    // Transfer to earliest added member
                    group.OwnerId = remainingMembers.First().UserId;
                }
                else
                {
                    // No members left. Delete group? 
                    // Requirement doesn't explicitly say to delete, but empty groups are useless.
                    // "A group chat can be deleted only by..." -> implied manual. 
                    // But if no one is left, it's orphaned. Let's delete it to keep DB clean.
                    _context.ChatGroups.Remove(group);
                }
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Index));
        }

        // POST: /Collaboration/DeleteGroup
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteGroup(int groupId)
        {
            var group = await _context.ChatGroups.FindAsync(groupId);
            if (group == null) return NotFound();

            var currentUserId = GetCurrentUserId();
            bool isOwner = group.OwnerId == currentUserId;
            bool isAdmin = User.IsInRole("admin");

            // Requirement 3: "A group chat can be deleted only by The account that created it [Owner], or An admin account"
            // Also Requirement 1: Default chat "Cannot be deleted" (except maybe by Admin? "Only admin accounts can manage or remove it")

            if (group.IsDefault)
            {
                return BadRequest("The default organization chat cannot be deleted.");
            }

            if (!isAdmin) return Forbid();

            // Explicitly remove all messages and members to ensure complete cleanup
            var messages = _context.ChatMessages.Where(m => m.GroupId == groupId);
            _context.ChatMessages.RemoveRange(messages);

            var members = _context.ChatGroupMembers.Where(m => m.ChatGroupId == groupId);
            _context.ChatGroupMembers.RemoveRange(members);

            _context.ChatGroups.Remove(group);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }
        // GET: /Collaboration/SearchUsers
        [HttpGet]
        public async Task<IActionResult> SearchUsers(string query)
        {
            if (string.IsNullOrWhiteSpace(query)) return Json(new List<object>());

            var users = await _context.Users
                .Where(u => u.Username.ToLower().Contains(query.ToLower()))
                .Select(u => new
                {
                    id = u.Id,
                    username = u.Username,
                    profilePictureUrl = u.ProfilePictureUrl
                })
                .Take(10)
                .ToListAsync();

            return Json(users);
        }

        [HttpGet]
        public async Task<IActionResult> GetUserChats()
        {
            var userId = GetCurrentUserId();
            
            // Get Groups
            var groups = await _context.ChatGroupMembers
                .Where(m => m.UserId == userId)
                .Select(m => new { 
                    id = m.ChatGroupId, 
                    name = m.ChatGroup.Name, 
                    isPrivate = false, 
                    photoUrl = m.ChatGroup.GroupPhotoUrl 
                })
                .ToListAsync();

            // Get Private Chats
            var privateChats = await _context.PrivateChats
                .Include(c => c.User1)
                .Include(c => c.User2)
                .Where(c => c.User1Id == userId || c.User2Id == userId)
                .Select(c => new {
                    id = c.Id,
                    name = c.User1Id == userId ? c.User2.Username : c.User1.Username,
                    isPrivate = true,
                    photoUrl = c.User1Id == userId ? c.User2.ProfilePictureUrl : c.User1.ProfilePictureUrl
                })
                .ToListAsync();

            var allChats = groups.Cast<object>().Concat(privateChats.Cast<object>()).ToList();
            return Json(allChats);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ShareToChat(int postId, int chatId, bool isPrivate)
        {
            var userId = GetCurrentUserId();
            var post = await _context.Posts.Include(p => p.User).FirstOrDefaultAsync(p => p.Id == postId);
            if (post == null) return Json(new { success = false, message = "Post not found" });

            string shareLink = $"{Request.Scheme}://{Request.Host}/Collaboration#post-{postId}";
            string truncatedContent = post.Content.Length > 100 ? post.Content.Substring(0, 97) + "..." : post.Content;
            string body = $"[POST_SHARE]|{post.User.Username}|{postId}|{truncatedContent}|{shareLink}";

            if (isPrivate)
            {
                var chat = await _context.PrivateChats.FindAsync(chatId);
                if (chat == null || (chat.User1Id != userId && chat.User2Id != userId)) return Forbid();

                var msg = new PrivateMessage
                {
                    PrivateChatId = chatId,
                    SenderId = userId,
                    Message = body,
                    SentDate = DateTime.UtcNow
                };
                _context.PrivateMessages.Add(msg);
                chat.LastActivityDate = DateTime.UtcNow;

                // Notification
                var recipientId = chat.User1Id == userId ? chat.User2Id : chat.User1Id;
                _context.Notifications.Add(new Notification {
                    RecipientId = recipientId,
                    SenderId = userId,
                    Title = "Shared a post with you",
                    Message = "Sent a post link in your private chat",
                    SentDate = DateTime.UtcNow,
                    ActionUrl = $"/Collaboration/PrivateDetails/{chatId}"
                });
            }
            else
            {
                var group = await _context.ChatGroups.FindAsync(chatId);
                if (group == null) return NotFound();
                var isMember = await _context.ChatGroupMembers.AnyAsync(m => m.ChatGroupId == chatId && m.UserId == userId);
                if (!isMember && !User.IsInRole("admin")) return Forbid();

                var msg = new ChatMessage
                {
                    GroupId = chatId,
                    SenderId = userId,
                    Message = body,
                    SentDate = DateTime.UtcNow
                };
                _context.ChatMessages.Add(msg);
                group.LastActivityDate = DateTime.UtcNow;

                // Notifications for members
                var members = await _context.ChatGroupMembers
                    .Where(m => m.ChatGroupId == chatId && m.UserId != userId)
                    .Select(m => m.UserId)
                    .ToListAsync();

                foreach (var memberId in members)
                {
                    _context.Notifications.Add(new Notification {
                        RecipientId = memberId,
                        SenderId = userId,
                        Title = $"Post shared in {group.Name}",
                        Message = "Shared a post link in the group",
                        SentDate = DateTime.UtcNow,
                        ActionUrl = $"/Collaboration/Details/{chatId}"
                    });
                }
            }

            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }

        [HttpGet]
        public async Task<IActionResult> SearchGlobal(string query)
        {
            try 
            {
                if (string.IsNullOrWhiteSpace(query))
                    return Json(new List<object>());

                int currentUserId = GetCurrentUserId();
                var searchTerm = $"%{query.Trim()}%";

                // Users search
                var users = await _context.Users
                    .IgnoreQueryFilters()
                    .Where(u => u.Id != currentUserId && 
                                u.Username != null && 
                                EF.Functions.ILike(u.Username, searchTerm))
                    .Select(u => new { type = "user", id = u.Id, name = u.Username, photo = u.ProfilePictureUrl })
                    .OrderBy(u => u.name)
                    .Take(10)
                    .ToListAsync();

                // Groups search
                var groups = await _context.ChatGroups
                    .Where(g => g.Name != null && EF.Functions.ILike(g.Name, searchTerm))
                    .Select(g => new { type = "group", id = g.Id, name = g.Name, photo = g.GroupPhotoUrl })
                    .OrderBy(g => g.name)
                    .Take(10)
                    .ToListAsync();

                var results = new List<object>();
                results.AddRange(users);
                results.AddRange(groups);
                return Json(results);
            }
            catch (Exception)
            {
                // Return empty list on error to keep frontend stable
                return Json(new List<object>());
            }
        }

        [HttpGet]
        public async Task<IActionResult> SearchDiagnostic()
        {
            try {
                var userCount = await _context.Users.CountAsync();
                var groupCount = await _context.ChatGroups.CountAsync();
                var sampleUser = await _context.Users.Select(u => u.Username).FirstOrDefaultAsync();
                var sampleGroup = await _context.ChatGroups.Select(g => g.Name).FirstOrDefaultAsync();
                
                return Json(new { 
                    status = "OK", 
                    userCount, 
                    groupCount, 
                    sampleUser, 
                    sampleGroup,
                    db = "Connected" 
                });
            } catch (Exception ex) {
                return Json(new { status = "ERROR", message = ex.Message, stack = ex.StackTrace });
            }
        }
    }
}
