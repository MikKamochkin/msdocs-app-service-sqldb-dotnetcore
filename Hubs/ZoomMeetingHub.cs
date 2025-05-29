using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;

namespace DotNetCoreSqlDb.Hubs
{
    public class ZoomMeetingHub : Hub
    {
        /// <summary>
        /// Lets a client join a group named by the scheduleId.
        /// </summary>
        public Task JoinGroup(string scheduleId)
            => Groups.AddToGroupAsync(Context.ConnectionId, scheduleId);
    }
}
