using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DocTask.Core.Dtos.Clustering
{
    public class EmployeeClusterResultDTO
    {
        public int UserId { get; set; }
        public string UserName { get; set; } = null!;
        public string FullName { get; set; } = null!;
        public int ClusterId { get; set; }                      // 0, 1, 2
        public string ClusterName { get; set; } = null!;        // "Junior", "Mid", "Senior"
        public decimal DistanceToCenter { get; set; }           // Khoảng cách đến centroid
        public decimal ConfidenceScore { get; set; }            // 0-1 (cao = chắc chắn)
        public EmployeeFeatureDTO Features { get; set; } = null!;
    }

    // Request để train K-means
    public class TrainClusteringRequest
    {
        public int NumberOfClusters { get; set; } = 3;          // K=3 (Junior, Mid, Senior)
        public List<string>? ClusterNames { get; set; }
    }
    // Kết quả sau khi train
    public class ClusteringTrainResult
    {
        public int TotalEmployees { get; set; }
        public int NumberOfClusters { get; set; }
        public List<ClusterInfoDto> Clusters { get; set; } = new();
        public DateTime TrainedAt { get; set; }
        public string ModelVersion { get; set; } = "1.0";
    }
    // Thông tin về một cluster
    public class ClusterInfoDto
    {
        public int ClusterId { get; set; }
        public string ClusterName { get; set; } = null!;
        public int MemberCount { get; set; }
        public EmployeeFeatureDTO Centroid { get; set; } = null!;  // Trung tâm cluster
    }
}