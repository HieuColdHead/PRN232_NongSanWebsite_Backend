using System.Globalization;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using BLL.Services.Interfaces;
using DAL.Entity;
using DAL.Repositories.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace BLL.Services;

public sealed class MealComboSuggestionService : IMealComboSuggestionService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<MealComboSuggestionService> _logger;
    private readonly IGenericRepository<Product> _productRepository;
    private readonly IGenericRepository<Category> _categoryRepository;

    public MealComboSuggestionService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<MealComboSuggestionService> logger,
        IGenericRepository<Product> productRepository,
        IGenericRepository<Category> categoryRepository)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
        _productRepository = productRepository;
        _categoryRepository = categoryRepository;
    }

    public async Task<MealComboSuggestionResult> SuggestAsync(MealComboSuggestionRequest request, CancellationToken cancellationToken = default)
    {
        var products = (await _productRepository.GetAllAsync()).ToList();
        var categories = (await _categoryRepository.GetAllAsync()).ToList();
        var categoryById = categories.ToDictionary(c => c.CategoryId, c => c);

        var excludedParentNames = GetExcludedParentCategoryNames(request.DietType);

        var candidates = products
            .Where(p => p.ProductVariants.Any(v => !v.IsDeleted && v.StockQuantity > 0))
            .SelectMany(p => p.ProductVariants
                .Where(v => !v.IsDeleted && v.StockQuantity > 0)
                .Select(v =>
                {
                    var (parentId, parentName, categoryName) = ResolveCategoryInfo(p.CategoryId, categoryById);
                    var unitPrice = GetVariantUnitPrice(v);
                    return new Candidate(
                        VariantId: v.VariantId,
                        VariantName: v.VariantName,
                        UnitPrice: unitPrice,
                        StockQuantity: v.StockQuantity,
                        ProductId: p.ProductId,
                        ProductName: p.ProductName,
                        ProductUnit: p.Unit,
                        ParentCategoryId: parentId,
                        ParentCategoryName: parentName,
                        CategoryName: categoryName);
                }))
            .Where(c => c.UnitPrice > 0)
            .Where(c => string.IsNullOrWhiteSpace(c.ParentCategoryName)
                        || !excludedParentNames.Contains(c.ParentCategoryName, StringComparer.OrdinalIgnoreCase))
            .OrderByDescending(c => c.StockQuantity)
            .ThenBy(c => c.UnitPrice)
            .Take(250)
            .ToList();

        // If no data, return empty
        if (candidates.Count == 0)
            return new MealComboSuggestionResult(false, null, Array.Empty<MealComboSuggestedItem>(), 0m);

        var apiKey = _configuration["MegaLLM:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            var heuristic = HeuristicSuggest(request, candidates);
            return new MealComboSuggestionResult(false, null, heuristic, heuristic.Sum(i => i.LineTotal));
        }

        try
        {
            var baseUrl = (_configuration["MegaLLM:BaseUrl"] ?? "https://ai.megallm.io/v1").Trim().TrimEnd('/');
            var modelId = _configuration["MegaLLM:ModelId"] ?? "openai-gpt-oss-20b";

            var systemPrompt = BuildSystemPrompt();
            var userPrompt = BuildUserPrompt(request, candidates);

            var payload = new
            {
                model = modelId,
                messages = new object[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = userPrompt }
                },
                max_tokens = ParseInt(_configuration["MegaLLM:MaxTokens"], 1400),
                temperature = ParseDouble(_configuration["MegaLLM:Temperature"], 0.4)
            };

            using var reqMsg = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/chat/completions");
            reqMsg.Headers.Add("Authorization", $"Bearer {apiKey}");
            reqMsg.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            var res = await _httpClient.SendAsync(reqMsg, cancellationToken);
            var body = await res.Content.ReadAsStringAsync(cancellationToken);
            if (!res.IsSuccessStatusCode)
            {
                _logger.LogWarning("MealComboSuggestionService MegaLLM failed {Status}: {Body}", (int)res.StatusCode, body);
                var heuristic = HeuristicSuggest(request, candidates);
                return new MealComboSuggestionResult(false, null, heuristic, heuristic.Sum(i => i.LineTotal));
            }

            var content = ExtractAssistantContent(body);
            var parsed = TryParseStrictJson(content, out var items);
            if (!parsed || items.Count == 0)
            {
                var heuristic = HeuristicSuggest(request, candidates);
                return new MealComboSuggestionResult(true, content, heuristic, heuristic.Sum(i => i.LineTotal));
            }

            var validated = ValidateAndFix(items, candidates, request);
            if (validated.Count == 0)
            {
                var heuristic = HeuristicSuggest(request, candidates);
                return new MealComboSuggestionResult(true, content, heuristic, heuristic.Sum(i => i.LineTotal));
            }

            var finalized = EnforceParentCategoryCoverage(validated, candidates, request);
            return new MealComboSuggestionResult(true, content, finalized, finalized.Sum(i => i.LineTotal));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "MealComboSuggestionService fallback to heuristic");
            var heuristic = HeuristicSuggest(request, candidates);
            return new MealComboSuggestionResult(false, null, heuristic, heuristic.Sum(i => i.LineTotal));
        }
    }

    private sealed record Candidate(
        Guid VariantId,
        string VariantName,
        decimal UnitPrice,
        int StockQuantity,
        Guid ProductId,
        string ProductName,
        string? ProductUnit,
        Guid? ParentCategoryId,
        string? ParentCategoryName,
        string? CategoryName);

    private static string BuildSystemPrompt()
    {
        return """
               Bạn là AI tạo “combo đi chợ” cho cửa hàng rau củ NongXanh.
               YÊU CẦU BẮT BUỘC:
               - Chỉ được chọn variant có trong danh sách được cung cấp (kèm variantId).
               - Ưu tiên sản phẩm còn hàng.
               - Trả về DUY NHẤT JSON hợp lệ, không thêm chữ giải thích.
               - packs phải là SỐ NGUYÊN >= 1.

               OUTPUT JSON schema:
               {
                 "items": [
                   { "variantId": "GUID", "packs": 1 }
                 ]
               }
               """.Trim();
    }

    private static string BuildUserPrompt(MealComboSuggestionRequest request, List<Candidate> candidates)
    {
        var parentCategoryIds = candidates
            .Where(c => c.ParentCategoryId.HasValue)
            .Select(c => c.ParentCategoryId!.Value)
            .Distinct()
            .ToList();

        var targetItemCount = Math.Clamp(parentCategoryIds.Count, 6, 12);
        var sb = new StringBuilder();
        sb.AppendLine($"peopleCount={request.PeopleCount}, days={request.Days}, dietType={request.DietType}");
        sb.AppendLine($"Hãy chọn khoảng {targetItemCount} món (mỗi món là 1 variant).");
        sb.AppendLine("Ưu tiên chọn đa dạng category cha (mỗi category cha 1 món nếu có thể).");
        sb.AppendLine("DANH SÁCH VARIANT (chỉ được chọn từ đây):");
        foreach (var c in candidates)
        {
            sb.AppendLine($"- variantId={c.VariantId}; variantName={c.VariantName}; productId={c.ProductId}; productName={c.ProductName}; parentCategory={c.ParentCategoryName ?? ""}; category={c.CategoryName ?? ""}; unitPrice={c.UnitPrice.ToString(CultureInfo.InvariantCulture)}; stock={c.StockQuantity}");
        }
        return sb.ToString().Trim();
    }

    private static bool TryParseStrictJson(string text, out List<ParsedItem> items)
    {
        items = new List<ParsedItem>();
        if (string.IsNullOrWhiteSpace(text)) return false;

        try
        {
            using var doc = JsonDocument.Parse(text);
            if (!doc.RootElement.TryGetProperty("items", out var arr) || arr.ValueKind != JsonValueKind.Array)
                return false;

            foreach (var el in arr.EnumerateArray())
            {
                var vid = el.TryGetProperty("variantId", out var v) ? v.GetString() : null;
                if (!Guid.TryParse(vid, out var variantId)) continue;

                var packs = el.TryGetProperty("packs", out var p) && p.TryGetInt32(out var n) ? n : 0;
                if (packs <= 0) continue;

                items.Add(new ParsedItem(variantId, packs));
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    private static IReadOnlyList<MealComboSuggestedItem> ValidateAndFix(
        List<ParsedItem> items,
        List<Candidate> candidates,
        MealComboSuggestionRequest request)
    {
        _ = request;
        var byVariantId = candidates.ToDictionary(x => x.VariantId, x => x);
        var cleaned = new List<MealComboSuggestedItem>();

        foreach (var i in items)
        {
            if (!byVariantId.TryGetValue(i.VariantId, out var c)) continue;
            var packs = Math.Max(1, i.Packs);
            if (packs > c.StockQuantity) packs = Math.Max(1, c.StockQuantity);
            var lineTotal = Decimal.Round(c.UnitPrice * packs, 0);
            cleaned.Add(new MealComboSuggestedItem(
                VariantId: c.VariantId,
                Packs: packs,
                ProductId: c.ProductId,
                ParentCategoryId: c.ParentCategoryId,
                ParentCategoryName: c.ParentCategoryName,
                CategoryName: c.CategoryName,
                UnitPrice: c.UnitPrice,
                LineTotal: lineTotal));
        }

        // De-dup by product then by variant
        var dedup = cleaned
            .GroupBy(x => x.ProductId)
            .Select(g => g.First())
            .GroupBy(x => x.VariantId)
            .Select(g => g.First())
            .ToList();

        return dedup;
    }

    private static IReadOnlyList<MealComboSuggestedItem> HeuristicSuggest(MealComboSuggestionRequest request, List<Candidate> candidates)
    {
        _ = request;

        // Prefer cheapest in-stock variant per parent category
        var byParent = candidates
            .Where(c => c.ParentCategoryId.HasValue)
            .GroupBy(c => c.ParentCategoryId!.Value)
            .Select(g => g.OrderBy(x => x.UnitPrice).First())
            .ToList();

        // If categories are missing, fill with remaining cheapest
        var target = Math.Clamp(byParent.Count, 6, 12);
        var chosen = byParent
            .Concat(candidates.OrderBy(c => c.UnitPrice))
            .GroupBy(c => c.VariantId).Select(g => g.First())
            .Take(target)
            .ToList();

        return chosen.Select(c =>
        {
            var packs = 1;
            var lineTotal = Decimal.Round(c.UnitPrice * packs, 0);
            return new MealComboSuggestedItem(
                VariantId: c.VariantId,
                Packs: packs,
                ProductId: c.ProductId,
                ParentCategoryId: c.ParentCategoryId,
                ParentCategoryName: c.ParentCategoryName,
                CategoryName: c.CategoryName,
                UnitPrice: c.UnitPrice,
                LineTotal: lineTotal);
        }).ToList();
    }

    private sealed record ParsedItem(Guid VariantId, int Packs);

    private static decimal GetVariantUnitPrice(ProductVariant v)
    {
        var discount = v.DiscountPrice.HasValue ? v.DiscountPrice.Value : 0m;
        if (discount > 0 && discount < v.Price)
            return discount;
        return v.Price;
    }

    private static HashSet<string> GetExcludedParentCategoryNames(string dietType)
    {
        var normalized = (dietType ?? string.Empty).Trim().ToLowerInvariant();
        var excluded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (normalized.Contains("ăn chay", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("vegetarian", StringComparison.OrdinalIgnoreCase))
        {
            excluded.Add("Thịt");
            excluded.Add("Cá, hải sản");
        }

        return excluded;
    }

    private static IReadOnlyList<MealComboSuggestedItem> EnforceParentCategoryCoverage(
        IReadOnlyList<MealComboSuggestedItem> current,
        List<Candidate> candidates,
        MealComboSuggestionRequest request)
    {
        _ = request;

        var excluded = GetExcludedParentCategoryNames(request.DietType);
        var requiredParents = candidates
            .Where(c => c.ParentCategoryId.HasValue && !string.IsNullOrWhiteSpace(c.ParentCategoryName))
            .Where(c => !excluded.Contains(c.ParentCategoryName!))
            .Select(c => c.ParentCategoryId!.Value)
            .Distinct()
            .ToList();

        // Start with current items, keep at most 1 per parent category
        var result = current
            .GroupBy(i => i.ParentCategoryId ?? Guid.Empty)
            .Select(g => g.First())
            .ToList();

        var selectedParents = result
            .Where(i => i.ParentCategoryId.HasValue)
            .Select(i => i.ParentCategoryId!.Value)
            .ToHashSet();

        foreach (var parentId in requiredParents)
        {
            if (selectedParents.Contains(parentId))
                continue;

            var pick = candidates
                .Where(c => c.ParentCategoryId == parentId)
                .OrderBy(c => c.UnitPrice)
                .FirstOrDefault();

            if (pick == null) continue;

            var packs = 1;
            var lineTotal = Decimal.Round(pick.UnitPrice * packs, 0);
            result.Add(new MealComboSuggestedItem(
                VariantId: pick.VariantId,
                Packs: packs,
                ProductId: pick.ProductId,
                ParentCategoryId: pick.ParentCategoryId,
                ParentCategoryName: pick.ParentCategoryName,
                CategoryName: pick.CategoryName,
                UnitPrice: pick.UnitPrice,
                LineTotal: lineTotal));
            selectedParents.Add(parentId);
        }

        // Hard cap to keep UX sane: 8 parent categories if available, otherwise up to 12.
        var cap = requiredParents.Count >= 8 ? 8 : Math.Min(12, requiredParents.Count);

        return result
            .OrderBy(i => i.ParentCategoryName, StringComparer.OrdinalIgnoreCase)
            .Take(Math.Max(1, cap))
            .ToList();
    }

    private static (Guid? ParentId, string? ParentName, string? CategoryName) ResolveCategoryInfo(
        Guid? categoryId,
        Dictionary<Guid, Category> categoryById)
    {
        if (!categoryId.HasValue || categoryId.Value == Guid.Empty)
            return (null, null, null);

        if (!categoryById.TryGetValue(categoryId.Value, out var current))
            return (null, null, null);

        var leafName = current.CategoryName;
        while (current.ParentId.HasValue && categoryById.TryGetValue(current.ParentId.Value, out var parent))
        {
            current = parent;
        }

        return (current.CategoryId, current.CategoryName, leafName);
    }

    private static string ExtractAssistantContent(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var choice0 = root.GetProperty("choices")[0];
            var msg = choice0.GetProperty("message");
            return msg.TryGetProperty("content", out var c) ? (c.GetString() ?? string.Empty) : string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static int ParseInt(string? v, int fallback)
        => int.TryParse(v, NumberStyles.Any, CultureInfo.InvariantCulture, out var x) ? x : fallback;

    private static double ParseDouble(string? v, double fallback)
        => double.TryParse(v, NumberStyles.Any, CultureInfo.InvariantCulture, out var x) ? x : fallback;
}
