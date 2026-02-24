using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OzarkLMS.Models
{
    public class PostComment
    {
        public int Id { get; set; }

        public int PostId { get; set; }
        public Post Post { get; set; }

        public int UserId { get; set; }
        public User User { get; set; }

        [Required]
        [MaxLength(500)]
        public string Content { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public int? ParentCommentId { get; set; }
        [ForeignKey("ParentCommentId")]
        public PostComment? ParentComment { get; set; }
        public ICollection<PostComment> Replies { get; set; } = new List<PostComment>();

        // Voting
        public int UpvoteCount { get; set; }
        public int DownvoteCount { get; set; }
        public ICollection<PostCommentVote> Votes { get; set; } = new List<PostCommentVote>();

    }
}
