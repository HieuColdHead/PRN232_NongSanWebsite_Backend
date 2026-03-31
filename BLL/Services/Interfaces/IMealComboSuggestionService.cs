namespace BLL.Services.Interfaces;

public interface IMealComboSuggestionService
{
    Task<MealComboSuggestionResult> SuggestAsync(MealComboSuggestionRequest request, CancellationToken cancellationToken = default);
}

public sealed record MealComboSuggestionRequest(int PeopleCount, int Days, string DietType);

public sealed record MealComboSuggestionResult(
    bool UsedAi,
    string? AiRawJson,
    IReadOnlyList<MealComboSuggestedItem> Items);

public sealed record MealComboSuggestedItem(
    Guid ProductId,
    decimal Quantity,
    string? Unit,
    string? Group);
