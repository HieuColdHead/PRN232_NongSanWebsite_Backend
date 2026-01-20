using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using BLL.DTOs;

namespace NongXanhController.Controllers;

// DEV ONLY - DO NOT USE IN PRODUCTION
[ApiController]
[Route("api/[controller]")]
public class DevAuthController : ControllerBase
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;

    public DevAuthController(IHttpClientFactory httpClientFactory, IConfiguration configuration)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
    }

    // DEV ONLY - DO NOT USE IN PRODUCTION
    [HttpPost("firebase-token")]
    public async Task<IActionResult> GetFirebaseToken([FromBody] FirebaseDevLoginRequest request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new { message = "Email and password are required." });
        }

        var apiKey = _configuration["Firebase:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Firebase API key is not configured." });
        }

        var client = _httpClientFactory.CreateClient();
        var url = $"https://identitytoolkit.googleapis.com/v1/accounts:signInWithPassword?key={apiKey}";

        var payload = new
        {
            email = request.Email,
            password = request.Password,
            returnSecureToken = true
        };

        var response = await client.PostAsJsonAsync(url, payload);

        if (response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadFromJsonAsync<FirebaseRestResponse>();
            if (body == null)
            {
                return StatusCode(StatusCodes.Status502BadGateway, new { message = "Unexpected Firebase response." });
            }

            return Ok(new FirebaseDevLoginResponse
            {
                IdToken = body.IdToken ?? string.Empty,
                RefreshToken = body.RefreshToken ?? string.Empty,
                ExpiresIn = int.TryParse(body.ExpiresIn, out var exp) ? exp : 0
            });
        }

        // Attempt to read firebase error
        var error = await response.Content.ReadFromJsonAsync<FirebaseRestErrorResponse>();
        var message = error?.Error?.Message ?? "Firebase authentication failed.";
        return StatusCode(StatusCodes.Status401Unauthorized, new { message });
    }

    private class FirebaseRestResponse
    {
        [JsonPropertyName("idToken")]
        public string? IdToken { get; set; }

        [JsonPropertyName("refreshToken")]
        public string? RefreshToken { get; set; }

        [JsonPropertyName("expiresIn")]
        public string? ExpiresIn { get; set; }
    }

    private class FirebaseRestErrorResponse
    {
        [JsonPropertyName("error")]
        public FirebaseErrorDetail? Error { get; set; }
    }

    private class FirebaseErrorDetail
    {
        [JsonPropertyName("message")]
        public string? Message { get; set; }
    }
}
