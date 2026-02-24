using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OzarkLMS.Models
{
    public class PostVote
    {
        public int PostId { get; set; }
        public Post Post { get; set; }

        public int UserId { get; set; }
        public User User { get; set; }

        public int Value { get; set; } // 1 for Upvote, -1 for Downvote
    }
}
