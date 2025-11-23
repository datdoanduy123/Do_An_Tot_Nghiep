namespace DocTask.Core.Dtos.Units;

public class AssignableUnitsResponseDto
{
    public List<UnitBasicDto> surbodinates { get; set; } = [];
    public List<UnitBasicDto> peers { get; set; } = [];
}