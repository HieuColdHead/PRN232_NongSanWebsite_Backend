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

    public MealComboSuggestionService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<MealComboSuggestionService> logger,
        IGenericRepository<Product> productRepository)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
        _productRepository = productRepository;
    }

    public async Task<MealComboSuggestionResult> SuggestAsync(MealComboSuggestionRequest request, CancellationToken cancellationToken = default)
    {
        var products = (await _productRepository.GetAllAsync()).ToList();

        var candidates = products
            .Where(p => p.ProductVariants.Any(v => !v.IsDeleted && v.StockQuantity > 0))
            .Select(p => new Candidate(
                p.ProductId,
                p.ProductName,
                p.Unit,
                Group: ClassifyGroup(p),
                Price: GetEffectiveUnitPrice(p),
                InStock: p.ProductVariants.Sum(v => v.IsDeleted ? 0 : v.StockQuantity)))
            .Where(c => c.Price > 0)
            .OrderByDescending(c => c.InStock)
            .ThenBy(c => c.Price)
            .Take(120)
            .ToList();

        // If no data, return empty
        if (candidates.Count == 0)
            return new MealComboSuggestionResult(false, null, Array.Empty<MealComboSuggestedItem>());

        var apiKey = _configuration["MegaLLM:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            var heuristic = HeuristicSuggest(request, candidates);
            return new MealComboSuggestionResult(false, null, heuristic);
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
                return new MealComboSuggestionResult(false, null, heuristic);
            }

            var content = ExtractAssistantContent(body);
            var parsed = TryParseStrictJson(content, out var items);
            if (!parsed || items.Count == 0)
            {
                var heuristic = HeuristicSuggest(request, candidates);
                return new MealComboSuggestionResult(true, content, heuristic);
            }

            var validated = ValidateAndFix(items, candidates, request);
            if (validated.Count == 0)
            {
                var heuristic = HeuristicSuggest(request, candidates);
                return new MealComboSuggestionResult(true, content, heuristic);
            }

            return new MealComboSuggestionResult(true, content, validated);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "MealComboSuggestionService fallback to heuristic");
            var heuristic = HeuristicSuggest(request, candidates);
            return new MealComboSuggestionResult(false, null, heuristic);
        }
    }

    private sealed record Candidate(Guid ProductId, string Name, string? Unit, string Group, decimal Price, int InStock);

    private static string BuildSystemPrompt()
    {
        return """
               Bạn là AI tạo “combo đi chợ” cho cửa hàng rau củ NongXanh.
               YÊU CẦU BẮT BUỘC:
               - Chỉ được chọn sản phẩm có trong danh sách được cung cấp (kèm productId).
               - Ưu tiên sản phẩm còn hàng.
               - Cân bằng tối thiểu 3 nhóm: rau_lá, củ, trái_cây (nếu dietType không cấm).
               - Trả về DUY NHẤT JSON hợp lệ, không thêm chữ giải thích.

               OUTPUT JSON schema:
               {
                 "items": [
                   { "productId": "GUID", "quantity": 1.5, "unit": "kg", "group": "rau_lá|củ|trái_cây|khác" }
                 ]
               }
               """.Trim();
    }

    private static string BuildUserPrompt(MealComboSuggestionRequest request, List<Candidate> candidates)
    {
        var targetItemCount = Math.Clamp((int)Math.Ceiling(request.PeopleCount * request.Days * 0.6m), 5, 18);
        var sb = new StringBuilder();
        sb.AppendLine($"peopleCount={request.PeopleCount}, days={request.Days}, dietType={request.DietType}");
        sb.AppendLine($"Hãy chọn khoảng {targetItemCount} sản phẩm.");
        sb.AppendLine("DANH SÁCH SẢN PHẨM (chỉ được chọn từ đây):");
        foreach (var c in candidates)
        {
            sb.AppendLine($"- productId={c.ProductId}; name={c.Name}; unit={c.Unit ?? ""}; group={c.Group}; price={c.Price.ToString(CultureInfo.InvariantCulture)}; stock={c.InStock}");
        }
        return sb.ToString().Trim();
    }

    private static bool TryParseStrictJson(string text, out List<MealComboSuggestedItem> items)
    {
        items = new List<MealComboSuggestedItem>();
        if (string.IsNullOrWhiteSpace(text)) return false;

        try
        {
            using var doc = JsonDocument.Parse(text);
            if (!doc.RootElement.TryGetProperty("items", out var arr) || arr.ValueKind != JsonValueKind.Array)
                return false;

            foreach (var el in arr.EnumerateArray())
            {
                var pid = el.TryGetProperty("productId", out var p) ? p.GetString() : null;
                if (!Guid.TryParse(pid, out var productId)) continue;

                var qty = el.TryGetProperty("quantity", out var q) && q.TryGetDecimal(out var d) ? d : 0m;
                if (qty <= 0) continue;

                var unit = el.TryGetProperty("unit", out var u) ? u.GetString() : null;
                var group = el.TryGetProperty("group", out var g) ? g.GetString() : null;
                items.Add(new MealComboSuggestedItem(productId, qty, unit, group));
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    private static IReadOnlyList<MealComboSuggestedItem> ValidateAndFix(
        List<MealComboSuggestedItem> items,
        List<Candidate> candidates,
        MealComboSuggestionRequest request)
    {
        var byId = candidates.ToDictionary(x => x.ProductId, x => x);
        var cleaned = new List<MealComboSuggestedItem>();

        foreach (var i in items)
        {
            if (!byId.TryGetValue(i.ProductId, out var c)) continue;
            var qty = i.Quantity <= 0 ? 0 : i.Quantity;
            if (qty <= 0) continue;
            var unit = string.IsNullOrWhiteSpace(i.Unit) ? c.Unit : i.Unit;
            var group = string.IsNullOrWhiteSpace(i.Group) ? c.Group : i.Group!;
            cleaned.Add(new MealComboSuggestedItem(i.ProductId, Decimal.Round(qty, 4), unit, group));
        }

        // Ensure group coverage (rau_lá, củ, trái_cây)
        var required = new[] { "rau_lá", "củ", "trái_cây" };
        foreach (var g in required)
        {
            if (cleaned.Any(x => string.Equals(x.Group, g, StringComparison.OrdinalIgnoreCase)))
                continue;

            var pick = candidates.FirstOrDefault(c => string.Equals(c.Group, g, StringComparison.OrdinalIgnoreCase));
            if (pick != null)
            {
                cleaned.Add(new MealComboSuggestedItem(pick.ProductId, DefaultQuantity(request, pick.Group), pick.Unit, pick.Group));
            }
        }

        // Cap size
        return cleaned
            .GroupBy(x => x.ProductId)
            .Select(g => g.First())
            .Take(20)
            .ToList();
    }

    private static IReadOnlyList<MealComboSuggestedItem> HeuristicSuggest(MealComboSuggestionRequest request, List<Candidate> candidates)
    {
        var targetItemCount = Math.Clamp((int)Math.Ceiling(request.PeopleCount * request.Days * 0.6m), 6, 18);

        var pickGroup = new Func<string, int, List<Candidate>>( (group, count) =>
            candidates.Where(c => string.Equals(c.Group, group, StringComparison.OrdinalIgnoreCase))
                .OrderBy(c => c.Price)
                .Take(count)
                .ToList());

        var leaf = pickGroup("rau_lá", Math.Max(2, targetItemCount / 3));
        var root = pickGroup("củ", Math.Max(2, targetItemCount / 3));
        var fruit = pickGroup("trái_cây", Math.Max(1, targetItemCount / 4));

        var chosen = leaf.Concat(root).Concat(fruit)
            .Concat(candidates.Where(c => c.Group == "khác").Take(2))
            .GroupBy(c => c.ProductId).Select(g => g.First())
            .Take(targetItemCount)
            .ToList();

        return chosen.Select(c => new MealComboSuggestedItem(
            c.ProductId,
            DefaultQuantity(request, c.Group),
            c.Unit,
            c.Group)).ToList();
    }

    private static decimal DefaultQuantity(MealComboSuggestionRequest request, string group)
    {
        // Heuristic quantity: scale by people*days; keep reasonable ranges.
        var scale = request.PeopleCount * request.Days;
        return group switch
        {
            "rau_lá" => Decimal.Round(Math.Clamp(scale * 0.10m, 0.5m, 6m), 4),
            "củ" => Decimal.Round(Math.Clamp(scale * 0.12m, 0.5m, 8m), 4),
            "trái_cây" => Decimal.Round(Math.Clamp(scale * 0.10m, 0.5m, 8m), 4),
            _ => Decimal.Round(Math.Clamp(scale * 0.06m, 0.25m, 5m), 4),
        };
    }

    private static string ClassifyGroup(Product p)
    {
        var name = (p.ProductName ?? string.Empty).ToLowerInvariant();
        if (name.Contains("rau") || name.Contains("cải") || name.Contains("bông cải") || name.Contains("súp lơ") || name.Contains("xà lách") || name.Contains("cải bó xôi") || name.Contains("rau bina"))
            return "rau_lá";

        if (name.Contains("cà rốt") || name.Contains("khoai") || name.Contains("củ") || name.Contains("hành") || name.Contains("tỏi") || name.Contains("gừng") || name.Contains("bí") || name.Contains("bầu") || name.Contains("mướp") || name.Contains("cà chua"))
            return "củ";

        if (name.Contains("chuối") || name.Contains("táo") || name.Contains("lê") || name.Contains("cam") || name.Contains("bưởi") || name.Contains("ổi") || name.Contains("xoài") || name.Contains("dưa") || name.Contains("nho") || name.Contains("thanh long"))
            return "trái_cây";

        return "khác";
    }

    private static decimal GetEffectiveUnitPrice(Product p)
    {
        // Prefer cheapest in-stock variant (discount if available), else fallback to product base price/discount.
        var inStockVariants = p.ProductVariants
            .Where(v => !v.IsDeleted && v.StockQuantity > 0)
            .Select(v => v.DiscountPrice.HasValue && v.DiscountPrice.Value > 0 && v.DiscountPrice.Value < v.Price ? v.DiscountPrice.Value : v.Price)
            .Where(x => x > 0)
            .ToList();

        if (inStockVariants.Count > 0)
            return inStockVariants.Min();

        if (p.DiscountPrice.HasValue && p.DiscountPrice.Value > 0 && p.DiscountPrice.Value < p.BasePrice)
            return p.DiscountPrice.Value;

        return p.BasePrice;
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
