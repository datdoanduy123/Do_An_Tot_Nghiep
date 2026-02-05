using DocTask.Core.Dtos.Clustering;
using DocTask.Core.DTOs.ApiResponses;
using DocTask.Core.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace DocTask.Api.Controllers;

[ApiController]
[Route("api/v1/clustering")]
[Authorize]
public class ClusteringController : ControllerBase
{
    private readonly IEmployeeClusteringService _clusteringService;

    public ClusteringController(IEmployeeClusteringService clusteringService)
    {
        _clusteringService = clusteringService;
    }

    /// <summary>
    /// Train K-means model
    /// </summary>
    [HttpPost("train")]
    public async Task<IActionResult> TrainClusteringModel([FromBody] TrainClusteringRequest request)
    {
        try
        {
            var result = await _clusteringService.TrainClusteringModelAsync(request);

            return Ok(new ApiResponse<ClusteringTrainResult>
            {
                Success = true,
                Data = result,
                Message = $"Train thành công! {result.TotalEmployees} employees, {result.NumberOfClusters} clusters"
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new ApiResponse<string>
            {
                Success = false,
                Error = ex.Message
            });
        }
    }

    /// <summary>
    /// Lấy tất cả kết quả clustering
    /// </summary>
    [HttpGet("results")]
    public async Task<IActionResult> GetAllClusterResults()
    {
        var results = await _clusteringService.GetAllClusterResultsAsync();

        return Ok(new ApiResponse<List<EmployeeClusterResultDTO>>
        {
            Success = true,
            Data = results
        });
    }

    /// <summary>
    /// Lấy cluster của một user
    /// </summary>
    [HttpGet("user/{userId}")]
    public async Task<IActionResult> GetUserCluster(int userId)
    {
        var result = await _clusteringService.GetUserClusterAsync(userId);

        if (result == null)
        {
            return NotFound(new ApiResponse<string>
            {
                Success = false,
                Error = $"Không tìm thấy cluster cho user {userId}"
            });
        }

        return Ok(new ApiResponse<EmployeeClusterResultDTO>
        {
            Success = true,
            Data = result
        });
    }

    /// <summary>
    /// Lấy tất cả employees trong một cluster
    /// </summary>
    [HttpGet("cluster/{clusterId}/employees")]
    public async Task<IActionResult> GetEmployeesByCluster(int clusterId)
    {
        var results = await _clusteringService.GetEmployeesByClusterIdAsync(clusterId);

        return Ok(new ApiResponse<List<EmployeeClusterResultDTO>>
        {
            Success = true,
            Data = results,
            Message = $"Cluster {clusterId} có {results.Count} nhân viên"
        });
    }
}