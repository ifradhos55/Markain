using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;

namespace OzarkLMS.Hubs
{
    public class VoteHub : Hub
    {
        public async Task SendVoteUpdate(int postId, int newScore, int userVote, int userId)
        {
            await Clients.All.SendAsync("ReceiveVoteUpdate", postId, newScore, userVote, userId);
        }

        public async Task SendCommentVoteUpdate(int commentId, int newScore, int userVote, int userId)
        {
            await Clients.All.SendAsync("ReceiveCommentVoteUpdate", commentId, newScore, userVote, userId);
        }

        public async Task SendNewComment(int postId, object commentData)
        {
            await Clients.All.SendAsync("ReceiveNewComment", postId, commentData);
        }

        public async Task SendCommentDeleted(int commentId, int postId)
        {
            await Clients.All.SendAsync("ReceiveCommentDeleted", commentId, postId);
        }

        public async Task SendCommentEdited(int commentId, string content)
        {
            await Clients.All.SendAsync("ReceiveCommentEdited", commentId, content);
        }

        public async Task SendPostEdited(int postId, string content)
        {
            await Clients.All.SendAsync("ReceivePostEdited", postId, content);
        }

        public async Task SendPostDeleted(int postId)
        {
            await Clients.All.SendAsync("ReceivePostDeleted", postId);
        }

        public async Task SendChatUpdate(int chatId, bool isPrivate, string timestamp, List<int> recipientUserIds)
        {
            await Clients.All.SendAsync("ReceiveChatUpdate", chatId, isPrivate, timestamp);
        }
    }
}
