using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using BLL.DTOs;
using BLL.Services.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace BLL.Services;

public class ChatService : IChatService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ChatService> _logger;
    private readonly IProductService _productService;
    private readonly ICategoryService _categoryService;

    public ChatService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<ChatService> logger,
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
        var baseUrl = _configuration["MegaLLM:BaseUrl"]?.TrimEnd('/') ?? "https://ai.megallm.io/v1";
        var modelId = _configuration["MegaLLM:ModelId"] ?? "openai-gpt-oss-20b";

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return (false, "MegaLLM", baseUrl, modelId, "MegaLLM:ApiKey chưa cấu hình.");
        }

        try
        {
            var payload = new
            {
                model = modelId,
                messages = new[] { new { role = "user", content = "hello" } },
                max_tokens = 5,
                temperature = 0.1
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

    public async Task<ChatResponseDto> SendChatAsync(ChatRequestDto request, CancellationToken cancellationToken = default)
    {
        var apiKey = _configuration["MegaLLM:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("MegaLLM:ApiKey chưa cấu hình.");

        var baseUrl = _configuration["MegaLLM:BaseUrl"]?.TrimEnd('/') ?? "https://ai.megallm.io/v1";
        var modelId = request.Model?.Trim() ?? _configuration["MegaLLM:ModelId"] ?? "openai-gpt-oss-20b";
        var maxTokens = request.MaxTokens ?? ParseInt(_configuration["MegaLLM:MaxTokens"], 1600);
        var temperature = request.Temperature ?? ParseDouble(_configuration["MegaLLM:Temperature"], 0.7);

        var lastUserMessage = GetLastUserMessage(request);
        if (string.IsNullOrWhiteSpace(lastUserMessage))
            throw new ArgumentException("Message hoặc Messages không được để trống.");

        var ragContext = await BuildRagContextAsync(lastUserMessage!, cancellationToken);
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
            : request.SystemPrompt!;

        if (!string.IsNullOrWhiteSpace(ragContext))
            systemPrompt += "\n\n" + ragContext;

        var messages = new List<object> { new { role = "system", content = systemPrompt } };
        if (request.Messages is { Count: > 0 })
        {
            foreach (var m in request.Messages.Where(x => !string.Equals(x.Role, "system", StringComparison.OrdinalIgnoreCase)))
            {
                messages.Add(new { role = string.IsNullOrWhiteSpace(m.Role) ? "user" : m.Role, content = m.Content ?? string.Empty });
            }
        }
        else
        {
            messages.Add(new { role = "user", content = request.Message });
        }

        var payload = new
        {
            model = modelId,
            messages,
            max_tokens = maxTokens,
            temperature
        };

        using var req = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/chat/completions");
        req.Headers.Add("Authorization", $"Bearer {apiKey}");
        req.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        var res = await _httpClient.SendAsync(req, cancellationToken);
        if (!res.IsSuccessStatusCode)
        {
            var err = await res.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("Chat API error {Status}: {Body}", (int)res.StatusCode, err);
            throw new HttpRequestException($"LLM trả {(int)res.StatusCode}: {err}");
        }

        var llm = await res.Content.ReadFromJsonAsync<LlmResponse>(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("Không đọc được phản hồi LLM.");

        return new ChatResponseDto
        {
            Content = llm.Choices?.FirstOrDefault()?.Message?.Content ?? string.Empty,
            Usage = llm.Usage == null
                ? null
                : new ChatUsageDto
                {
                    PromptTokens = llm.Usage.PromptTokens,
                    CompletionTokens = llm.Usage.CompletionTokens,
                    TotalTokens = llm.Usage.TotalTokens
                }
        };
    }

    private async Task<string> BuildRagContextAsync(string userMessage, CancellationToken cancellationToken)
    {
        // Lightweight RAG: retrieve from live DB data (products/categories), then inject as context.
        var products = (await _productService.GetAllAsync()).ToList();
        var categories = (await _categoryService.GetAllAsync()).ToList();

        var tokens = userMessage
            .ToLowerInvariant()
            .Split(new[] { ' ', ',', '.', '?', '!', ':', ';', '-', '_' }, StringSplitOptions.RemoveEmptyEntries)
            .Distinct()
            .ToList();

        var matched = products
            .Select(p =>
            {
                var text = $"{p.ProductName} {p.Description} {p.CategoryName} {p.Origin}".ToLowerInvariant();
                var score = tokens.Count(t => text.Contains(t));
                return new { Product = p, Score = score };
            })
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .Take(8)
            .Select(x => x.Product)
            .ToList();

        if (matched.Count == 0)
            matched = products.Take(6).ToList();

        var sb = new StringBuilder();
        sb.AppendLine("=== NGUON DU LIEU NONGXANH (RAG) ===");
        sb.AppendLine("Danh muc: " + string.Join(", ", categories.Select(c => c.CategoryName).Where(n => !string.IsNullOrWhiteSpace(n)).Distinct()));
        sb.AppendLine("San pham lien quan:");
        foreach (var p in matched)
        {
            var price = p.DiscountPrice.HasValue && p.DiscountPrice.Value > 0 ? p.DiscountPrice.Value : p.BasePrice;
            sb.AppendLine($"- {p.ProductName}: {price:N0}d");
        }
        var discounted = matched
            .Where(p => p.DiscountPrice.HasValue && p.DiscountPrice.Value > 0 && p.DiscountPrice.Value < p.BasePrice)
            .ToList();
        if (discounted.Count > 0)
        {
            sb.AppendLine("Dang giam gia:");
            foreach (var p in discounted)
            {
                sb.AppendLine($"- {p.ProductName}: {p.DiscountPrice!.Value:N0}d (giam tu {p.BasePrice:N0}d)");
            }
        }
        sb.AppendLine("Chi su dung thong tin trong nguon du lieu nay de tu van, neu khong co thi noi ro khong tim thay.");
        return sb.ToString();
    }

    private static string? GetLastUserMessage(ChatRequestDto request)
    {
        if (request.Messages is { Count: > 0 })
        {
            return request.Messages.LastOrDefault(m => string.Equals(m.Role, "user", StringComparison.OrdinalIgnoreCase))?.Content?.Trim();
        }
        return request.Message?.Trim();
    }

    private static int ParseInt(string? value, int fallback)
    {
        return int.TryParse(value, out var parsed) ? parsed : fallback;
    }

    private static double ParseDouble(string? value, double fallback)
    {
        return double.TryParse(value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : fallback;
    }

    private sealed class LlmResponse
    {
        public List<LlmChoice>? Choices { get; set; }
        public LlmUsage? Usage { get; set; }
    }

    private sealed class LlmChoice
    {
        public LlmMessage? Message { get; set; }
    }

    private sealed class LlmMessage
    {
        public string? Content { get; set; }
    }

    private sealed class LlmUsage
    {
        public int PromptTokens { get; set; }
        public int CompletionTokens { get; set; }
        public int TotalTokens { get; set; }
    }
}
