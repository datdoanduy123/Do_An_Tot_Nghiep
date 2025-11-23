using DocTask.Core.Dtos.Tasks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DocTask.Core.Dtos.Progress
{
    public class ReviewUserProgressResponse
    {
        public int TaskId { get; set; }
        public string Title { get; set; } = null!;
        public string? Description { get; set; }
        public int? PeriodId { get; set; }
        public string? Status { get; set; }
        public string? Priority { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? DueDate { get; set; }
        public int? Percentagecomplete { get; set; }
        public int? FrequencyId { get; set; }
        public int? IntervalValue { get; set; }
        public ProgressSummary? Summary { get; set; } = new ();
        public List<ScheduledProgressDto> scheduledProgress { get; set; } = new();
    }
}
