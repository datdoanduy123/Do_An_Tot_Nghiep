using DocTask.Core.Dtos.Reminders;
using DocTask.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DocTask.Service.Mappers
{
    public static class ReminderMapper
    {
        public static Reminder ToRemider (RemiderRequest request)
        {
            return new Reminder {
                Taskid = request.Taskid,
                Periodid = request.Periodid,
                Title = request.Title,
                Message = request.Message,
                Triggertime = request.Triggertime,
                Isauto = request.Isauto,
                Createdby = request.Createdby,
                Isnotified = request.Isnotified ?? false,
                Notifiedat = request.Notifiedat,
                Notificationid = request.Notificationid,
                UserId = request.UserId
            };
        }
        
        public static ReminderDetailDto ToReminderDetailDto (this Reminder model, int userId)
        {
            return new ReminderDetailDto{
                    ReminderId = model.Reminderid,
                    Task = model.Task.ToTaskDto(),
                    Title = model.Title,
                    Message = model.Message,
                    CreatedBy = model.Createdby,
                    CreatedAt  = model.Createdat,
                    IsRead = model.IsRead, 
                    UserId = model.UserId,
                    IsOwner = model.Task.AssignerId == userId
            };
        }

        public static ReminderDto FromRemider (Reminder reminder)
        {
            return new ReminderDto {
                Reminderid = reminder.Reminderid,
                Taskid = reminder.Taskid,
                Periodid = reminder.Periodid,
                Title = reminder.Title,
                Message = reminder.Message,
                Triggertime = reminder.Triggertime,
                Isauto = reminder.Isauto,
                Createdby = reminder.Createdby,
                Createdat = reminder.Createdat,
                Isnotified = reminder.Isnotified,
                Notifiedat = reminder.Notifiedat,
                Notificationid = reminder.Notificationid,
                UserId = reminder.UserId
            };
        }

    }
}
