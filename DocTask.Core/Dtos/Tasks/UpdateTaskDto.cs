using System.ComponentModel.DataAnnotations;

namespace DocTask.Core.Dtos.Tasks;

public class UpdateTaskDto
{
    [Required(ErrorMessage = "Title required")]
    public string Title { get; set; } = null!;

    [Required(ErrorMessage = "Description requird")] 
    public string Description { get; set; }

    [Required(ErrorMessage = "StartDate required")]
    public DateTime? StartDate { get; set; }

    [Required(ErrorMessage = "DueDate required")]
    public DateTime? DueDate { get; set; }
    public string? Frequency { get; set; }
    public int? IntervalValue { get; set; }
    public List<int>? Days { get; set; }
}