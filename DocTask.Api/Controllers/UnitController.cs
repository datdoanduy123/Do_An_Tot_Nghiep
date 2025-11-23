using DocTask.Core.Dtos.Units;
using DocTask.Core.DTOs.ApiResponses;
using DocTask.Core.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace DocTask.Api.Controllers;

[ApiController]
[Route("/api/v1/unit")]
public class UnitController : ControllerBase
{
  private readonly IUnitService _unitService;
  public UnitController(IUnitService unitService)
  {
    _unitService = unitService;
  }
  [HttpGet]
  [SwaggerOperation(Summary = "Lấy danh sách tất cả đơn vị")]
  public async Task<IActionResult> GetAllUnits()
  {
    var units = await _unitService.GetAllUnitAsync();
    return Ok(new ApiResponse<List<UnitDto>>
    {
      Data = units,
      Message = "Lấy danh sách đơn vị thành công"
    });
  }
}