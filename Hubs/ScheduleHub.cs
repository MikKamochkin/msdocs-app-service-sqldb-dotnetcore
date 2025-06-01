using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;   // Add this for logging
using System.Threading.Tasks;

namespace DotNetCoreSqlDb.Hubs
{
    public class ScheduleHub : Hub
    {
        private readonly ILogger<ScheduleHub> _logger;

        public ScheduleHub(ILogger<ScheduleHub> logger)
        {
            _logger = logger;
        }

        public Task JoinTeacherGroup(string teacherId)
        {
            _logger.LogInformation("Connection {ConnectionId} requested JoinTeacherGroup({TeacherId})", Context.ConnectionId, teacherId);
            return Groups.AddToGroupAsync(Context.ConnectionId, $"teacher_{teacherId}");
        }

        public Task JoinStudentGroup(Guid studentId)      // ← Guid instead of string
        {
            _logger.LogInformation("JoinStudentGroup({StudentId})", studentId);
            return Groups.AddToGroupAsync(Context.ConnectionId, $"student_{studentId}");
        }

        public override Task OnConnectedAsync()
        {
            _logger.LogInformation("SignalR: Connection established: {ConnectionId}", Context.ConnectionId);
            return base.OnConnectedAsync();
        }

        public override Task OnDisconnectedAsync(System.Exception? exception)
        {
            _logger.LogInformation("SignalR: Connection disconnected: {ConnectionId}", Context.ConnectionId);
            return base.OnDisconnectedAsync(exception);
        }
    }
}
