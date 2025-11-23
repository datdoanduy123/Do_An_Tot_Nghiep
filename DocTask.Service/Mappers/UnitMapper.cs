using DocTask.Core.Dtos.Units;
using DocTask.Core.Models;

namespace DocTask.Service.Mappers;

public static class UnitMapper
{
    public static UnitBasicDto ToUnitBasicDto(this Unit unit)
    {
        var leader = unit.Unitusers?.FirstOrDefault(u => u.Level == 1);
        return new UnitBasicDto
        {
            UnitId = unit.UnitId,
            Org = unit.Org.OrgName,
            UnitName = unit.UnitName,
            UserId = leader?.User?.UserId ?? 0,
            FullName = leader?.User?.FullName ?? "",
            Type = unit.Type
        };
    }
}