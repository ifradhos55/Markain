using System;
using System.ComponentModel.DataAnnotations;

namespace OzarkLMS.Models
{
    public class DashboardAnnouncement
    {
        public int Id { get; set; }

        [Required]
        public string Title { get; set; } = string.Empty;

        [Required]
        public DateTime Date { get; set; } = DateTime.UtcNow;

        // Optional: Could add link or description later
    }
}
