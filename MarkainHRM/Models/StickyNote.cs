using System;

namespace OzarkLMS.Models
{
    public class StickyNote
    {
        public int Id { get; set; }
        public string Content { get; set; } = string.Empty;
        public string Color { get; set; } = "bg-yellow-200"; // yellow, blue, green, pink
        public int UserId { get; set; }
        public User User { get; set; }
    }
}
