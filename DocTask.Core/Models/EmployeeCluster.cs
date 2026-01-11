namespace DocTask.Core.Models;

/// <summary>
/// Lưu trữ kết quả phân cụm nhân viên theo K-means
/// Mỗi cluster đại diện cho một nhóm năng lực tương đương
/// </summary>
public partial class EmployeeCluster
{
    public int EmployeeClusterId { get; set; }
    
    public int UserId { get; set; }
    
    /// <summary>
    /// ID của cluster (0, 1, 2, ... k-1)
    /// VD: Cluster 0: Junior, Cluster 1: Mid, Cluster 2: Senior
    /// </summary>
    public int ClusterId { get; set; }
    
    /// <summary>
    /// Tên mô tả cluster (optional)
    /// VD: "Junior Developers", "Senior Backend Engineers"
    /// </summary>
    public string? ClusterName { get; set; }
    
    /// <summary>
    /// Khoảng cách từ employee đến centroid của cluster
    /// Giá trị nhỏ = gần centroid = typical member
    /// </summary>
    public decimal DistanceToCenter { get; set; }
    
    /// <summary>
    /// Confidence score (0-1)
    /// Mức độ chắc chắn employee thuộc cluster này
    /// </summary>
    public decimal ConfidenceScore { get; set; }
    
    /// <summary>
    /// Version của model ML được dùng để cluster
    /// </summary>
    public string ModelVersion { get; set; } = "1.0";
    
    /// <summary>
    /// Thời điểm clustering
    /// </summary>
    public DateTime ClusteredAt { get; set; } = DateTime.Now;
    
    /// <summary>
    /// Feature vector đã dùng để clustering (JSON)
    /// VD: {"avgSkill": 4.2, "experience": 8, "productivity": 0.92, "workload": 0.6}
    /// </summary>
    public string? FeatureVector { get; set; }
    
    // Navigation properties
    public virtual User User { get; set; } = null!;
}
