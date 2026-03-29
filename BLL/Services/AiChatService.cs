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
              - Chỉ tư vấn dựa trên dữ liệu sản phẩm được cung cấp trong context.
              - Khi có sản phẩm giảm giá, ưu tiên nêu riêng mục "Đang giảm giá".
              - Không bịa thông tin y khoa tuyệt đối, chỉ mang tính tư vấn dinh dưỡng phổ thông.
              
              KHI NGƯỜI DÙNG NÓI THIẾU CHẤT:
              - Thiếu vitamin C: ưu tiên cam, ổi, bưởi, chanh, ớt chuông.
              - Thiếu vitamin A: ưu tiên cà rốt, bí đỏ, khoai lang.
              - Thiếu sắt: ưu tiên rau bina/cải bó xôi, bông cải xanh, đậu.
              - Thiếu kali: ưu tiên chuối, khoai tây, cà chua.
              - Thiếu chất xơ: ưu tiên bông cải xanh, cà rốt, rau xanh, táo, lê.
              
              FORMAT GỢI Ý SẢN PHẨM:
              - Mỗi dòng: - Tên sản phẩm: giá đ
              - Nếu giảm giá: - Tên sản phẩm: giá đ (giảm từ giá_gốc đ)
              """.Trim()
            : request.SystemPrompt!.Trim();

        var ragContext = await BuildRagContextAsync(lastUserMessage!, cancellationToken);
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

    private async Task<string> BuildRagContextAsync(string userText, CancellationToken cancellationToken)
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
                sb.AppendLine($"- {p.ProductName}: {FormatMoney(p.DiscountPrice!.Value)} đ (giảm từ {FormatMoney(p.BasePrice)} đ)");
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
                sb.AppendLine($"- {p.ProductName}: {price}");
            }
        }

        return sb.Length == 0 ? string.Empty : "CONTEXT (dữ liệu cửa hàng):\n" + sb.ToString().Trim();
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

