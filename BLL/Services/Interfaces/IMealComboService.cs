using BLL.DTOs;

namespace BLL.Services.Interfaces;

public interface IMealComboService
{
    Task<IEnumerable<MealComboDto>> GetAllAsync();
    Task<MealComboDto?> GetByIdAsync(Guid id);
    Task<IEnumerable<MealComboDto>> GetSuggestionsAsync(int peopleCount, int days, string dietType);
}

public class MealComboDto
{
    public Guid MealComboId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int TargetPeopleCount { get; set; }
    public int DurationDays { get; set; }
    public string? DietType { get; set; }
    public decimal BasePrice { get; set; }
    public string? ImageUrl { get; set; }
    public List<MealComboItemDto> Items { get; set; } = new();
}

public class MealComboItemDto
{
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public string? Unit { get; set; }
    public decimal Price { get; set; }
    public Guid? VariantId { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }
}
