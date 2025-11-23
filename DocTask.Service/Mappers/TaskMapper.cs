using DocTask.Core.Dtos.Tasks;
using DocTask.Core.Dtos.Units;
using Task = DocTask.Core.Models.Task;

namespace DocTask.Service.Mappers;

public static class TaskMapper
{
    public static TaskDto ToTaskDto(this Task task)
    {
        return new TaskDto
        {
            TaskId = task.TaskId,
            Title = task.Title,
            Description = task.Description,
            StartDate = task.StartDate,
            DueDate = task.DueDate,
            Percentagecomplete = task.Percentagecomplete ?? 0,
            Status = task.Status ?? "pending",
            ParentTaskId = task.ParentTaskId,

            Frequency = task.Frequency?.FrequencyType,
            IntervalValue = task.Frequency?.IntervalValue,
            Days = task.Frequency?.FrequencyDetails?
                .Select(fd => fd.DayOfWeek ?? fd.DayOfMonth ?? 0)
                .Where(d => d > 0)
                .ToList(),
            AssignedUsersIds = task.Users?.Select(u => new UserDto
            {
                UserId = u.UserId,
                FullName = u.FullName
            }).ToList() ?? new List<UserDto>(),

            AssignedUnitIds = task.Taskunitassignments?.Select(tu => new UnitDto
            {
                UnitId = tu.UnitId,
                UnitName = tu.Unit?.UnitName
            }).ToList() ?? new List<UnitDto>()
        };
    }
}
