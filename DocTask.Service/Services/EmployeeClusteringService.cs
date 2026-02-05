using DocTask.Core.Models;
using Microsoft.ML;
using Microsoft.ML.Data;
using DocTask.Core.Dtos.Clustering;
using DocTask.Core.Interfaces.Services;
using DocTask.Core.Interfaces.Repositories;
using System.Text.Json;
namespace DocTask.Service.Services;

public class EmployeeClusteringService : IEmployeeClusteringService
{
    private readonly MLContext _mlContext;
    private readonly IEmployeeProfileRepository _employeeProfileRepository;
    private readonly IEmployeeClusterRepository _employeeClusterRepository;
    private readonly IUserRepository _userRepository;
    private ITransformer? _trainedModel;

    // ===== ML.NET DATA CLASSES =====

    /// <summary>
    /// Input data cho ML.NET
    /// </summary>
    public class EmployeeData
    {
        [LoadColumn(0)] public float UserId { get; set; }
        [LoadColumn(1)] public float AverageSkillLevel { get; set; }
        [LoadColumn(2)] public float TotalYearsOfExperience { get; set; }
        [LoadColumn(3)] public float ProductivityScore { get; set; }
        [LoadColumn(4)] public float CurrentWorkloadPercentage { get; set; }
    }

    /// <summary>
    /// Output prediction từ ML.NET
    /// </summary>
    public class ClusterPrediction
    {
        [ColumnName("PredictedLabel")]
        public uint ClusterId { get; set; }

        [ColumnName("Score")]
        public float[] Distances { get; set; } = Array.Empty<float>();
    }

    // ===== CONSTRUCTOR =====

    public EmployeeClusteringService(
        IEmployeeProfileRepository employeeProfileRepository,
        IEmployeeClusterRepository employeeClusterRepository,
        IUserRepository userRepository
    )
    {
        _mlContext = new MLContext(seed: 0);
        _employeeProfileRepository = employeeProfileRepository;
        _employeeClusterRepository = employeeClusterRepository;
        _userRepository = userRepository;
    }

    // ===== PUBLIC METHODS =====

    /// <summary>
    /// 🎯 METHOD CHÍNH: Train K-means model
    /// </summary>
    public async Task<ClusteringTrainResult> TrainClusteringModelAsync(TrainClusteringRequest request)
    {
        // 1. Lấy data từ DB
        var profiles = await _employeeProfileRepository.GetAllAsync();

        if (profiles.Count < request.NumberOfClusters)
        {
            throw new InvalidOperationException(
                $"Không đủ data! Cần ít nhất {request.NumberOfClusters} employees, hiện có {profiles.Count}");
        }

        // 2. Convert sang ML.NET format
        var employeeData = profiles.Select(p => new EmployeeData
        {
            UserId = p.UserId,
            AverageSkillLevel = (float)p.AverageSkillLevel,
            TotalYearsOfExperience = (float)p.TotalYearsOfExperience,
            ProductivityScore = (float)p.ProductivityScore,
            CurrentWorkloadPercentage = (float)p.CurrentWorkloadPercentage
        }).ToList();

        var dataView = _mlContext.Data.LoadFromEnumerable(employeeData);

        // 3. Build ML Pipeline
        // Concatenate 4 features thành 1 vector
        var pipeline = _mlContext.Transforms.Concatenate(
            "Features",
            nameof(EmployeeData.AverageSkillLevel),
            nameof(EmployeeData.TotalYearsOfExperience),
            nameof(EmployeeData.ProductivityScore),
            nameof(EmployeeData.CurrentWorkloadPercentage)
        ).Append(_mlContext.Clustering.Trainers.KMeans(
            featureColumnName: "Features",
            numberOfClusters: request.NumberOfClusters
        ));

        // 4. Train model
        Console.WriteLine($"🤖 Training K-means với K={request.NumberOfClusters}...");
        _trainedModel = pipeline.Fit(dataView);
        Console.WriteLine("✅ Training hoàn thành!");

        // 5. Predict cho tất cả employees
        var predictions = _mlContext.Data.CreateEnumerable<ClusterPrediction>(
            _trainedModel.Transform(dataView),
            reuseRowObject: false
        ).ToList();

        // 6. Generate cluster names
        var clusterNames = request.ClusterNames ?? GenerateDefaultClusterNames(request.NumberOfClusters);

        // 7. Save results to DB
        await SaveClusterResults(profiles, predictions, clusterNames);

        // 8. Calculate statistics
        var clusterInfos = CalculateClusterStatistics(profiles, predictions, clusterNames);

        return new ClusteringTrainResult
        {
            TotalEmployees = profiles.Count,
            NumberOfClusters = request.NumberOfClusters,
            Clusters = clusterInfos,
            TrainedAt = DateTime.Now,
            ModelVersion = "1.0"
        };
    }

    public async Task<List<EmployeeClusterResultDTO>> GetAllClusterResultsAsync()
    {
        var clusters = await _employeeClusterRepository.GetAllAsync();
        var results = new List<EmployeeClusterResultDTO>();

        foreach (var cluster in clusters)
        {
            var user = await _userRepository.GetByIdAsync(cluster.UserId);
            var profile = await _employeeProfileRepository.GetByUserIdAsync(cluster.UserId);

            if (user != null && profile != null)
            {
                results.Add(MapToDto(cluster, user, profile));
            }
        }

        return results;
    }

    public async Task<EmployeeClusterResultDTO?> GetUserClusterAsync(int userId)
    {
        var cluster = await _employeeClusterRepository.GetByUserIdAsync(userId);
        if (cluster == null) return null;

        var user = await _userRepository.GetByIdAsync(userId);
        var profile = await _employeeProfileRepository.GetByUserIdAsync(userId);

        if (user == null || profile == null) return null;

        return MapToDto(cluster, user, profile);
    }

    public async Task<List<EmployeeClusterResultDTO>> GetEmployeesByClusterIdAsync(int clusterId)
    {
        var clusters = await _employeeClusterRepository.GetByClusterIdAsync(clusterId);
        var results = new List<EmployeeClusterResultDTO>();

        foreach (var cluster in clusters)
        {
            var user = await _userRepository.GetByIdAsync(cluster.UserId);
            var profile = await _employeeProfileRepository.GetByUserIdAsync(cluster.UserId);

            if (user != null && profile != null)
            {
                results.Add(MapToDto(cluster, user, profile));
            }
        }

        return results;
    }

    public async Task<EmployeeClusterResultDTO> PredictClusterAsync(EmployeeFeatureDTO features, int userId)
    {
        if (_trainedModel == null)
        {
            throw new InvalidOperationException("Model chưa được train!");
        }

        var employeeData = new EmployeeData
        {
            UserId = userId,
            AverageSkillLevel = (float)features.AverageSkillLevel,
            TotalYearsOfExperience = (float)features.TotalYearsOfExperience,
            ProductivityScore = (float)features.ProductivityScore,
            CurrentWorkloadPercentage = (float)features.CurrentWorkloadPercentage
        };

        var predictionEngine = _mlContext.Model.CreatePredictionEngine<EmployeeData, ClusterPrediction>(_trainedModel);
        var prediction = predictionEngine.Predict(employeeData);

        var user = await _userRepository.GetByIdAsync(userId);
        if (user == null)
        {
            throw new ArgumentException($"User {userId} không tồn tại");
        }

        var clusterId = (int)prediction.ClusterId;
        var distanceToCenter = prediction.Distances.Length > 0
            ? (decimal)prediction.Distances.Min()
            : 0m;
        
        return new EmployeeClusterResultDTO
        {
            UserId = userId,
            UserName = user.Username,
            FullName = user.FullName ?? user.Username,
            ClusterId = clusterId,
            ClusterName = $"Cluster {clusterId}",
            DistanceToCenter = distanceToCenter,
            ConfidenceScore = CalculateConfidence(prediction.Distances, clusterId),
            Features = features
        };
    }

    // ===== PRIVATE HELPER METHODS =====

    private async System.Threading.Tasks.Task SaveClusterResults(
        List<EmployeeProfile> profiles,
        List<ClusterPrediction> predictions,
        List<string> clusterNames)
    {
        await _employeeClusterRepository.DeleteAllAsync();

        for (int i = 0; i < profiles.Count; i++)
        {
            var profile = profiles[i];
            var prediction = predictions[i];

            var featureVector = JsonSerializer.Serialize(new
            {
                avgSkill = profile.AverageSkillLevel,
                experience = profile.TotalYearsOfExperience,
                productivity = profile.ProductivityScore,
                workload = profile.CurrentWorkloadPercentage
            });

            // Fix: Safely access arrays với bounds checking
            var clusterId = (int)prediction.ClusterId;
            var clusterName = clusterId < clusterNames.Count 
                ? clusterNames[clusterId] 
                : $"Cluster {clusterId}";
            
            // Fix: Distances array có thể có length khác với số clusters
            var distanceToCenter = prediction.Distances.Length > 0
                ? (decimal)prediction.Distances.Min()  // Lấy distance nhỏ nhất (gần nhất với cluster)
                : 0m;
            
            var employeeCluster = new EmployeeCluster
            {
                UserId = profile.UserId,
                ClusterId = clusterId,
                ClusterName = clusterName,
                DistanceToCenter = distanceToCenter,
                ConfidenceScore = CalculateConfidence(prediction.Distances, clusterId),
                ModelVersion = "1.0",
                ClusteredAt = DateTime.Now,
                FeatureVector = featureVector
            };

            await _employeeClusterRepository.CreateAsync(employeeCluster);
        }
    }

    private decimal CalculateConfidence(float[] distances, int assignedCluster)
    {
        var assignedDistance = distances[assignedCluster];
        var otherDistances = distances.Where((d, idx) => idx != assignedCluster).ToList();

        if (otherDistances.Count == 0) return 1.0m;

        var avgOtherDistance = otherDistances.Average();
        var confidence = 1.0f / (1.0f + assignedDistance / avgOtherDistance);

        return Math.Round((decimal)confidence, 2);
    }

    private List<string> GenerateDefaultClusterNames(int k)
    {
        if (k == 3)
        {
            return new List<string> { "Junior Developers", "Mid-level Developers", "Senior Developers" };
        }

        return Enumerable.Range(0, k).Select(i => $"Cluster {i}").ToList();
    }

    private List<ClusterInfoDto> CalculateClusterStatistics(
        List<EmployeeProfile> profiles,
        List<ClusterPrediction> predictions,
        List<string> clusterNames)
    {
        var clusterInfos = new List<ClusterInfoDto>();

        for (int clusterId = 0; clusterId < clusterNames.Count; clusterId++)
        {
            var membersInCluster = profiles
                .Where((p, idx) => predictions[idx].ClusterId == clusterId)
                .ToList();

            if (membersInCluster.Count == 0) continue;

            var centroid = new EmployeeFeatureDTO
            {
                AverageSkillLevel = membersInCluster.Average(m => m.AverageSkillLevel),
                TotalYearsOfExperience = membersInCluster.Average(m => m.TotalYearsOfExperience),
                ProductivityScore = membersInCluster.Average(m => m.ProductivityScore),
                CurrentWorkloadPercentage = membersInCluster.Average(m => m.CurrentWorkloadPercentage)
            };

            clusterInfos.Add(new ClusterInfoDto
            {
                ClusterId = clusterId,
                ClusterName = clusterNames[clusterId],
                MemberCount = membersInCluster.Count,
                Centroid = centroid
            });
        }

        return clusterInfos;
    }

    private EmployeeClusterResultDTO MapToDto(EmployeeCluster cluster, User user, EmployeeProfile profile)
    {
        return new EmployeeClusterResultDTO
        {
            UserId = user.UserId,
            UserName = user.Username,
            FullName = user.FullName ?? user.Username,
            ClusterId = cluster.ClusterId,
            ClusterName = cluster.ClusterName ?? $"Cluster {cluster.ClusterId}",
            DistanceToCenter = cluster.DistanceToCenter,
            ConfidenceScore = cluster.ConfidenceScore,
            Features = new EmployeeFeatureDTO
            {
                AverageSkillLevel = profile.AverageSkillLevel,
                TotalYearsOfExperience = profile.TotalYearsOfExperience,
                ProductivityScore = profile.ProductivityScore,
                CurrentWorkloadPercentage = profile.CurrentWorkloadPercentage
            }
        };
    }
}