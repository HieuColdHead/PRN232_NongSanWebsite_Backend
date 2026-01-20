using BLL.DTOs;
using BLL.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using FirebaseAdmin.Auth;

namespace NongXanhController.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(IAuthService authService, ILogger<AuthController> logger)
    {
        _authService = authService;
        _logger = logger;
    }

    [HttpPost("firebase-login")]
    public async Task<IActionResult> FirebaseLogin([FromBody] FirebaseLoginRequest request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.IdToken))
        {
            return BadRequest(new { message = "Firebase ID token is required." });
        }

        try
        {
            var response = await _authService.LoginWithFirebaseAsync(request.IdToken);
            return Ok(response);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Firebase login failed");
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Đã xảy ra lỗi không mong muốn." });
        }
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] FirebaseRegisterRequest request)
    {
        if (request == null)
        {
            return BadRequest(new { message = "Request body is required." });
        }

        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new { message = "Email và mật khẩu là bắt buộc." });
        }

        try
        {
            var response = await _authService.RegisterWithEmailPasswordAsync(request);
            return Ok(response);
        }
        catch (InvalidOperationException ex) when (ex.InnerException is FirebaseAuthException firebaseEx && firebaseEx.AuthErrorCode == AuthErrorCode.EmailAlreadyExists)
        {
            return Conflict(new { message = "Email đã tồn tại trên Firebase." });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            // Các lỗi từ Firebase (ví dụ password yếu, phone sai định dạng...)
            if (ex.Message.Contains("đã tồn tại", StringComparison.OrdinalIgnoreCase))
            {
                return Conflict(new { message = ex.Message });
            }

            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Register firebase user failed");
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Đã xảy ra lỗi không mong muốn." });
        }
    }
}
