using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OzarkLMS.Models
{
    public class Post
    {
        [Key]
        public int Id { get; set; }

        public int UserId { get; set; }
        [ForeignKey("UserId")]
        public User User { get; set; }

        [MaxLength(500)]
        public string? Content { get; set; }

        public string? ImageUrl { get; set; }
        public string? AttachmentUrl { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Social Interactions
        public ICollection<PostVote> Votes { get; set; } = new List<PostVote>();
        public ICollection<PostComment> Comments { get; set; } = new List<PostComment>();

        public int UpvoteCount { get; set; }
        public int DownvoteCount { get; set; }
    }
}
