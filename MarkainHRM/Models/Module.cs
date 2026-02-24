using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace OzarkLMS.Models
{
    public class Module
    {
        public int Id { get; set; }
        
        public int CourseId { get; set; } // FK

        [Required]
        public string Title { get; set; } = string.Empty;

        public List<ModuleItem> Items { get; set; } = new List<ModuleItem>();
        public Course Course { get; set; } = null!; // Navigation property
    }

    public class ModuleItem
    {
        public int Id { get; set; }
        public int ModuleId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? ContentUrl { get; set; } // Path to uploaded file (pdf/docx)
        public string Type { get; set; } = "page"; // page, file, quiz, assignment
        public string DisplayMode { get; set; } = "link"; // link, embed (for direct view)
        public Module Module { get; set; } = null!; // Navigation property
    }
}
