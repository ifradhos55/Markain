using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OzarkLMS.Models
{
    public class Follow
    {
        public int FollowerId { get; set; }
        [ForeignKey("FollowerId")]
        public User Follower { get; set; }

        public int FollowingId { get; set; }
        [ForeignKey("FollowingId")]
        public User Following { get; set; }
    }
}
