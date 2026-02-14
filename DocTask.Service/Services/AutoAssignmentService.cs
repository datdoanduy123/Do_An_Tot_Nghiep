using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DocTask.Core.Dtos.Tasks;
using DocTask.Core.Interfaces.Repositories;
using DocTask.Core.Interfaces.Services;
using DocTask.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace DocTask.Service.Services
{
    public class AutoAssignmentService : IAutoAssignmentService
    {
        private readonly DocTask.Data.ApplicationDbContext _context;
        private readonly ITaskRepository _taskRepository;

        // Weights for scoring
        private const decimal WEIGHT_SKILL_MATCH = 0.5m;
        private const decimal WEIGHT_AVAILABILITY = 0.5m;
        
        // Threshold to accept assignment
        private const decimal ASSIGNMENT_THRESHOLD = 0.4m;

        public AutoAssignmentService(
            DocTask.Data.ApplicationDbContext context,
            ITaskRepository taskRepository)
        {
            _context = context;
            _taskRepository = taskRepository;
        }

        public async Task<List<AssignmentProposalDto>> ProposeAssignmentsForDraftAsync(int draftId)
        {
            // Get all tasks generated from this draft
            var tasks = await _context.Tasks
                .Include(t => t.SkillRequirements).ThenInclude(sr => sr.Skill)
                .Where(t => 
                    (t.ParentTaskId == null && _context.TaskDrafts.Any(d => d.DraftId == draftId && d.CreatedTaskId == t.TaskId)) ||
                    t.ParentTask.IsAIGenerated // Simplify: check if part of AI generated hierarchy
                )
                .ToListAsync();

            // Refine query to get EXACTLY tasks from this draft approval
            // Since we link root draft -> root task via CreatedTaskId
            var rootDraft = await _context.TaskDrafts.FindAsync(draftId);
            if (rootDraft?.CreatedTaskId == null) return new List<AssignmentProposalDto>();
            
            var rootTaskId = rootDraft.CreatedTaskId.Value;
            var allRelatedTasks = await _context.Tasks
                .Include(t=>t.SkillRequirements).ThenInclude(sr=>sr.Skill)
                .Where(t => t.TaskId == rootTaskId || t.ParentTaskId == rootTaskId || t.ParentTask.ParentTaskId == rootTaskId)
                .ToListAsync();

            var taskIds = allRelatedTasks.Select(t => t.TaskId).ToList();
            return await ProposeAssignmentsAsync(taskIds);
        }

        public async Task<List<AssignmentProposalDto>> ProposeAssignmentsAsync(List<int> taskIds)
        {
            var proposals = new List<AssignmentProposalDto>();
            
            // 1. Get Tasks with Requirements
            var tasksToCheck = await _context.Tasks
                .Include(t => t.SkillRequirements).ThenInclude(r => r.Skill)
                .Where(t => taskIds.Contains(t.TaskId) && t.Status != "Done" && t.Status != "Completed")
                .ToListAsync();

            if (!tasksToCheck.Any()) return proposals;

            // 2. Get Active Employees with Profiles & Skills
            var employees = await _context.Users
                .Include(u => u.EmployeeProfile)
                //.Include(u => u.UserSkills).ThenInclude(us => us.Skill) // Assuming navigation exists
                .Where(u => u.EmployeeProfile != null) // Filter active
                .ToListAsync();
            
            // Load UserSkills explicitly if include fails or to be safe
            foreach(var emp in employees)
            {
                await _context.Entry(emp).Collection(u => u.UserSkills).LoadAsync();
                foreach(var us in emp.UserSkills)
                {
                    await _context.Entry(us).Reference(x => x.Skill).LoadAsync();
                }
            }

            foreach (var task in tasksToCheck)
            {
                // Skip task if no skill requirement (or assign to default PM?) -> Skip for now
                if (!task.SkillRequirements.Any()) 
                {
                    // Fallback: Assign to random available or skip
                    continue;
                }

                User? bestCandidate = null;
                decimal bestScore = -1;
                string bestReason = "";

                foreach (var emp in employees)
                {
                    var profile = emp.EmployeeProfile;
                    if (profile == null) continue;

                    // --- Score 1: Skill Match ---
                    decimal skillScore = CalculateSkillMatch(task.SkillRequirements.ToList(), emp.UserSkills.ToList());

                    // --- Score 2: Availability ---
                    // Capacity saturation: 1.0 (empty) -> 0.0 (full)
                    // If Overload (> 100%), score is negative to discourage
                    decimal currentLoad = profile.CurrentWorkloadHours; // Already stored in DB
                    decimal capacity = profile.WeeklyCapacity > 0 ? profile.WeeklyCapacity : 40;
                    
                    decimal availabilityScore = 0;
                    if (currentLoad >= capacity) 
                    {
                        availabilityScore = -0.1m; // Overloaded
                    }
                    else
                    {
                        availabilityScore = (capacity - currentLoad) / capacity; 
                    }

                    // Calculate AvailableHoursPerWeek for display
                    profile.AvailableHoursPerWeek = capacity - currentLoad;

                    // --- Final Weighted Score ---
                    decimal finalScore = (skillScore * WEIGHT_SKILL_MATCH) + (availabilityScore * WEIGHT_AVAILABILITY);

                    if (finalScore > bestScore)
                    {
                        bestScore = finalScore;
                        bestCandidate = emp;
                        bestReason = $"Skill match: {skillScore:P0}, Avail: {profile.AvailableHoursPerWeek:N1}h";
                    }
                }

                if (bestCandidate != null && bestScore >= ASSIGNMENT_THRESHOLD)
                {
                    // Update predicted workload for this proposal run (greedy approach)
                    // Allows distribution among team instead of piling on one superstar
                    if (bestCandidate.EmployeeProfile != null && task.EstimatedHours.HasValue)
                    {
                         // Assuming task duration ~ 1 week for simplicity or add fractional
                         // For V1, add full estimate to current load to penalize next assignment
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

        private decimal CalculateSkillMatch(List<TaskSkillRequirement> required, List<UserSkill> userSkills)
        {
            if (!required.Any()) return 1.0m; // No reqs = 100% match

            decimal totalWeight = 0;
            decimal totalScore = 0;

            foreach (var req in required)
            {
                var weight = req.Importance; // Default importance 1
                totalWeight += weight;

                var userSkill = userSkills.FirstOrDefault(us => 
                    us.SkillId == req.SkillId || 
                    (us.Skill.SkillName.ToLower().Contains(req.Skill.SkillName.ToLower())) // Loose match by name
                );

                if (userSkill != null)
                {
                    // Calculate ratio: UserLevel / ReqLevel
                    // e.g. User 4 / Req 3 = 1.33 (Bonus)
                    // User 2 / Req 3 = 0.66 (Penalty)
                    decimal reqLevel = req.RequiredLevel > 0 ? req.RequiredLevel : 1;
                    decimal userLevel = userSkill.ProficiencyLevel > 0 ? userSkill.ProficiencyLevel : 1; // Default stored as Level

                    decimal ratio = userLevel / reqLevel;
                    if (ratio > 1.2m) ratio = 1.2m; // Cap bonus at 120%

                    totalScore += ratio * weight;
                }
            }

            return totalWeight > 0 ? totalScore / totalWeight : 0;
        }
    }
}
