using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DocTask.Core.Dtos.Clustering;

namespace DocTask.Core.Interfaces.Services
{
    public interface IEmployeeClusteringService
    {
        /// <summary>
        /// Train K-means model với dữ liệu hiện tại
        /// </summary>
        Task<ClusteringTrainResult> TrainClusteringModelAsync(TrainClusteringRequest request);

        /// <summary>
        /// Lấy kết quả clustering của tất cả employees
        /// </summary>
        Task<List<EmployeeClusterResultDTO>> GetAllClusterResultsAsync();

        /// <summary>
        /// Lấy kết quả clustering của một employee
        /// </summary>
        Task<EmployeeClusterResultDTO?> GetUserClusterAsync(int userId);

        /// <summary>
        /// Lấy danh sách employees trong cùng cluster
        /// </summary>
        Task<List<EmployeeClusterResultDTO>> GetEmployeesByClusterIdAsync(int clusterId);

        /// <summary>
        /// Predict cluster cho một employee mới
        /// </summary>
        Task<EmployeeClusterResultDTO> PredictClusterAsync(EmployeeFeatureDTO features, int userId);
    }
}