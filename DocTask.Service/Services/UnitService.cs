using DocTask.Core.Dtos.Units;
using DocTask.Core.Interfaces.Repositories;
using DocTask.Core.Interfaces.Services;
using DocTask.Core.Models;
using DocTask.Data;
using Microsoft.EntityFrameworkCore;
using System;

namespace DocTask.Service.Services;

public class UnitService : IUnitService
{
    private readonly IUnitRepository _unitRepository;
    private readonly ITaskRepository _taskRepository;
    private readonly IUserRepository _userRepository;
    private readonly ITaskPermissionService _taskPermissionService;
    private readonly ApplicationDbContext _context;
    private readonly IReminderService _reminderService;

    public UnitService(IUnitRepository unitRepository, ITaskRepository taskRepository, IUserRepository userRepository, ITaskPermissionService taskPermissionService, ApplicationDbContext context, IReminderService reminderService)
    {
        _unitRepository = unitRepository;
        _taskRepository = taskRepository;
        _userRepository = userRepository;
        _taskPermissionService = taskPermissionService;
        _context = context;
        _reminderService = reminderService;
    }

    public async Task<List<UnitDto>> GetAllUnitAsync()
    {
        var units = await _unitRepository.GetAllUnitsAsync();
        return units.Select(MapToUnitDto).ToList();
    }
    
    public async Task<List<UnitDto>> GetAssignableUnitsAsync(int fromUnitId)
    {
        var units = await _unitRepository.GetAssignableUnitsAsync(fromUnitId);
        return units.Select(MapToUnitDto).ToList();
    }

    public async System.Threading.Tasks.Task<UnitTaskDto?> CreateTaskForUnitAsync(CreateTaskForUnitRequest request, int assignerUserId)
    {
        Console.WriteLine($"DEBUG: CreateTaskForUnitAsync called with ParentTaskId={request.ParentTaskId}, AssignerUserId={assignerUserId}");
        
        // Lấy task gốc
        var parentTask = await _taskRepository.GetTaskByIdAsync(request.ParentTaskId);
        Console.WriteLine($"DEBUG: ParentTask found: {parentTask != null}");
        if (parentTask == null) return null;

        // Lấy đơn vị của người giao việc
        var assignerUnit = await GetUserUnitAsync(assignerUserId);
        Console.WriteLine($"DEBUG: AssignerUnit found: {assignerUnit != null}, UnitId: {assignerUnit?.UnitId}");
        if (assignerUnit == null) 
        {
            Console.WriteLine("DEBUG: User does not belong to any unit");
            throw new UnauthorizedAccessException("Bạn không thuộc đơn vị nào. Vui lòng liên hệ quản trị viên để được phân quyền.");
        }

        // Kiểm tra quyền giao việc cho tất cả đơn vị (không vượt cấp)
        foreach (var unitId in request.AssignedUnitIds!)
        {
            Console.WriteLine($"DEBUG: Checking permission for unit {unitId}");
            var canAssign = await CanAssignToUnitWithLevelCheckAsync(assignerUnit.UnitId, unitId);
            Console.WriteLine($"DEBUG: Can assign to unit {unitId}: {canAssign}");
            if (!canAssign)
            {
                throw new UnauthorizedAccessException($"Bạn không có quyền giao việc cho đơn vị {unitId}.");
            }
        }

        // Tạo frequency
        var frequency = new Core.Models.Frequency
        {
            FrequencyType = request.Frequency,
            IntervalValue = request.IntervalValue,
        };
        
        var persistedFrequency = await _context.Frequencies.AddAsync(frequency);
        await _context.SaveChangesAsync();

        // Tạo frequency details
        foreach (var day in request.Days)
        {
            var frequencyDetail = new Core.Models.FrequencyDetail
            {
                FrequencyId = frequency.FrequencyId,
                DayOfMonth = request.Frequency == "monthly" ? day : null,
                DayOfWeek = request.Frequency == "weekly" ? day : null,
            };
            await _context.FrequencyDetails.AddAsync(frequencyDetail);
        }
        await _context.SaveChangesAsync();

        // Tạo subtask cho mỗi đơn vị
        var createdTasks = new List<Core.Models.Task>();
        
        foreach (var unitId in request.AssignedUnitIds)
        {
            Console.WriteLine($"DEBUG: Processing unit {unitId}");
            // Lấy người đứng đầu đơn vị (level = 1)
            var unitHead = await GetUnitHeadAsync(unitId);
            Console.WriteLine($"DEBUG: Unit head found for unit {unitId}: {unitHead != null}, UserId: {unitHead?.UserId}");
            if (unitHead == null) continue; // Bỏ qua nếu không có người đứng đầu

            var unitTask = new Core.Models.Task
            {
                Title = request.Title,
                Description = request.Description,
                AssignerId = assignerUserId,
                AssigneeId = unitHead.UserId, // Giao cho người đứng đầu đơn vị
                OrgId = parentTask.OrgId,
                UnitId = unitId,
                ParentTaskId = request.ParentTaskId, // Là subtask
                Status = "pending",
                Priority = "medium",
                StartDate = request.StartDate,
                DueDate = request.DueDate,
                CreatedAt = DateTime.UtcNow,
                FrequencyId = frequency.FrequencyId,
                Percentagecomplete = 0,
                IsDeleted = false
            };

            var createdTask = await _taskRepository.CreateTaskAsync(unitTask);
            Console.WriteLine($"DEBUG: Task created for unit {unitId}: {createdTask != null}, TaskId: {createdTask?.TaskId}");
            if (createdTask != null)
            {
                // Tạo bản ghi trong Taskunitassignment
                await CreateTaskUnitAssignmentAsync(createdTask.TaskId, unitId);
                createdTasks.Add(createdTask);


                var unitHeader = await GetUnitHeadAsync(unitId);
                if( unitHeader != null)
                {
                    await _reminderService.SendRealTimeNotificationAsync(
                        unitHeader.UserId,
                        $"Phòng ban của bạn nhận được công việc mới: {createdTask.Title}",
                        $"Được giao từ {assignerUnit.UnitName}, hạn: {createdTask.DueDate?.ToString("dd/MM/yyyy") ?? "không xác định"}",
                        new { TaskId = createdTask.TaskId }
                        );
                }
            }
        }

        Console.WriteLine($"DEBUG: Total created tasks: {createdTasks.Count}");
        // Trả về task đầu tiên được tạo
        return createdTasks.Any() ? MapToUnitTaskDto(createdTasks.First()) : null;
    }

    public async Task<UnitDto?> GetUserUnitAsync(int userId)
    {
        var userUnit = await _context.Unitusers
            .Include(uu => uu.Unit)
            .FirstOrDefaultAsync(uu => uu.UserId == userId);
        
        if (userUnit?.Unit == null) return null;
        
        return new UnitDto
        {
            UnitId = userUnit.Unit.UnitId,
            UnitName = userUnit.Unit.UnitName,
            UnitParent = userUnit.Unit.UnitParent,
            OrgId = userUnit.Unit.OrgId
        };
    }

    public async Task<User?> GetUnitHeadAsync(int unitId)
    {
        // Lấy user có level = 1 trong đơn vị
        var unitHead = await _context.Unitusers
            .Include(uu => uu.User)
            .FirstOrDefaultAsync(uu => uu.UnitId == unitId && uu.Level == 1);
        return unitHead?.User;
    }

    private async System.Threading.Tasks.Task CreateTaskUnitAssignmentAsync(int taskId, int unitId)
    {
        // Tạo bản ghi trong Taskunitassignment
        await _taskRepository.CreateTaskUnitAssignmentAsync(taskId, unitId);
    }

    private async Task<bool> CanAssignToUnitWithLevelCheckAsync(int fromUnitId, int targetUnitId)
    {
        Console.WriteLine($"DEBUG: CanAssignToUnitWithLevelCheckAsync fromUnitId={fromUnitId}, targetUnitId={targetUnitId}");
        
        var fromUnit = await _unitRepository.GetUnitByIdAsync(fromUnitId);
        var targetUnit = await _unitRepository.GetUnitByIdAsync(targetUnitId);
        
        Console.WriteLine($"DEBUG: FromUnit found: {fromUnit != null}, UnitParent: {fromUnit?.UnitParent}");
        Console.WriteLine($"DEBUG: TargetUnit found: {targetUnit != null}, UnitParent: {targetUnit?.UnitParent}");
        
        if (fromUnit == null || targetUnit == null) 
        {
            Console.WriteLine("DEBUG: One of the units not found");
            return false;
        }
        
        if (fromUnitId == targetUnitId) 
        {
            Console.WriteLine("DEBUG: Cannot assign to same unit");
            return false; // Không thể giao cho chính mình
        }

        // Nếu đơn vị giao là unit cha (UnitParent = null)
        if (fromUnit.UnitParent == null)
        {
            Console.WriteLine("DEBUG: FromUnit is parent unit");
            // Unit cha có thể giao cho:
            // 1. Các unit cha khác
            if (targetUnit.UnitParent == null)
            {
                Console.WriteLine("DEBUG: TargetUnit is also parent unit, can assign");
                return true;
            }
            
            // 2. Tất cả đơn vị con của mình (đệ quy)
            var canAssignToChild = await _unitRepository.IsChildUnitAsync(fromUnitId, targetUnitId);
            Console.WriteLine($"DEBUG: TargetUnit is child of FromUnit: {canAssignToChild}");
            return canAssignToChild;
        }
        else
        {
            // Nếu đơn vị giao là unit con
            // Chỉ có thể giao cho các đơn vị con khác có cùng cha
            if (targetUnit.UnitParent == null) 
            {
                Console.WriteLine("DEBUG: TargetUnit is parent unit, child unit cannot assign to parent");
                return false; // Không thể giao cho unit cha
            }
            
            var sameParent = fromUnit.UnitParent == targetUnit.UnitParent;
            Console.WriteLine($"DEBUG: Both are child units with same parent: {sameParent}");
            return sameParent;
        }
    }

    private UnitDto MapToUnitDto(Unit unit)
    {
        return new UnitDto
        {
            UnitId = unit.UnitId,
            UnitName = unit.UnitName,
            Type = unit.Type,
            UnitParent = unit.UnitParent,
            OrgId = unit.OrgId
        };
    }

    private UnitTaskDto MapToUnitTaskDto(Core.Models.Task task)
    {
        return new UnitTaskDto
        {
            TaskId = task.TaskId,
            Title = task.Title,
            Description = task.Description,
            AssignerId = task.AssignerId,
            AssigneeId = task.AssigneeId,
            UnitId = task.UnitId,
            Status = task.Status,
            Priority = task.Priority,
            StartDate = task.StartDate,
            DueDate = task.DueDate,
            CreatedAt = task.CreatedAt,
            ParentTaskId = task.ParentTaskId,
            AssignerName = null, // Sẽ cần load từ database nếu cần
            AssigneeName = null, // Sẽ cần load từ database nếu cần
            UnitName = null // Sẽ cần load từ database nếu cần
        };
    }

    // Các method không cần thiết - để trống để implement interface
    public Task<UnitDto?> GetUnitByIdAsync(int unitId) => throw new NotImplementedException();
    public Task<List<UnitDto>> GetUnitsByOrgIdAsync(int orgId) => throw new NotImplementedException();
    public Task<List<UnitHierarchyDto>> GetUnitHierarchyAsync(int orgId) => throw new NotImplementedException();
    public Task<bool> CanAssignToUnitAsync(int fromUnitId, int targetUnitId) => throw new NotImplementedException();
    public Task<UnitTaskDto?> AssignTaskToUnitAsync(AssignTaskToUnitRequest request, int assignerUserId) => throw new NotImplementedException();
    public Task<List<UnitTaskDto>> GetTasksByUnitAsync(int unitId, int userId) => throw new NotImplementedException();
    public Task<List<UnitTaskDto>> GetTasksAssignedToUnitAsync(int unitId, int userId) => throw new NotImplementedException();
    public Task<UnitDto?> CreateUnitAsync(UnitDto unitDto) => throw new NotImplementedException();
    public Task<UnitDto?> UpdateUnitAsync(int unitId, UnitDto unitDto) => throw new NotImplementedException();
    public Task<bool> DeleteUnitAsync(int unitId) => throw new NotImplementedException();
}