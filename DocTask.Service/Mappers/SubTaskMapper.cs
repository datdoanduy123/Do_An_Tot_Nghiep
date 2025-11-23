using DocTask.Core.Dtos.SubTasks;
using DocTask.Core.Models;
using DocTask.Core.Dtos.Users;
using TaskEntity = DocTask.Core.Models.Task;

namespace DocTask.Service.Mappers
{
    public static class SubTaskMapper
    {
        public static SubTaskDto ToSubTaskDto(this TaskEntity subTask)
        {
            return new SubTaskDto
            {
                TaskId = subTask.TaskId,
                Title = subTask.Title,
                Description = subTask.Description,
                AssignerId = subTask.AssignerId,
                AssigneeId = subTask.AssigneeId,
                PeriodId = subTask.PeriodId,
                AttachedFile = subTask.AttachedFile,
                Status = subTask.Status,
                Priority = subTask.Priority,
                StartDate = subTask.StartDate,
                DueDate = subTask.DueDate,
                CreatedAt = subTask.CreatedAt,
                FrequencyType = subTask.Frequency?.FrequencyType,
                IntervalValue = subTask.Frequency?.IntervalValue,
                FrequencyDays = subTask.Frequency?.FrequencyDetails
                    ?.Select(fd => fd.DayOfWeek ?? fd.DayOfMonth ?? 0)
                    .Where(v => v > 0)
                    .ToList() ?? new List<int>(),
                Percentagecomplete = subTask.Percentagecomplete,
                ParentTaskId = subTask.ParentTaskId,
                AssignedUsers = subTask.Users?.Select(u => new UserDto
                {
                    UserId = u.UserId,
                    Username = u.Username,
                    FullName = u.FullName,
                    Email = u.Email,
                }).ToList() ?? [],
                AssignedUnits = subTask.Taskunitassignments.Select(t => t.Unit?.ToUnitBasicDto()).ToList()
            };
        }

        public static TaskEntity ToEntity(CreateSubTaskRequest request, int? assignerId = null)
        {
            // Set assigneeId to the first user in assignedUserIds list if available
            var assigneeId = request.AssignedUserIds?.FirstOrDefault();

            return new TaskEntity
            {
                Title = request.Title,
                Description = request.Description,
                AssignerId = null,
                Status = "pending", // Default value
                Priority = "medium", // Default value
                StartDate = request.StartDate, // DateTime? to DateTime?
                DueDate = request.DueDate,     // DateTime? to DateTime?
                Percentagecomplete = 0, // Default value
                CreatedAt = DateTime.UtcNow
            };
        }

        public static void UpdateEntity(TaskEntity existingSubTask, UpdateSubTaskRequest request)
        {
            if (!string.IsNullOrWhiteSpace(request.Title))
                existingSubTask.Title = request.Title;

            if (request.Description != null)
                existingSubTask.Description = request.Description;

            if (request.StartDate.HasValue)
                existingSubTask.StartDate = request.StartDate;

            if (request.DueDate.HasValue)
                existingSubTask.DueDate = request.DueDate;
        }

        public static UserResponse ToUserResponse( User user)
        {
            return new UserResponse
            {
                UserId = user.UserId,
                Username = user.Username,
            };
        }
    }
}