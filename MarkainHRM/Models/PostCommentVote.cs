using System.ComponentModel.DataAnnotations;

namespace OzarkLMS.Models
{
    public class PostCommentVote
    {
        public int CommentId { get; set; }
        public PostComment Comment { get; set; }

        public int UserId { get; set; }
        public User User { get; set; }

        public int Value { get; set; } // 1 for Upvote, -1 for Downvote
    }
}
