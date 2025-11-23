using DocTask.Core.Dtos.Progress;
using DocTask.Core.Dtos.Tasks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DocTask.Service.Mappers
{
    public class ProgressMapper
    {
        public ReviewUserProgressResponse FromTaskToReviewUserProgressResponse(Core.Models.Task task, string frequencyType, int intervalValue)
        {
            return new ReviewUserProgressResponse
            {
                TaskId = task.TaskId,
                Title = task.Title,
                Description = task.Description ?? string.Empty,
                StartDate = task.StartDate,
                DueDate = task.DueDate,
                Status = task.Status ?? string.Empty,
                Percentagecomplete = task.Percentagecomplete,
                IntervalValue = intervalValue,
                scheduledProgress = new List<ScheduledProgressDto>()
            };
        }
    }
}
