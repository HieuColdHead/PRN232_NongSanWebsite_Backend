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

        var dietRule = DietRule.From(request.DietType);
        var rng = CreateDeterministicRandom(request);

        var candidates = products
            .Where(p => p.ProductVariants.Any(v => !v.IsDeleted && v.StockQuantity > 0))
            .SelectMany(p => p.ProductVariants
                .Where(v => !v.IsDeleted && v.StockQuantity > 0)
                .Select(v =>
                {
                    var (parentId, parentName, categoryName) = ResolveCategoryInfo(p.CategoryId, categoryById);
                    var unitPrice = GetVariantUnitPrice(v);
                    var foodGroup = MapFoodGroup(parentName);
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
                        CategoryName: categoryName,
                        FoodGroup: foodGroup);
                }))
            .Where(c => c.UnitPrice > 0)
            .Where(c => dietRule.AllowedFoodGroups.Contains(c.FoodGroup))
            .OrderByDescending(c => c.StockQuantity)
            .Take(600)
            .ToList();

        // Shuffle to reduce repeated patterns and price bias
        ShuffleInPlace(candidates, rng);

        // Keep a balanced pool: prioritize in-stock while keeping diversity
        candidates = TakeBalancedCandidates(candidates, dietRule, maxTotal: 350);

        // If no data, return empty
        if (candidates.Count == 0)
            return new MealComboSuggestionResult(false, null, Array.Empty<MealComboSuggestedItem>(), 0m);

        var apiKey = _configuration["MegaLLM:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            var heuristic = HeuristicSuggest(request, candidates, dietRule, rng);
            return new MealComboSuggestionResult(false, null, heuristic, heuristic.Sum(i => i.LineTotal));
        }

        try
        {
            var baseUrl = (_configuration["MegaLLM:BaseUrl"] ?? "https://ai.megallm.io/v1").Trim().TrimEnd('/');
            var modelId = _configuration["MegaLLM:ModelId"] ?? "openai-gpt-oss-20b";

            var systemPrompt = BuildSystemPrompt();
            var userPrompt = BuildUserPrompt(request, candidates, dietRule);

            var payload = new
            {
                model = modelId,
                messages = new object[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = userPrompt }
                },
                max_tokens = ParseInt(_configuration["MegaLLM:MaxTokens"], 1400),
                temperature = ParseDouble(_configuration["MegaLLM:Temperature"], 0.8)
            };

            using var reqMsg = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/chat/completions");
            reqMsg.Headers.Add("Authorization", $"Bearer {apiKey}");
            reqMsg.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            var res = await _httpClient.SendAsync(reqMsg, cancellationToken);
            var body = await res.Content.ReadAsStringAsync(cancellationToken);
            if (!res.IsSuccessStatusCode)
            {
                _logger.LogWarning("MealComboSuggestionService MegaLLM failed {Status}: {Body}", (int)res.StatusCode, body);
                var heuristic = HeuristicSuggest(request, candidates, dietRule, rng);
                return new MealComboSuggestionResult(false, null, heuristic, heuristic.Sum(i => i.LineTotal));
            }

            var content = ExtractAssistantContent(body);
            var parsed = TryParseStrictJson(content, out var items);
            if (!parsed || items.Count == 0)
            {
                var heuristic = HeuristicSuggest(request, candidates, dietRule, rng);
                return new MealComboSuggestionResult(true, content, heuristic, heuristic.Sum(i => i.LineTotal));
            }

            var validated = ValidateAndFix(items, candidates, request);
            if (validated.Count == 0)
            {
                var heuristic = HeuristicSuggest(request, candidates, dietRule, rng);
                return new MealComboSuggestionResult(true, content, heuristic, heuristic.Sum(i => i.LineTotal));
            }

            var finalized = EnforceFoodGroupCoverage(validated, candidates, request, dietRule, rng);
            return new MealComboSuggestionResult(true, content, finalized, finalized.Sum(i => i.LineTotal));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "MealComboSuggestionService fallback to heuristic");
            var heuristic = HeuristicSuggest(request, candidates, dietRule, rng);
            return new MealComboSuggestionResult(false, null, heuristic, heuristic.Sum(i => i.LineTotal));
        }
    }

    private enum FoodGroup
    {
        Unknown = 0,
        ProteinMeat,
        ProteinSeafood,
        ProteinEgg,
        ProteinPlant,
        Dairy,
        Vegetable,
        Fruit,
        Carb,
        SpiceOrOther
    }

    private sealed record DietRule(
        string NormalizedDietType,
        HashSet<FoodGroup> AllowedFoodGroups,
        List<FoodGroup> ProteinRotation)
    {
        public static DietRule From(string dietType)
        {
            var normalized = (dietType ?? string.Empty).Trim();
            var lower = normalized.ToLowerInvariant();

            var allowed = new HashSet<FoodGroup>
            {
                FoodGroup.ProteinMeat,
                FoodGroup.ProteinSeafood,
                FoodGroup.ProteinEgg,
                FoodGroup.ProteinPlant,
                FoodGroup.Dairy,
                FoodGroup.Vegetable,
                FoodGroup.Fruit,
                FoodGroup.Carb,
                FoodGroup.SpiceOrOther,
                FoodGroup.Unknown
            };

            // Defaults: rotate protein to maximize variety across days
            var rotation = new List<FoodGroup>
            {
                FoodGroup.ProteinMeat,
                FoodGroup.ProteinSeafood,
                FoodGroup.ProteinEgg,
                FoodGroup.ProteinPlant
            };

            // Vegetarian / An chay
            if (lower.Contains("ăn chay", StringComparison.OrdinalIgnoreCase)
                || lower.Contains("vegetarian", StringComparison.OrdinalIgnoreCase))
            {
                allowed.Remove(FoodGroup.ProteinMeat);
                allowed.Remove(FoodGroup.ProteinSeafood);
                rotation = new List<FoodGroup> { FoodGroup.ProteinPlant, FoodGroup.ProteinEgg, FoodGroup.Dairy };
            }

            // Keto / Low carb / Diabetes: limit carbs, prefer protein + vegetable.
            if (lower.Contains("keto", StringComparison.OrdinalIgnoreCase)
                || lower.Contains("low carb", StringComparison.OrdinalIgnoreCase)
                || lower.Contains("lowcarb", StringComparison.OrdinalIgnoreCase)
                || lower.Contains("tiểu đường", StringComparison.OrdinalIgnoreCase)
                || lower.Contains("tieu duong", StringComparison.OrdinalIgnoreCase))
            {
                allowed.Remove(FoodGroup.Carb);
                // allow fruit but heuristic will keep it limited
                rotation = rotation.Where(g => g != FoodGroup.ProteinPlant).Concat(new[] { FoodGroup.ProteinPlant }).ToList();
            }

            // Eat clean / Healthy / Giảm cân: keep all groups, but heuristic will bias toward veg/protein.
            _ = lower.Contains("eat clean", StringComparison.OrdinalIgnoreCase)
                || lower.Contains("eatclean", StringComparison.OrdinalIgnoreCase)
                || lower.Contains("healthy", StringComparison.OrdinalIgnoreCase)
                || lower.Contains("giảm cân", StringComparison.OrdinalIgnoreCase)
                || lower.Contains("giam can", StringComparison.OrdinalIgnoreCase);

            // If diet type is custom/other: keep defaults.
            return new DietRule(normalized, allowed, rotation);
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
        string? CategoryName,
        FoodGroup FoodGroup);

    private static string BuildSystemPrompt()
    {
        return """
               Bạn là AI tạo “combo đi chợ” cho cửa hàng rau củ NongXanh.
               YÊU CẦU BẮT BUỘC:
               - Chỉ được chọn variant có trong danh sách được cung cấp (kèm variantId).
               - Ưu tiên sản phẩm còn hàng.
               - KHÔNG cần tối ưu giá, ưu tiên đa dạng món và phù hợp chế độ ăn.
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

    private static string BuildUserPrompt(MealComboSuggestionRequest request, List<Candidate> candidates, DietRule dietRule)
    {
        var targetItemCount = GetTargetUniqueItemCount(request.Days);
        var sb = new StringBuilder();
        sb.AppendLine($"peopleCount={request.PeopleCount}, days={request.Days}, dietType={request.DietType}");
        sb.AppendLine($"Hãy chọn khoảng {targetItemCount} món (mỗi món là 1 variant).");
        sb.AppendLine("Mục tiêu: đa dạng món theo ngày, luân phiên nhóm protein giữa các ngày (không trùng protein chính ngày liền kề).");
        sb.AppendLine("Không cần tối ưu giá.");
        sb.AppendLine($"Nhóm protein ưu tiên xoay vòng: {string.Join(", ", dietRule.ProteinRotation)}");
        sb.AppendLine("DANH SÁCH VARIANT (chỉ được chọn từ đây):");
        foreach (var c in candidates)
        {
            sb.AppendLine($"- variantId={c.VariantId}; variantName={c.VariantName}; productId={c.ProductId}; productName={c.ProductName}; foodGroup={c.FoodGroup}; parentCategory={c.ParentCategoryName ?? ""}; category={c.CategoryName ?? ""}; unitPrice={c.UnitPrice.ToString(CultureInfo.InvariantCulture)}; stock={c.StockQuantity}");
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

    private static IReadOnlyList<MealComboSuggestedItem> HeuristicSuggest(
        MealComboSuggestionRequest request,
        List<Candidate> candidates,
        DietRule dietRule,
        Random rng)
    {
        return SuggestByDaysPlan(request, candidates, dietRule, rng);
    }

    private sealed record ParsedItem(Guid VariantId, int Packs);

    private static decimal GetVariantUnitPrice(ProductVariant v)
    {
        var discount = v.DiscountPrice.HasValue ? v.DiscountPrice.Value : 0m;
        if (discount > 0 && discount < v.Price)
            return discount;
        return v.Price;
    }

    private static FoodGroup MapFoodGroup(string? parentCategoryName)
    {
        var n = (parentCategoryName ?? string.Empty).Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(n)) return FoodGroup.Unknown;

        if (n.Contains("thịt", StringComparison.OrdinalIgnoreCase)) return FoodGroup.ProteinMeat;
        if (n.Contains("cá", StringComparison.OrdinalIgnoreCase) || n.Contains("hải sản", StringComparison.OrdinalIgnoreCase) || n.Contains("hai san", StringComparison.OrdinalIgnoreCase))
            return FoodGroup.ProteinSeafood;
        if (n.Contains("trứng", StringComparison.OrdinalIgnoreCase) || n.Contains("trung", StringComparison.OrdinalIgnoreCase))
            return FoodGroup.ProteinEgg;
        if (n.Contains("sữa", StringComparison.OrdinalIgnoreCase) || n.Contains("sua", StringComparison.OrdinalIgnoreCase))
            return FoodGroup.Dairy;
        if (n.Contains("rau", StringComparison.OrdinalIgnoreCase) || n.Contains("củ", StringComparison.OrdinalIgnoreCase) || n.Contains("cu", StringComparison.OrdinalIgnoreCase) || n.Contains("quả", StringComparison.OrdinalIgnoreCase) || n.Contains("qua", StringComparison.OrdinalIgnoreCase))
            return FoodGroup.Vegetable;
        if (n.Contains("trái", StringComparison.OrdinalIgnoreCase) || n.Contains("trai", StringComparison.OrdinalIgnoreCase) || n.Contains("hoa quả", StringComparison.OrdinalIgnoreCase))
            return FoodGroup.Fruit;
        if (n.Contains("gạo", StringComparison.OrdinalIgnoreCase) || n.Contains("gao", StringComparison.OrdinalIgnoreCase) || n.Contains("bánh", StringComparison.OrdinalIgnoreCase) || n.Contains("banh", StringComparison.OrdinalIgnoreCase) || n.Contains("ngũ cốc", StringComparison.OrdinalIgnoreCase) || n.Contains("ngu coc", StringComparison.OrdinalIgnoreCase))
            return FoodGroup.Carb;
        if (n.Contains("gia vị", StringComparison.OrdinalIgnoreCase) || n.Contains("gia vi", StringComparison.OrdinalIgnoreCase) || n.Contains("thảo mộc", StringComparison.OrdinalIgnoreCase) || n.Contains("thao moc", StringComparison.OrdinalIgnoreCase))
            return FoodGroup.SpiceOrOther;
        if (n.Contains("đậu", StringComparison.OrdinalIgnoreCase) || n.Contains("dau", StringComparison.OrdinalIgnoreCase) || n.Contains("hạt", StringComparison.OrdinalIgnoreCase) || n.Contains("hat", StringComparison.OrdinalIgnoreCase))
            return FoodGroup.ProteinPlant;

        return FoodGroup.Unknown;
    }

    private static IReadOnlyList<MealComboSuggestedItem> EnforceFoodGroupCoverage(
        IReadOnlyList<MealComboSuggestedItem> current,
        List<Candidate> candidates,
        MealComboSuggestionRequest request,
        DietRule dietRule,
        Random rng)
    {
        var targetUnique = GetTargetUniqueItemCount(request.Days);

        var byVariant = candidates.ToDictionary(c => c.VariantId, c => c);
        var result = current
            .GroupBy(i => i.VariantId)
            .Select(g => g.First())
            .ToList();

        var selectedVariantIds = result.Select(r => r.VariantId).ToHashSet();
        var selectedProductIds = result.Select(r => r.ProductId).ToHashSet();

        bool HasGroup(FoodGroup g)
        {
            return result.Any(i =>
                byVariant.TryGetValue(i.VariantId, out var c) && c.FoodGroup == g);
        }

        Candidate? Pick(FoodGroup g)
        {
            var pool = candidates
                .Where(c => c.FoodGroup == g)
                .Where(c => c.StockQuantity > 0)
                .Where(c => !selectedVariantIds.Contains(c.VariantId))
                .Where(c => !selectedProductIds.Contains(c.ProductId))
                .Take(120)
                .ToList();

            if (pool.Count == 0) return null;
            pool = pool.OrderByDescending(x => x.StockQuantity).Take(60).ToList();
            return pool[rng.Next(pool.Count)];
        }

        void AddCandidate(Candidate c)
        {
            var packs = CalculatePacks(request, c.FoodGroup);
            packs = Math.Max(1, Math.Min(packs, Math.Max(1, c.StockQuantity)));
            var lineTotal = Decimal.Round(c.UnitPrice * packs, 0);
            result.Add(new MealComboSuggestedItem(
                VariantId: c.VariantId,
                Packs: packs,
                ProductId: c.ProductId,
                ParentCategoryId: c.ParentCategoryId,
                ParentCategoryName: c.ParentCategoryName,
                CategoryName: c.CategoryName,
                UnitPrice: c.UnitPrice,
                LineTotal: lineTotal));
            selectedVariantIds.Add(c.VariantId);
            selectedProductIds.Add(c.ProductId);
        }

        // Ensure at least 2 different protein groups if possible
        var proteinGroups = dietRule.ProteinRotation
            .Where(dietRule.AllowedFoodGroups.Contains)
            .Distinct()
            .ToList();

        foreach (var pg in proteinGroups)
        {
            if (result.Count >= targetUnique) break;
            if (HasGroup(pg)) continue;
            var pick = Pick(pg);
            if (pick != null) AddCandidate(pick);
        }

        // Ensure vegetables exist
        if (result.Count < targetUnique && dietRule.AllowedFoodGroups.Contains(FoodGroup.Vegetable) && !HasGroup(FoodGroup.Vegetable))
        {
            var pick = Pick(FoodGroup.Vegetable);
            if (pick != null) AddCandidate(pick);
        }

        // Ensure dairy or fruit if allowed
        if (result.Count < targetUnique && dietRule.AllowedFoodGroups.Contains(FoodGroup.Dairy) && !HasGroup(FoodGroup.Dairy))
        {
            var pick = Pick(FoodGroup.Dairy);
            if (pick != null) AddCandidate(pick);
        }

        if (result.Count < targetUnique && dietRule.AllowedFoodGroups.Contains(FoodGroup.Fruit) && !HasGroup(FoodGroup.Fruit))
        {
            var pick = Pick(FoodGroup.Fruit);
            if (pick != null) AddCandidate(pick);
        }

        // Cap to targetUnique
        return result
            .GroupBy(i => i.VariantId)
            .Select(g => g.First())
            .Take(targetUnique)
            .ToList();
    }

    private static int GetTargetUniqueItemCount(int days)
        => Math.Clamp(days * 3, 8, 30);

    private static Random CreateDeterministicRandom(MealComboSuggestionRequest request)
    {
        var s = $"{request.PeopleCount}|{request.Days}|{request.DietType}".GetHashCode();
        return new Random(s);
    }

    private static void ShuffleInPlace<T>(IList<T> list, Random rng)
    {
        for (var i = list.Count - 1; i > 0; i--)
        {
            var j = rng.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    private static List<Candidate> TakeBalancedCandidates(List<Candidate> candidates, DietRule dietRule, int maxTotal)
    {
        // Keep a balanced number per FoodGroup to avoid one group (e.g., veggies) taking over.
        var maxPerGroup = Math.Max(10, maxTotal / Math.Max(1, dietRule.AllowedFoodGroups.Count));
        var result = new List<Candidate>(Math.Min(maxTotal, candidates.Count));

        foreach (var g in dietRule.AllowedFoodGroups)
        {
            if (result.Count >= maxTotal) break;
            result.AddRange(candidates.Where(c => c.FoodGroup == g).Take(maxPerGroup));
        }

        if (result.Count < maxTotal)
        {
            var remaining = candidates
                .Where(c => result.All(x => x.VariantId != c.VariantId))
                .Take(maxTotal - result.Count);
            result.AddRange(remaining);
        }

        return result
            .GroupBy(c => c.VariantId)
            .Select(g => g.First())
            .Take(maxTotal)
            .ToList();
    }

    private static IReadOnlyList<MealComboSuggestedItem> SuggestByDaysPlan(
        MealComboSuggestionRequest request,
        List<Candidate> candidates,
        DietRule dietRule,
        Random rng)
    {
        var targetUnique = GetTargetUniqueItemCount(request.Days);
        var chosen = new List<Candidate>();
        var chosenVariantIds = new HashSet<Guid>();
        var chosenProductIds = new HashSet<Guid>();

        var byGroup = candidates
            .GroupBy(c => c.FoodGroup)
            .ToDictionary(g => g.Key, g => g.ToList());

        Candidate? PickFromGroup(FoodGroup g, Func<Candidate, bool>? extra = null)
        {
            if (!byGroup.TryGetValue(g, out var pool) || pool.Count == 0) return null;
            var filtered = pool
                .Where(c => c.StockQuantity > 0)
                .Where(c => !chosenVariantIds.Contains(c.VariantId))
                .Where(c => !chosenProductIds.Contains(c.ProductId))
                .Where(c => extra == null || extra(c))
                .ToList();
            if (filtered.Count == 0) return null;
            // Pick biased by stock, but not price
            filtered = filtered.OrderByDescending(x => x.StockQuantity).Take(80).ToList();
            return filtered[rng.Next(filtered.Count)];
        }

        // Rotate protein across days, avoid repeating on consecutive days
        var rotation = dietRule.ProteinRotation.Where(dietRule.AllowedFoodGroups.Contains).ToList();
        if (rotation.Count == 0)
        {
            rotation = new List<FoodGroup> { FoodGroup.ProteinPlant, FoodGroup.ProteinEgg, FoodGroup.Dairy };
        }

        FoodGroup? lastProtein = null;
        for (var day = 0; day < Math.Max(1, request.Days); day++)
        {
            if (chosen.Count >= targetUnique) break;

            var proteinGroup = rotation[day % rotation.Count];
            if (lastProtein.HasValue && proteinGroup == lastProtein.Value && rotation.Count > 1)
            {
                proteinGroup = rotation[(day + 1) % rotation.Count];
            }

            var proteinPick = PickFromGroup(proteinGroup);
            if (proteinPick != null)
            {
                chosen.Add(proteinPick);
                chosenVariantIds.Add(proteinPick.VariantId);
                chosenProductIds.Add(proteinPick.ProductId);
                lastProtein = proteinGroup;
            }

            // Vegetables: 1-2 per day for most diets
            var veg1 = PickFromGroup(FoodGroup.Vegetable);
            if (veg1 != null && chosen.Count < targetUnique)
            {
                chosen.Add(veg1);
                chosenVariantIds.Add(veg1.VariantId);
                chosenProductIds.Add(veg1.ProductId);
            }

            if (chosen.Count < targetUnique && day % 2 == 0)
            {
                var veg2 = PickFromGroup(FoodGroup.Vegetable);
                if (veg2 != null)
                {
                    chosen.Add(veg2);
                    chosenVariantIds.Add(veg2.VariantId);
                    chosenProductIds.Add(veg2.ProductId);
                }
            }

            // Fruit or Dairy occasionally, depending on diet
            var lower = dietRule.NormalizedDietType.ToLowerInvariant();
            var allowFruitOften = !(lower.Contains("keto", StringComparison.OrdinalIgnoreCase)
                                   || lower.Contains("low carb", StringComparison.OrdinalIgnoreCase)
                                   || lower.Contains("tiểu đường", StringComparison.OrdinalIgnoreCase)
                                   || lower.Contains("tieu duong", StringComparison.OrdinalIgnoreCase));

            if (chosen.Count < targetUnique && day % 3 == 0 && allowFruitOften)
            {
                var fruit = PickFromGroup(FoodGroup.Fruit);
                if (fruit != null)
                {
                    chosen.Add(fruit);
                    chosenVariantIds.Add(fruit.VariantId);
                    chosenProductIds.Add(fruit.ProductId);
                }
            }

            if (chosen.Count < targetUnique && day % 3 == 1)
            {
                var dairy = PickFromGroup(FoodGroup.Dairy);
                if (dairy != null)
                {
                    chosen.Add(dairy);
                    chosenVariantIds.Add(dairy.VariantId);
                    chosenProductIds.Add(dairy.ProductId);
                }
            }
        }

        // Fill remaining with a mix of allowed groups, prioritizing proteins then veg
        var fillOrder = new[]
        {
            FoodGroup.ProteinSeafood, FoodGroup.ProteinMeat, FoodGroup.ProteinEgg, FoodGroup.ProteinPlant,
            FoodGroup.Vegetable, FoodGroup.Dairy, FoodGroup.Fruit, FoodGroup.Carb, FoodGroup.SpiceOrOther, FoodGroup.Unknown
        }.Where(dietRule.AllowedFoodGroups.Contains).ToList();

        foreach (var g in fillOrder)
        {
            while (chosen.Count < targetUnique)
            {
                var pick = PickFromGroup(g);
                if (pick == null) break;
                chosen.Add(pick);
                chosenVariantIds.Add(pick.VariantId);
                chosenProductIds.Add(pick.ProductId);
            }
        }

        // Convert to suggested items with packs calculated
        return chosen
            .Take(targetUnique)
            .Select(c =>
            {
                var packs = CalculatePacks(request, c.FoodGroup);
                packs = Math.Max(1, Math.Min(packs, Math.Max(1, c.StockQuantity)));
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
            })
            .ToList();
    }

    private static int CalculatePacks(MealComboSuggestionRequest request, FoodGroup group)
    {
        var people = Math.Max(1, request.PeopleCount);
        var days = Math.Max(1, request.Days);

        return group switch
        {
            FoodGroup.ProteinMeat or FoodGroup.ProteinSeafood => (int)Math.Ceiling((people * days) / 6m),
            FoodGroup.ProteinEgg => (int)Math.Ceiling((people * days) / 8m),
            FoodGroup.ProteinPlant => (int)Math.Ceiling((people * days) / 7m),
            FoodGroup.Vegetable => (int)Math.Ceiling((people * days) / 5m),
            FoodGroup.Fruit => (int)Math.Ceiling((people * days) / 10m),
            FoodGroup.Dairy => (int)Math.Ceiling((people * days) / 12m),
            FoodGroup.Carb => (int)Math.Ceiling((people * days) / 10m),
            _ => 1
        };
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
