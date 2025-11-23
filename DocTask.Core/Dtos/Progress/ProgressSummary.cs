using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DocTask.Core.Dtos.Progress
{
    public class ProgressSummary
    {
        public int TotalPeriods { get; set; }
        public int CompletedPeriods { get; set; }
        public int LateReports { get; set; }
        public int PendingReports { get; set; }
        public int MissedReports { get; set; }
        public int upcomingReports { get; set; }
        public double CompletedRate { get; set; }
    }
}
