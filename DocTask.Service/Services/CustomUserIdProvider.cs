using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;

namespace DocTask.Service.Services
{
    public class CustomUserIdProvider : IUserIdProvider
    {
        public string? GetUserId(HubConnectionContext connection)
        {
            // Lấy userId từ claim "id" trong JWT token
            return connection.User?.FindFirst("id")?.Value;
        }
    }
}