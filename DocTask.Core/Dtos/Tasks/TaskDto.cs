using System.ComponentModel.DataAnnotations;
using DocTask.Core.Dtos.Units;

namespace DocTask.Core.Dtos.Tasks;

public class TaskDto
{
  public int TaskId { get; set; }

  [Required(ErrorMessage = "Tiêu đề là bắt buộc.")]
  public string Title { get; set; } = null!;

  [Required(ErrorMessage = "Mô tả là bắt buộc.")]
  public string Description { get; set; }
  public int AssignerId { get; set; }
  public int PeriodId { get; set; }
  public string AttachedFile { get; set; }
  public string Priority { get; set; }
  public DateTime? StartDate { get; set; }

  public DateTime? DueDate { get; set; }

  public int? Percentagecomplete { get; set; }

  public string? Status { get; set; }

  public int? ParentTaskId { get; set; }

  public string? Frequency { get; set; }
  public int? IntervalValue { get; set; }
  public List<int>? Days { get; set; }
  public List<UserDto>? AssignedUsersIds { get; set; } = [];
  public List<UnitDto>? AssignedUnitIds { get; set; } = [];
}
