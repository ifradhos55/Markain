using OzarkLMS.Models;
using System.Collections.Generic;

namespace OzarkLMS.ViewModels
{
    public class UserProfileViewModel
    {
        public User? User { get; set; }
        public List<Course> EnrolledCourses { get; set; } = new List<Course>();
        public List<Course> TaughtCourses { get; set; } = new List<Course>();

        // For Admin View
        public List<User> AllInstructors { get; set; } = new List<User>();
        public List<User> AllStudents { get; set; } = new List<User>();
        
        public List<ChatGroup> ChatGroups { get; set; } = new List<ChatGroup>();

        // Social Hub Extensions
        public List<Post> Posts { get; set; } = new List<Post>();
        public List<SharedPost> SharedPosts { get; set; } = new List<SharedPost>();
        public int FollowersCount { get; set; }
        public int FollowingCount { get; set; }
        public bool IsFollowing { get; set; } // For the viewer to know if they follow this profile

    }
}
