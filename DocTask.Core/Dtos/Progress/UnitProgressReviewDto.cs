using DocTask.Core.Dtos.Tasks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DocTask.Core.Dtos.Progress
{
    public class UnitProgressReviewDto
    {
        public int UnitId { get; set; }
        public string UnitName { get; set; } = null!;
        public string LeaderFullName { get; set; } = null!;
        public int userId { get; set; } 
        public List<ScheduledProgressDto> ScheduledProgresses { get; set; } = new();

    }
}
