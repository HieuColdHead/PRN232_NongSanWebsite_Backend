using BLL.DTOs;
using BLL.Services.Interfaces;
using DAL.Entity;
using DAL.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BLL.Services;

public class MealComboService : IMealComboService
{
    private readonly IGenericRepository<MealCombo> _mealComboRepository;
    private readonly IGenericRepository<Product> _productRepository;

    public MealComboService(
        IGenericRepository<MealCombo> mealComboRepository,
        IGenericRepository<Product> productRepository)
    {
        _mealComboRepository = mealComboRepository;
        _productRepository = productRepository;
    }

    public async Task<IEnumerable<MealComboDto>> GetAllAsync()
    {
        var combos = await _mealComboRepository.GetAllAsync();
        return combos.Select(MapToDto);
    }

    public async Task<MealComboDto?> GetByIdAsync(Guid id)
    {
        var combo = await _mealComboRepository.GetByIdAsync(id);
        return combo != null ? MapToDto(combo) : null;
    }

    public async Task<IEnumerable<MealComboDto>> GetSuggestionsAsync(int peopleCount, int days, string dietType)
    {
        // 1. Try to find exact matches in the database
        var combos = await _mealComboRepository.FindAsync(c => 
            c.TargetPeopleCount == peopleCount && 
            c.DurationDays == days && 
            (string.IsNullOrEmpty(dietType) || c.DietType == dietType) &&
            c.IsActive);

        if (combos.Any())
        {
            return combos.Select(MapToDto);
        }

        // 2. Dynamic Suggestion Logic (Fallback)
        // If no predefined combo exists, we could "build" one on the fly.
        // For this MVP, we will return a mock suggestion if no DB record found
        // or just return from a "Market Basket" pool.
        
        return new List<MealComboDto> 
        { 
            new MealComboDto
            {
                MealComboId = Guid.NewGuid(),
                Name = $"Combo {days} ngày cho {peopleCount} người - {dietType}",
                Description = "Hệ thống tự động gợi ý dựa trên nhu cầu của bạn.",
                TargetPeopleCount = peopleCount,
                DurationDays = days,
                DietType = dietType,
                BasePrice = CalculateMockPrice(peopleCount, days),
                Items = await GetRandomItemsAsync(peopleCount, days)
            }
        };
    }

    private decimal CalculateMockPrice(int people, int days)
    {
        // Rough estimate: 50,000 VND per person per day
        return people * days * 50000m;
    }

    private async Task<List<MealComboItemDto>> GetRandomItemsAsync(int people, int days)
    {
        // Pick some products to fill the basket
        var allProducts = (await _productRepository.GetAllAsync()).Take(5).ToList();
        return allProducts.Select(p => new MealComboItemDto
        {
            ProductId = p.ProductId,
            ProductName = p.ProductName,
            Quantity = (decimal)(people * days * 0.5), // Mock quantity
            Unit = p.Unit ?? "kg",
            Price = p.BasePrice
        }).ToList();
    }

    private MealComboDto MapToDto(MealCombo combo)
    {
        return new MealComboDto
        {
            MealComboId = combo.MealComboId,
            Name = combo.Name,
            Description = combo.Description,
            TargetPeopleCount = combo.TargetPeopleCount,
            DurationDays = combo.DurationDays,
            DietType = combo.DietType,
            BasePrice = combo.BasePrice,
            ImageUrl = combo.ImageUrl,
            Items = combo.Items.Select(i => new MealComboItemDto
            {
                ProductId = i.ProductId,
                ProductName = i.Product?.ProductName ?? "Sản phẩm",
                Quantity = i.Quantity,
                Unit = i.Unit,
                Price = i.Product?.BasePrice ?? 0
            }).ToList()
        };
    }
}
