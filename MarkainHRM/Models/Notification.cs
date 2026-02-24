using System;

namespace OzarkLMS.Models
{
    public class Notification
    {
        public int Id { get; set; }

        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;

        public int? SenderId { get; set; }
        public int? RecipientId { get; set; } // Null = Broadcast to all (if implemented later) or specific user

        public DateTime SentDate { get; set; } = DateTime.UtcNow;
        public bool IsRead { get; set; } = false;
        public string? ActionUrl { get; set; } // e.g. /Collaboration/Group/5 or /Courses/Details/3

        // Navigation
        public User? Sender { get; set; }
        public User? Recipient { get; set; }
    }
}
