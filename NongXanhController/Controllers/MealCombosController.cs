using BLL.DTOs;
using BLL.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace NongXanhController.Controllers;

[Route("api/[controller]")]
public class MealCombosController : BaseApiController
{
    private readonly IMealComboService _mealComboService;

    public MealCombosController(IMealComboService mealComboService)
    {
        _mealComboService = mealComboService;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<IEnumerable<MealComboDto>>>> GetAll()
    {
        var result = await _mealComboService.GetAllAsync();
        return SuccessResponse(result);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ApiResponse<MealComboDto>>> GetById(Guid id)
    {
        var result = await _mealComboService.GetByIdAsync(id);
        if (result == null) return ErrorResponse<MealComboDto>("Combo not found", statusCode: 404);
        return SuccessResponse(result);
    }

    [HttpGet("suggestions")]
    public async Task<ActionResult<ApiResponse<IEnumerable<MealComboDto>>>> GetSuggestions(
        [FromQuery] int peopleCount, 
        [FromQuery] int days, 
        [FromQuery] string? dietType)
    {
        if (peopleCount <= 0 || days <= 0) 
            return ErrorResponse<IEnumerable<MealComboDto>>("Invalid input parameters");

        var result = await _mealComboService.GetSuggestionsAsync(peopleCount, days, dietType ?? "");
        return SuccessResponse(result);
    }
}
