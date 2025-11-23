using System.ComponentModel.DataAnnotations;

namespace DocTask.Core.Dtos.Units;

public class UnitDto
{
    public int UnitId { get; set; }
    public int OrgId { get; set; }
    public string UnitName { get; set; } = string.Empty;
    public string? Type { get; set; }
    public int? UnitParent { get; set; }
    public string? ParentUnitName { get; set; }
    public string OrgName { get; set; } = string.Empty;
    public List<UnitDto> ChildUnits { get; set; } = new List<UnitDto>();
    public List<UserDto> Users { get; set; } = new List<UserDto>();
}

public class UnitBasicDto
{
    public int UnitId { get; set; }
    public string Org { get; set; }
    public string UnitName { get; set; } = string.Empty;
    public string FullName {  get; set; } = string.Empty;
    public int UserId { get; set; }
    public string? Type { get; set; }
}

public class UserDto
{
    public int UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? Email { get; set; }
}

public class AssignTaskToUnitRequest
{
    public int TaskId { get; set; }
    public int UnitId { get; set; }
    public string? Message { get; set; }
    public DateTime? DueDate { get; set; }
    public string? Priority { get; set; }
}

public class CreateTaskForUnitRequest
{
    [Required(ErrorMessage = "Title is required")]
    [StringLength(255, ErrorMessage = "Title cannot exceed 255 characters")]
    public string Title { get; set; } = null!;

    [Required(ErrorMessage = "Description is required")]
    public string? Description { get; set; }

    [Required(ErrorMessage = "StartDate is required")]
    public DateTime? StartDate { get; set; }

    [Required(ErrorMessage = "DueDate is required")]
    public DateTime? DueDate { get; set; }

    [Required(ErrorMessage = "Frequency is required")]
    public string Frequency { get; set; } = string.Empty;

    [Required(ErrorMessage = "IntervalValue is required")]
    public int IntervalValue { get; set; }

    public List<int> Days { get; set; } = new();

    [Required(ErrorMessage = "AssignedUnitIds is required")]
    public List<int>? AssignedUnitIds { get; set; } // List of unit IDs to assign the task to

    [Required(ErrorMessage = "ParentTaskId is required")]
    public int ParentTaskId { get; set; } // Task gốc để tạo subtask
}

public class UnitTaskDto
{
    public int TaskId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Status { get; set; }
    public string? Priority { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? DueDate { get; set; }
    public DateTime CreatedAt { get; set; }
    public int? AssignerId { get; set; }
    public string? AssignerName { get; set; }
    public int? AssigneeId { get; set; }
    public string? AssigneeName { get; set; }
    public int? UnitId { get; set; }
    public string? UnitName { get; set; }
    public int? ParentTaskId { get; set; }
    public int? PercentageComplete { get; set; }
    public List<UnitTaskDto> SubTasks { get; set; } = new List<UnitTaskDto>();
}

public class UnitHierarchyDto
{
    public int UnitId { get; set; }
    public string UnitName { get; set; } = string.Empty;
    public int? UnitParent { get; set; }
    public string? ParentUnitName { get; set; }
    public int Level { get; set; }
    public List<UnitHierarchyDto> Children { get; set; } = new List<UnitHierarchyDto>();
}
