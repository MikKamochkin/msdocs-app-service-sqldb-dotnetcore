using Microsoft.AspNetCore.SignalR;

namespace DotNetCoreSqlDb.Hubs
{
    public class ConversationHub : Hub
    {

        private readonly ILogger<ConversationHub> _logger;

        public async Task SendMessage(Guid conversationId, Guid senderId, string message)
        {
            //_logger.LogInformation("SendMessage: ConversationId={ConversationId}, SenderId={SenderId}, Message={Message}", conversationId, senderId, message);
            await Clients.Group($"conversation_{conversationId}").SendAsync("ReceiveMessage", senderId, message);
            //await Clients.All.SendAsync("ReceiveMessage", sender, message);
        }

        public async Task NotifyMessageCreated(Guid conversationId)
        {
            await Clients
                .Group($"conversation_{conversationId}")
                .SendAsync("MessageCreated", conversationId.ToString());
        }

        public ConversationHub(ILogger<ConversationHub> logger)
        {
            _logger = logger;
        }
        
        public Task JoinConversationGroup(Guid conversationId)
        {
            //_logger.LogInformation("JoinConversationGroup({ConversationId})", conversationId);
            return Groups.AddToGroupAsync(Context.ConnectionId, $"conversation_{conversationId}");
        }

        public Task LeaveConversationGroup(Guid conversationId)
        {
            //_logger.LogInformation("LeaveConversationGroup({ConversationId})", conversationId);
            return Groups.RemoveFromGroupAsync(Context.ConnectionId, $"conversation_{conversationId}");
        }

        public override Task OnConnectedAsync()
        {
            //_logger.LogInformation("SignalR: Connection established: {ConnectionId}", Context.ConnectionId);
            return base.OnConnectedAsync();
        }

        public override Task OnDisconnectedAsync(System.Exception? exception)
        {
            //_logger.LogInformation("SignalR: Connection disconnected: {ConnectionId}", Context.ConnectionId);
            return base.OnDisconnectedAsync(exception);
        }
    }
}