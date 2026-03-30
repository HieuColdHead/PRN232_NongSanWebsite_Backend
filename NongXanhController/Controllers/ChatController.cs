using BLL.DTOs;
using BLL.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace NongXanhController.Controllers;

[Route("api/support-chat")]
[ApiController]
[Authorize]
public class SupportChatController : ControllerBase
{
    private readonly IChatService _chatService;

    public SupportChatController(IChatService chatService)
    {
        _chatService = chatService;
    }

    [HttpPost("send")]
    public async Task<IActionResult> SendMessage([FromBody] SendMessageRequest request)
    {
        var userId = GetUserId();
        if (userId == Guid.Empty)
            return Unauthorized(new { message = "Không xác định được người gửi." });
        if (request == null || string.IsNullOrWhiteSpace(request.Message))
            return BadRequest(new { message = "Nội dung tin nhắn không được rỗng." });
        try
        {
            var result = await _chatService.SendMessageAsync(userId, request);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpGet("history/{otherUserId}")]
    public async Task<IActionResult> GetHistory(Guid otherUserId)
    {
        var userId = GetUserId();
        var history = await _chatService.GetChatHistoryAsync(userId, otherUserId);
        return Ok(history);
    }

    [HttpGet("history")]
    public async Task<IActionResult> GetMyHistory()
    {
        var userId = GetUserId();
        var history = await _chatService.GetMyChatHistoryAsync(userId);
        return Ok(history);
    }

    [HttpGet("admin/recent")]
    [Authorize(Roles = "Admin,Staff")]
    public async Task<IActionResult> GetRecentChats()
    {
        var recent = await _chatService.GetRecentChatsForAdminAsync();
        return Ok(recent);
    }

    [HttpPost("mark-read/{senderId}")]
    public async Task<IActionResult> MarkRead(Guid senderId)
    {
        var userId = GetUserId();
        await _chatService.MarkAsReadAsync(userId, senderId);
        return NoContent();
    }

    private Guid GetUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim == null)
        {
            userIdClaim = User.FindFirst("sub");
        }
        
        return userIdClaim != null && Guid.TryParse(userIdClaim.Value, out var guid) ? guid : Guid.Empty;
    }
}
