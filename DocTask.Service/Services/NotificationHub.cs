using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DocTask.Core.Interfaces.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace DocTask.Service.Services
{
    public class NotificationHub : Hub
    {
        private readonly ILogger<NotificationHub> _logger;
        private readonly IReminderService _reminderService;

        public NotificationHub(ILogger<NotificationHub> logger, IReminderService reminderService)
        {
            _logger = logger;
            _reminderService = reminderService;
        }

        public override async Task OnConnectedAsync()
        {
            var userId = Context.UserIdentifier; // Lấy từ JWT token
            if (!string.IsNullOrEmpty(userId))
            {
                // Thêm user vào group theo userId
                await Groups.AddToGroupAsync(Context.ConnectionId, $"user-{userId}");
                _logger.LogInformation($"User {userId} connected with ConnectionId: {Context.ConnectionId}");
            }
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var userId = Context.UserIdentifier;
            if (!string.IsNullOrEmpty(userId))
            {
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"user-{userId}");
                _logger.LogInformation($"User {userId} disconnected");
            }
            await base.OnDisconnectedAsync(exception);
        }

        public async Task SendReminderRead(int reminderId)
        {
            var userId = Context.User?.FindFirst("id")?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return;
            }
            await _reminderService.ReadReminder(int.Parse(userId), reminderId);
        }
    }
}