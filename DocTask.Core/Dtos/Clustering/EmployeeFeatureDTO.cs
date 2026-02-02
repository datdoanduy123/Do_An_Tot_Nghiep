namespace DocTask.Core.Dtos.Clustering
{
    public class EmployeeFeatureDTO
    {
        public decimal AverageSkillLevel { get; set; }          // 1-5
        public decimal TotalYearsOfExperience { get; set; }     // 0-20
        public decimal ProductivityScore { get; set; }          // 0.0-1.0
        public decimal CurrentWorkloadPercentage { get; set; }  // 0-100
    }
}