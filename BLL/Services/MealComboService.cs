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
    private readonly IMealComboSuggestionService _suggestionService;

    public MealComboService(
        IGenericRepository<MealCombo> mealComboRepository,
        IGenericRepository<Product> productRepository,
        IMealComboSuggestionService suggestionService)
    {
        _mealComboRepository = mealComboRepository;
        _productRepository = productRepository;
        _suggestionService = suggestionService;
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
        // 1) Reuse exact match if exists (avoid DB spam), but peopleCount/days are not limited to fixed sets.
        var existing = await _mealComboRepository.FindAsync(c =>
            c.TargetPeopleCount == peopleCount &&
            c.DurationDays == days &&
            (string.IsNullOrEmpty(dietType) || c.DietType == dietType) &&
            c.IsActive);

        var first = existing.FirstOrDefault();
        if (first != null)
            return new[] { MapToDto(first) };

        // 2) AI/heuristic suggestion -> persist to DB and return real mealComboId
        var suggestion = await _suggestionService.SuggestAsync(new MealComboSuggestionRequest(peopleCount, days, dietType));

        var mealCombo = new MealCombo
        {
            MealComboId = Guid.NewGuid(),
            Name = $"Combo {days} ngày cho {peopleCount} người - {dietType}",
            Description = suggestion.UsedAi
                ? "Hệ thống AI tự động gợi ý dựa trên nhu cầu và tồn kho hiện tại."
                : "Hệ thống tự động gợi ý dựa trên tồn kho hiện tại.",
            TargetPeopleCount = peopleCount,
            DurationDays = days,
            DietType = string.IsNullOrWhiteSpace(dietType) ? null : dietType,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            Items = suggestion.Items.Select(i => new MealComboItem
            {
                MealComboItemId = Guid.NewGuid(),
                MealComboId = Guid.Empty, // set after combo id assigned below
                ProductId = i.ProductId,
                Quantity = i.Packs,
                Unit = null
            }).ToList()
        };

        foreach (var item in mealCombo.Items)
            item.MealComboId = mealCombo.MealComboId;

        mealCombo.BasePrice = suggestion.TotalPrice > 0
            ? suggestion.TotalPrice
            : await CalculateComboPriceAsync(mealCombo);

        await _mealComboRepository.AddAsync(mealCombo);
        await _mealComboRepository.SaveChangesAsync();

        // Return the persisted combo (should have Items+Product loaded via repository includes)
        var persisted = await _mealComboRepository.GetByIdAsync(mealCombo.MealComboId);
        return persisted != null ? new[] { MapToDto(persisted) } : new[] { MapToDto(mealCombo) };
    }

    private async Task<decimal> CalculateComboPriceAsync(MealCombo combo)
    {
        // Price is estimated from products' cheapest in-stock variant (prefer DiscountPrice),
        // multiplied by the suggested quantity.
        var allProducts = await _productRepository.GetAllAsync();
        var byId = allProducts.ToDictionary(p => p.ProductId, p => p);

        decimal total = 0m;
        foreach (var item in combo.Items)
        {
            if (!byId.TryGetValue(item.ProductId, out var p)) continue;

            var (_, _, unitPrice) = GetCheapestInStockVariant(p);
            if (unitPrice <= 0) continue;

            total += unitPrice * item.Quantity;
        }

        // If price data is missing (total=0), fallback to a rough estimate so cart won't be 0.
        var rounded = Decimal.Round(total, 0);
        if (rounded <= 0)
            return CalculateFallbackPrice(combo.TargetPeopleCount, combo.DurationDays);

        return rounded;
    }

    private static decimal CalculateFallbackPrice(int peopleCount, int days)
    {
        // Rough estimate: 50,000 VND per person per day
        return peopleCount > 0 && days > 0 ? peopleCount * days * 50000m : 0m;
    }

    private static (Guid? VariantId, string? VariantName, decimal UnitPrice) GetCheapestInStockVariant(Product p)
    {
        var cheapest = p.ProductVariants
            .Where(v => !v.IsDeleted && v.StockQuantity > 0)
            .Select(v => new
            {
                v.VariantId,
                v.VariantName,
                UnitPrice = v.DiscountPrice.HasValue && v.DiscountPrice.Value > 0 && v.DiscountPrice.Value < v.Price
                    ? v.DiscountPrice.Value
                    : v.Price
            })
            .Where(x => x.UnitPrice > 0)
            .OrderBy(x => x.UnitPrice)
            .FirstOrDefault();

        if (cheapest != null)
        {
            return (cheapest.VariantId, cheapest.VariantName, cheapest.UnitPrice);
        }

        if (p.DiscountPrice.HasValue && p.DiscountPrice.Value > 0 && p.DiscountPrice.Value < p.BasePrice)
        {
            return (null, null, p.DiscountPrice.Value);
        }

        return (null, null, p.BasePrice);
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
            Items = combo.Items.Select(i =>
            {
                var (variantId, variantName, unitPrice) = i.Product != null
                    ? GetCheapestInStockVariant(i.Product)
                    : (null, null, 0m);

                return new MealComboItemDto
                {
                    ProductId = i.ProductId,
                    ProductName = i.Product?.ProductName ?? "Sản phẩm",
                    Quantity = i.Quantity,
                    Unit = i.Unit,
                    Price = unitPrice,
                    VariantId = variantId,
                    VariantName = variantName,
                    UnitPrice = unitPrice,
                    LineTotal = Decimal.Round(unitPrice * i.Quantity, 0)
                };
            }).ToList()
        };
    }
}
