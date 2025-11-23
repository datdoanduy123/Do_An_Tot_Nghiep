namespace DocTask.Core.Models;

public partial class AgentContext
{
    public int Id { get; set; }

    public string ContextName { get; set; } = null!;

    public string ContextDescription { get; set; } = null!;

    public int FileId { get; set; }

    public virtual Uploadfile File { get; set; } = null!;
}
