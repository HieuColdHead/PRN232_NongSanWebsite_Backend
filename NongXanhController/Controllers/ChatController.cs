using BLL.DTOs;
using BLL.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace NongXanhController.Controllers;

[ApiController]
[Route("api/chat")]
[Produces("application/json")]
public class ChatController : BaseApiController
{
    private readonly IChatService _chatService;
    private readonly ILogger<ChatController> _logger;

    public ChatController(IChatService chatService, ILogger<ChatController> logger)
    {
        _chatService = chatService;
        _logger = logger;
    }

    [HttpGet("diagnostic")]
    [AllowAnonymous]
    public async Task<IActionResult> Diagnostic(CancellationToken cancellationToken = default)
    {
        var (apiKeyConfigured, provider, baseUrl, modelId, testError) = await _chatService.GetDiagnosticAsync(cancellationToken);
        return Ok(new
        {
            apiKeyConfigured,
            provider,
            baseUrl,
            modelId,
            status = testError == null ? "OK" : "Error",
            error = testError,
            timestamp = DateTime.UtcNow
        });
    }

    [HttpPost]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<ChatResponseDto>>> SendMessage([FromBody] ChatRequestDto request, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _chatService.SendChatAsync(request, cancellationToken);
            return SuccessResponse(response, "AI response generated");
        }
        catch (ArgumentException ex)
        {
            return ErrorResponse<ChatResponseDto>(ex.Message, statusCode: 400);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Chat service is not configured");
            return ErrorResponse<ChatResponseDto>("AI chưa được cấu hình", statusCode: 503);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Upstream LLM request failed");
            return ErrorResponse<ChatResponseDto>("Không thể kết nối dịch vụ AI", new List<string> { ex.Message }, 502);
        }
        catch (TaskCanceledException)
        {
            return ErrorResponse<ChatResponseDto>("Yêu cầu quá thời gian", statusCode: 504);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled chat error");
            return ErrorResponse<ChatResponseDto>("Lỗi nội bộ hệ thống", statusCode: 500);
        }
    }
}
