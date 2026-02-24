using OzarkLMS.Models;
using System.Collections.Generic;

namespace OzarkLMS.ViewModels
{
    public class SocialHubViewModel
    {
        public User CurrentUser { get; set; }
        
        // Feed
        public List<Post> Feed { get; set; } = new List<Post>();
        
        // Chats
        public List<ChatGroup> ChatGroups { get; set; } = new List<ChatGroup>();
        public List<PrivateChat> PrivateChats { get; set; } = new List<PrivateChat>();
        
        // Metadata
        public Dictionary<int, int> GroupUnread { get; set; } = new Dictionary<int, int>();
        public Dictionary<int, int> PrivateUnread { get; set; } = new Dictionary<int, int>();
    }
}
