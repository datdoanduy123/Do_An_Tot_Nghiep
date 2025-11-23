using DocTask.Core.Dtos.Tasks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DocTask.Core.Dtos.Progress
{
    public class UnitMemberProgressDto
    {
        public int UserId { get; set; }
        public string UserName { get; set; } = string.Empty;
        public bool IsLeader { get; set; }
        public List<ScheduledProgressDto> ScheduledProgresses { get; set; } = new();
    }
}
