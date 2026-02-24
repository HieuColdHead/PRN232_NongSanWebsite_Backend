using BLL.DTOs;
using BLL.Services.Interfaces;
using DAL.Data;
using DAL.Entity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace NongXanhController.Controllers;

[ApiController]
[Route("api/auth")]
[AllowAnonymous]
public class AuthController : ControllerBase
{
    private readonly IGoogleOAuthService _googleOAuthService;
    private readonly IEmailOtpService _emailOtpService;
    private readonly ILocalAuthService _localAuthService;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ApplicationDbContext _dbContext;

    public AuthController(
        IGoogleOAuthService googleOAuthService,
        IEmailOtpService emailOtpService,
        ILocalAuthService localAuthService,
        IPasswordHasher passwordHasher,
        ApplicationDbContext dbContext)
    {
        _googleOAuthService = googleOAuthService;
        _emailOtpService = emailOtpService;
        _localAuthService = localAuthService;
        _passwordHasher = passwordHasher;
        _dbContext = dbContext;
    }

    [HttpGet("google/start")]
    public ActionResult<GoogleOAuthStartResponse> GoogleStart()
    {
        var result = _googleOAuthService.BuildAuthorizationUrl();
        return Ok(result);
    }

    [HttpPost("google/callback")]
    public async Task<ActionResult<AuthResponse>> GoogleCallback([FromBody] GoogleOAuthCallbackRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var result = await _googleOAuthService.ExchangeCodeAndLoginAsync(request.Code, request.State, request.RedirectUri);
        return Ok(result);
    }

    /// <summary>
    /// GET callback endpoint for Google OAuth redirect.
    /// After processing, redirects to mobile app scheme with token.
    /// </summary>
    [HttpGet("google/callback")]
    public async Task<IActionResult> GoogleCallbackRedirect([FromQuery] string code, [FromQuery] string state, [FromQuery] string? error = null)
    {
        const string appScheme = "nongxanh://";
        
        if (!string.IsNullOrEmpty(error))
        {
            return Redirect($"{appScheme}auth/error?error={Uri.EscapeDataString(error)}");
        }

        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(state))
        {
            return Redirect($"{appScheme}auth/error?error=missing_params");
        }

        try
        {
            var result = await _googleOAuthService.ExchangeCodeAndLoginAsync(code, state);
            // Redirect to mobile app with token
            var token = Uri.EscapeDataString(result.AccessToken);
            var userId = Uri.EscapeDataString(result.User.Id.ToString());
            var email = Uri.EscapeDataString(result.User.Email);
            var displayName = Uri.EscapeDataString(result.User.DisplayName ?? "");
            
            return Redirect($"{appScheme}auth/success?token={token}&userId={userId}&email={email}&displayName={displayName}");
        }
        catch (Exception ex)
        {
            return Redirect($"{appScheme}auth/error?error={Uri.EscapeDataString(ex.Message)}");
        }
    }

    [HttpPost("email/request-otp")]
    public async Task<IActionResult> RequestEmailOtp([FromBody] EmailOtpRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        await _emailOtpService.RequestOtpAsync(request.Email);
        return NoContent();
    }

    [HttpPost("email/verify-otp")]
    public async Task<ActionResult<AuthResponse>> VerifyEmailOtp([FromBody] EmailOtpVerifyRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var result = await _emailOtpService.VerifyOtpAndRegisterAsync(request);
        return Ok(result);
    }

    // Register (email/password) is a 2-step flow:
    // 1) Stage registration data + send OTP
    // 2) Verify OTP to actually create the User
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterStartRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var email = request.Email.Trim().ToLowerInvariant();

        // If already registered -> reject.
        var existsUser = await _dbContext.Users.AnyAsync(u => u.Email == email);
        if (existsUser)
        {
            return Conflict(new { Message = "Email already exists." });
        }

        // Upsert pending registration (expires in 15 minutes)
        var pending = await _dbContext.PendingRegistrations.FirstOrDefaultAsync(x => x.Email == email);
        if (pending is null)
        {
            pending = new PendingRegistration { Email = email };
            _dbContext.PendingRegistrations.Add(pending);
        }

        pending.DisplayName = request.DisplayName;
        pending.PhoneNumber = request.PhoneNumber;
        pending.PasswordHash = _passwordHasher.Hash(request.Password);
        pending.ExpiresAt = DateTime.UtcNow.AddMinutes(15);

        await _dbContext.SaveChangesAsync();

        await _emailOtpService.RequestOtpAsync(email);

        return Accepted(new
        {
            Message = "OTP sent to email. Please verify OTP to complete registration.",
            Email = email
        });
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var result = await _localAuthService.LoginAsync(request);
        return Ok(result);
    }
}
