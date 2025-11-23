using Microsoft.AspNetCore.Http;

namespace DocTask.Core.Dtos.Tasks;

public class UpdateProgressFormDto
{
    public string? Proposal { get; set; }
    public string? Result { get; set; }
    public string? Feedback { get; set; }
    public IFormFile? ReportFile { get; set; }
}


