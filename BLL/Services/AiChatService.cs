using System.Globalization;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using BLL.DTOs;
using BLL.Services.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace BLL.Services;

public sealed class AiChatService : IAiChatService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AiChatService> _logger;
    private readonly IProductService _productService;
    private readonly ICategoryService _categoryService;

    public AiChatService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<AiChatService> logger,
        IProductService productService,
        ICategoryService categoryService)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
        _productService = productService;
        _categoryService = categoryService;
    }

    public async Task<(bool ApiKeyConfigured, string Provider, string BaseUrl, string ModelId, string? TestError)> GetDiagnosticAsync(
        CancellationToken cancellationToken = default)
    {
        var apiKey = _configuration["MegaLLM:ApiKey"];
        var baseUrl = (_configuration["MegaLLM:BaseUrl"] ?? "https://ai.megallm.io/v1").Trim().TrimEnd('/');
        var modelId = _configuration["MegaLLM:ModelId"] ?? "openai-gpt-oss-20b";

        if (string.IsNullOrWhiteSpace(apiKey))
            return (false, "MegaLLM", baseUrl, modelId, "MegaLLM:ApiKey chưa cấu hình. Set env MegaLLM__ApiKey trên Azure.");

        try
        {
            var payload = new MegaChatCompletionRequest
            {
                Model = modelId,
                Messages = new List<MegaTextMessage> { new() { Role = "user", Content = "hello" } },
                MaxTokens = 5,
                Temperature = 0.1
            };

            using var req = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/chat/completions");
            req.Headers.Add("Authorization", $"Bearer {apiKey}");
            req.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(req, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var err = await response.Content.ReadAsStringAsync(cancellationToken);
                return (true, "MegaLLM", baseUrl, modelId, $"LLM trả {(int)response.StatusCode}: {err}");
            }

            return (true, "MegaLLM", baseUrl, modelId, null);
        }
        catch (Exception ex)
        {
            return (true, "MegaLLM", baseUrl, modelId, ex.Message);
        }
    }

    public async Task<AiChatResponseDto> SendChatAsync(AiChatRequestDto request, CancellationToken cancellationToken = default)
    {
        var apiKey = _configuration["MegaLLM:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("MegaLLM:ApiKey chưa cấu hình.");

        var baseUrl = (_configuration["MegaLLM:BaseUrl"] ?? "https://ai.megallm.io/v1").Trim().TrimEnd('/');
        var modelId = request.Model?.Trim() ?? _configuration["MegaLLM:ModelId"] ?? "openai-gpt-oss-20b";
        var maxTokens = request.MaxTokens ?? ParseInt(_configuration["MegaLLM:MaxTokens"], 1600);
        var temperature = request.Temperature ?? ParseDouble(_configuration["MegaLLM:Temperature"], 0.7);

        var lastUserMessage = GetLastUserMessage(request);
        if (string.IsNullOrWhiteSpace(lastUserMessage))
            throw new ArgumentException("Message hoặc Messages không được để trống.");

        var systemPrompt = string.IsNullOrWhiteSpace(request.SystemPrompt)
            ? """
              Bạn là trợ lý dinh dưỡng và mua sắm của NongXanh (chuyên rau củ, trái cây).
              QUY TẮC:
              - Luôn trả lời bằng tiếng Việt, ngắn gọn, dễ hiểu.
              - Ưu tiên tư vấn dựa trên dữ liệu sản phẩm/cate được cung cấp trong context; nếu context thiếu dữ liệu phù hợp thì hỏi lại 1-2 câu để làm rõ hoặc gợi ý lựa chọn phổ biến.
              - Khi có sản phẩm giảm giá, ưu tiên nêu riêng mục "Đang giảm giá".
              - Không bịa thông tin y khoa tuyệt đối, chỉ mang tính tư vấn dinh dưỡng phổ thông.
              - Không hứa hẹn “chữa bệnh”. Dùng các cụm như: “hỗ trợ”, “có thể giúp”, “thường được biết đến”.
              
              PHONG CÁCH (FOMO NHẸ, KHÔNG LỐ):
              - Khi gợi ý món/sản phẩm, kèm 1 câu lợi ích và một lời nhắc hành động nhẹ: “đang đúng mùa/đang giảm giá/hết nhanh”.
              - Nêu lợi ích dễ hiểu: đẹp da (hỗ trợ collagen/chống oxy hoá), tiêu hoá (chất xơ), năng lượng (carb tốt), miễn dịch (vitamin C).
              
              KHI NGƯỜI DÙNG NÓI THIẾU CHẤT:
              - Thiếu vitamin C: ưu tiên cam, ổi, bưởi, chanh, ớt chuông.
              - Thiếu vitamin A: ưu tiên cà rốt, bí đỏ, khoai lang.
              - Thiếu sắt: ưu tiên rau bina/cải bó xôi, bông cải xanh, đậu.
              - Thiếu kali: ưu tiên chuối, khoai tây, cà chua.
              - Thiếu chất xơ: ưu tiên bông cải xanh, cà rốt, rau xanh, táo, lê.
              
              GIẢI THÍCH NHANH (1-2 câu) KHI THIẾU CHẤT:
              - Vitamin C: thường hỗ trợ miễn dịch và tổng hợp collagen (da).
              - Vitamin A/beta-carotene: thường liên quan thị lực, da và niêm mạc.
              - Sắt: liên quan tạo hồng cầu, năng lượng; nên kết hợp nguồn vitamin C để hấp thu tốt hơn.
              - Chất xơ: hỗ trợ tiêu hoá, no lâu; tốt cho kiểm soát đường huyết.
              
              FORMAT GỢI Ý SẢN PHẨM:
              - Mỗi dòng: - Tên sản phẩm: giá đ
              - Nếu giảm giá: - Tên sản phẩm: giá đ (giảm từ giá_gốc đ)
              - Nếu có, kèm “| lợi ích: ...” (ngắn gọn 5-10 từ).
              """.Trim()
            : request.SystemPrompt!.Trim();

        var rag = await BuildRagContextAsync(lastUserMessage!, cancellationToken);
        // Nếu user hỏi về 1 rau/củ cụ thể nhưng DB không có sản phẩm liên quan thì báo rõ ràng.
        if (rag.MatchedCount == 0 && LooksLikeSpecificProduceQuestion(lastUserMessage!))
        {
            return new AiChatResponseDto { Content = "Không có thông tin về sản phẩm này." };
        }

        var ragContext = rag.Context;
        if (!string.IsNullOrWhiteSpace(ragContext))
            systemPrompt += "\n\n" + ragContext;

        var messages = new List<MegaTextMessage> { new() { Role = "system", Content = systemPrompt } };

        if (request.Messages is { Count: > 0 })
        {
            foreach (var m in request.Messages.Where(x => !string.Equals(x.Role, "system", StringComparison.OrdinalIgnoreCase)))
                messages.Add(new MegaTextMessage { Role = string.IsNullOrWhiteSpace(m.Role) ? "user" : m.Role, Content = m.Content ?? string.Empty });
        }
        else
        {
            messages.Add(new MegaTextMessage { Role = "user", Content = request.Message ?? string.Empty });
        }

        var payload = new MegaChatCompletionRequest
        {
            Model = modelId,
            Messages = messages,
            MaxTokens = maxTokens,
            Temperature = temperature
        };

        using var req = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/chat/completions");
        req.Headers.Add("Authorization", $"Bearer {apiKey}");
        req.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        var res = await _httpClient.SendAsync(req, cancellationToken);
        var body = await res.Content.ReadAsStringAsync(cancellationToken);
        if (!res.IsSuccessStatusCode)
        {
            _logger.LogError("MegaLLM error {Status}: {Body}", (int)res.StatusCode, body);
            throw new HttpRequestException($"MegaLLM trả {(int)res.StatusCode}: {body}");
        }

        var content = ExtractAssistantContent(body);
        return new AiChatResponseDto { Content = content };
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

    private static string? GetLastUserMessage(AiChatRequestDto request)
    {
        if (request.Messages is { Count: > 0 })
        {
            var last = request.Messages.LastOrDefault(m => string.Equals(m.Role, "user", StringComparison.OrdinalIgnoreCase));
            return last?.Content?.Trim();
        }

        return request.Message?.Trim();
    }

    private sealed record RagResult(string Context, int MatchedCount);

    private async Task<RagResult> BuildRagContextAsync(string userText, CancellationToken cancellationToken)
    {
        var text = userText.ToLowerInvariant();
        var sb = new StringBuilder();

        IEnumerable<ProductDto> products;
        IEnumerable<CategoryDto> categories;
        try
        {
            products = await _productService.GetAllAsync();
        }
        catch
        {
            products = Array.Empty<ProductDto>();
        }

        try
        {
            categories = await _categoryService.GetAllAsync();
        }
        catch
        {
            categories = Array.Empty<CategoryDto>();
        }

        var productList = products.Take(250).ToList();
        var categoryList = categories.Take(200).ToList();

        var keywords = ExtractKeywords(text).Take(6).ToList();
        var matched = new List<ProductDto>();

        if (keywords.Count > 0)
        {
            foreach (var p in productList)
            {
                if (matched.Count >= 18) break;
                var name = (p.ProductName ?? string.Empty).ToLowerInvariant();
                if (keywords.Any(k => name.Contains(k)))
                    matched.Add(p);
            }
        }

        if (matched.Count == 0 && productList.Count > 0)
        {
            matched = productList
                .OrderByDescending(p => (p.DiscountPrice.HasValue && p.BasePrice > 0 && p.DiscountPrice.Value > 0 && p.DiscountPrice.Value < p.BasePrice) ? 1 : 0)
                .ThenBy(p => p.ProductName)
                .Take(12)
                .ToList();
        }

        var discounted = productList
            .Where(p => p.DiscountPrice.HasValue && p.DiscountPrice.Value > 0 && p.DiscountPrice.Value < p.BasePrice)
            .OrderBy(p => p.DiscountPrice!.Value)
            .Take(10)
            .ToList();

        if (categoryList.Count > 0)
        {
            sb.AppendLine("DANH MỤC:");
            foreach (var c in categoryList.Take(25))
                sb.AppendLine($"- {c.CategoryName}");
            sb.AppendLine();
        }

        if (discounted.Count > 0)
        {
            sb.AppendLine("ĐANG GIẢM GIÁ:");
            foreach (var p in discounted)
            {
                var benefit = GetBenefitsHint((p.ProductName ?? string.Empty).ToLowerInvariant());
                var meta = BuildMeta(p);
                sb.AppendLine($"- {p.ProductName}: {FormatMoney(p.DiscountPrice!.Value)} đ (giảm từ {FormatMoney(p.BasePrice)} đ){meta}{(benefit != null ? $" | lợi ích: {benefit}" : string.Empty)}");
            }
            sb.AppendLine();
        }

        if (matched.Count > 0)
        {
            sb.AppendLine("SẢN PHẨM LIÊN QUAN:");
            foreach (var p in matched)
            {
                var price = p.DiscountPrice.HasValue && p.DiscountPrice.Value > 0 && p.DiscountPrice.Value < p.BasePrice
                    ? $"{FormatMoney(p.DiscountPrice.Value)} đ (giảm từ {FormatMoney(p.BasePrice)} đ)"
                    : $"{FormatMoney(p.BasePrice)} đ";
                var benefit = GetBenefitsHint((p.ProductName ?? string.Empty).ToLowerInvariant());
                var meta = BuildMeta(p);
                sb.AppendLine($"- {p.ProductName}: {price}{meta}{(benefit != null ? $" | lợi ích: {benefit}" : string.Empty)}");
            }
        }

        var ctx = sb.Length == 0 ? string.Empty : "CONTEXT (dữ liệu cửa hàng):\n" + sb.ToString().Trim();
        return new RagResult(ctx, matched.Count);
    }

    private static bool LooksLikeSpecificProduceQuestion(string userText)
    {
        var t = userText.Trim().ToLowerInvariant();
        if (t.Length < 3) return false;

        // Các mẫu hỏi về “một món cụ thể” (cà rốt có tác dụng gì, rau X là gì, ăn X có tốt không,...)
        var questionHints = new[]
        {
            "có tác dụng", "tác dụng", "là gì", "có tốt", "ăn", "uống", "dùng", "có đẹp da", "bổ sung", "vitamin", "dinh dưỡng", "công dụng"
        };

        // Từ khoá liên quan nhóm rau/củ/trái; nếu thiếu hoàn toàn thì không kết luận “sản phẩm này”
        var produceHints = new[]
        {
            "rau", "củ", "quả", "trái", "hoa quả", "trái cây",
            "cà", "bí", "bầu", "mướp", "đậu", "cải", "bông", "súp lơ", "khoai", "chuối", "táo", "lê", "ổi", "cam", "bưởi", "chanh", "cà chua", "ớt"
        };

        return questionHints.Any(h => t.Contains(h)) && produceHints.Any(h => t.Contains(h));
    }

    private static string BuildMeta(ProductDto p)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(p.Origin)) parts.Add($"xuất xứ {p.Origin!.Trim()}");
        if (!string.IsNullOrWhiteSpace(p.Unit)) parts.Add($"đơn vị {p.Unit!.Trim()}");
        if (p.IsOrganic) parts.Add("hữu cơ");
        if (p.Quantity > 0) parts.Add($"còn {p.Quantity}");
        return parts.Count == 0 ? string.Empty : $" ({string.Join(", ", parts)})";
    }

    private static string? GetBenefitsHint(string name)
    {
        // Chỉ là gợi ý dinh dưỡng phổ thông theo tên gọi; không khẳng định y khoa.
        if (name.Contains("ổi") || name.Contains("cam") || name.Contains("bưởi") || name.Contains("chanh"))
            return "giàu vitamin C, hỗ trợ collagen/đẹp da";
        if (name.Contains("cà rốt") || name.Contains("bí đỏ") || name.Contains("khoai lang"))
            return "beta-carotene, tốt cho da & mắt";
        if (name.Contains("rau bina") || name.Contains("cải bó xôi") || name.Contains("spinach"))
            return "sắt + folate, hỗ trợ năng lượng";
        if (name.Contains("bông cải") || name.Contains("súp lơ") || name.Contains("broccoli"))
            return "chất xơ, hỗ trợ tiêu hoá & no lâu";
        if (name.Contains("cà chua") || name.Contains("tomato"))
            return "chống oxy hoá (lycopene), tốt cho da";
        if (name.Contains("táo") || name.Contains("lê"))
            return "chất xơ, hỗ trợ tiêu hoá";
        if (name.Contains("chuối"))
            return "kali, hỗ trợ cơ bắp & phục hồi";
        if (name.Contains("khoai tây"))
            return "kali + carb tốt, năng lượng ổn định";
        if (name.Contains("ớt chuông") || name.Contains("ớt"))
            return "vitamin C, tăng vị ngon bữa ăn";
        return null;
    }

    private static IEnumerable<string> ExtractKeywords(string text)
    {
        var tokens = text
            .Split(new[] { ' ', '\t', '\r', '\n', ',', '.', ';', ':', '!', '?', '/', '\\', '-', '_', '(', ')', '[', ']', '{', '}', '"' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(t => t.Trim().ToLowerInvariant())
            .Where(t => t.Length >= 2);

        var stop = new HashSet<string>(new[]
        {
            "có","là","gì","nào","bao","nhiêu","cho","tôi","mình","bạn","của","và","hoặc","với","từ","đến","các","một","những","này","đó","không","được","hay","muốn","cần","tìm","mua","xem","giá","thế","ra","sao","ạ","ơi","nhé"
        });

        return tokens.Where(t => !stop.Contains(t)).Distinct();
    }

    private static int ParseInt(string? v, int fallback)
        => int.TryParse(v, NumberStyles.Any, CultureInfo.InvariantCulture, out var x) ? x : fallback;

    private static double ParseDouble(string? v, double fallback)
        => double.TryParse(v, NumberStyles.Any, CultureInfo.InvariantCulture, out var x) ? x : fallback;

    private static string FormatMoney(decimal v) => Math.Round(v, 0).ToString("0", CultureInfo.InvariantCulture);
}

