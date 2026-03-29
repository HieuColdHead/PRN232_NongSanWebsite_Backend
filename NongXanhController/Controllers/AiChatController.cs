using BLL.DTOs;
using BLL.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace NongXanhController.Controllers;

/// <summary>
/// AI Chat Bot - tích hợp Mega LLM (OpenAI-compatible)
/// </summary>
[ApiController]
[Route("api/chat")]
[Produces("application/json")]
public sealed class AiChatController : BaseApiController
{
    private readonly IAiChatService _chatService;
    private readonly ILogger<AiChatController> _logger;

    public AiChatController(IAiChatService chatService, ILogger<AiChatController> logger)
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
    public async Task<ActionResult<ApiResponse<AiChatResponseDto>>> SendMessage([FromBody] AiChatRequestDto request, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _chatService.SendChatAsync(request, cancellationToken);
            return SuccessResponse(response, "AI response generated");
        }
        catch (ArgumentException ex)
        {
            return ErrorResponse<AiChatResponseDto>(ex.Message, statusCode: 400);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "AI chat service not configured");
            return ErrorResponse<AiChatResponseDto>("AI chưa được cấu hình", statusCode: 503);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Upstream LLM request failed");
            return ErrorResponse<AiChatResponseDto>("Không thể kết nối dịch vụ AI", new List<string> { ex.Message }, 502);
        }
        catch (TaskCanceledException)
        {
            return ErrorResponse<AiChatResponseDto>("Yêu cầu quá thời gian", statusCode: 504);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled AI chat error");
            return ErrorResponse<AiChatResponseDto>("Lỗi nội bộ hệ thống", statusCode: 500);
        }
    }
}

