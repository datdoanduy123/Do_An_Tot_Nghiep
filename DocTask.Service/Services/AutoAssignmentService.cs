using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DocTask.Core.Dtos.Tasks;
using DocTask.Core.Interfaces.Repositories;
using DocTask.Core.Interfaces.Services;
using DocTask.Core.Models;
using Microsoft.EntityFrameworkCore;
using TaskModel = DocTask.Core.Models.Task;

namespace DocTask.Service.Services
{
    /// <summary>
    /// Service phân công tự động task cho nhân viên dựa trên workload hiện tại.
    /// Lưu ý: TaskSkillRequirement đã bị xóa, việc scoring dựa trên availability.
    /// </summary>
    public class AutoAssignmentService : IAutoAssignmentService
    {
        private readonly DocTask.Data.ApplicationDbContext _context;
        private readonly ITaskRepository _taskRepository;

        // Ngưỡng tối thiểu để chấp nhận phân công (availability score)
        private const decimal ASSIGNMENT_THRESHOLD = 0.2m;

        public AutoAssignmentService(
            DocTask.Data.ApplicationDbContext context,
            ITaskRepository taskRepository)
        {
            _context = context;
            _taskRepository = taskRepository;
        }

        /// <summary>
        /// Đề xuất phân công cho danh sách taskIds dựa trên workload availability.
        /// </summary>
        public async Task<List<AssignmentProposalDto>> ProposeAssignmentsAsync(List<int> taskIds)
        {
            var proposals = new List<AssignmentProposalDto>();

            // 1. Lấy danh sách Tasks cần xử lý
            var tasksToCheck = await _context.Tasks
                .Where(t => taskIds.Contains(t.TaskId) && t.Status != "Done" && t.Status != "Completed")
                .ToListAsync();

            if (!tasksToCheck.Any()) return proposals;

            // 2. Lấy danh sách nhân viên có EmployeeProfile
            var employees = await _context.Users
                .Include(u => u.EmployeeProfile)
                .Where(u => u.EmployeeProfile != null)
                .ToListAsync();

            foreach (var task in tasksToCheck)
            {
                User? bestCandidate = null;
                decimal bestScore = -1;
                string bestReason = "";

                foreach (var emp in employees)
                {
                    var profile = emp.EmployeeProfile;
                    if (profile == null) continue;

                    // --- Tính availability score ---
                    // 1.0 = hoàn toàn rảnh, 0.0 = full tải, âm = quá tải
                    decimal currentLoad = profile.CurrentWorkloadHours;
                    decimal capacity = profile.WeeklyCapacity > 0 ? profile.WeeklyCapacity : 40;

                    decimal availabilityScore = currentLoad >= capacity
                        ? -0.1m
                        : (capacity - currentLoad) / capacity;

                    // Cập nhật AvailableHoursPerWeek để hiển thị
                    profile.AvailableHoursPerWeek = capacity - currentLoad;

                    if (availabilityScore > bestScore)
                    {
                        bestScore = availabilityScore;
                        bestCandidate = emp;
                        bestReason = $"Availability: {profile.AvailableHoursPerWeek:N1}h còn trống";
                    }
                }

                if (bestCandidate != null && bestScore >= ASSIGNMENT_THRESHOLD)
                {
                    // Cộng dồn estimated hours vào workload để tránh dồn hết vào 1 người
                    if (bestCandidate.EmployeeProfile != null && task.EstimatedHours.HasValue)
                    {
                        bestCandidate.EmployeeProfile.CurrentWorkloadHours += task.EstimatedHours.Value;
                    }

                    proposals.Add(new AssignmentProposalDto
                    {
                        TaskId = task.TaskId,
                        TaskTitle = task.Title,
                        AssignedUserId = bestCandidate.UserId,
                        AssignedUserName = bestCandidate.FullName,
                        MatchScore = bestScore,
                        Reasoning = bestReason,
                        PredictedUtilization = bestCandidate.EmployeeProfile?.CurrentWorkloadHours ?? 0
                    });
                }
            }

            return proposals;
        }
    }
}
