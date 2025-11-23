namespace DocTask.Core.Dtos.Units;

public class AssignedUnitDto
{
    public int? UnitId {get; set;}
    public string? UnitName {get; set;}
    public string? Org {get; set;}
    public string? Type {get; set;}
    public Users.UserDto Leader {get; set;}
}