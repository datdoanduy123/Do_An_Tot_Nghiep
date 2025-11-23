//using DocTask.Api.Providers;
//using DocTask.Core.Interfaces.Repositories;
//using Microsoft.AspNetCore.SignalR;
//using Microsoft.Extensions.Hosting;
//using Microsoft.Extensions.Logging;
//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;

//namespace DocTask.Service.Services
//{
//    public class ReminderBackgroundService : BackgroundService
//    {
//        private readonly IServiceProvider _serviceProvider;
//        private readonly ILogger<ReminderBackgroundService> _logger;
//        private readonly IReminderRepository _repo;
//        private readonly IHubContext<NotificationHub> _hubContext;


//        public ReminderBackgroundService(IServiceProvider serviceProvider, ILogger<ReminderBackgroundService> logger, IReminderRepository repo, IHubContext<NotificationHub> hubContext)
//        {
//            _serviceProvider = serviceProvider;
//            _logger = logger;
//            _repo = repo;
//            _hubContext = hubContext;
//        }
//        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
//        {
//            while (!stoppingToken.IsCancellationRequested)
//            {
//                var dueDate = await _repo.GetDueRemindersAsync();
                
//                foreach (var reminder in dueDate)
//                {
//                    await _hubContext.Clients
//                        .Group($"user-{reminder.UserId}")
//                        .SendAsync("ReceiveNotification", new
//                        {
//                            title = reminder.Title,
//                            message = reminder.Message,
//                            triggertime = reminder.Triggertime
//                        });
//                    reminder.Isnotified = true;
//                    await _repo.(reminder);
//                    _logger.LogInformation($"Sent reminder to user {reminder.UserId}: {reminder.Title}");
//                }
//                var nextRun = DateTime.UtcNow.Date.AddDays(1).AddHours(8); var delay = nextRun - DateTime.UtcNow; await Task.Delay(delay, stoppingToken);
//            } 
                
//        }
//    }
//}


