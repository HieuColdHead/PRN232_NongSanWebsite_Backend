using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using BLL.DTOs;
using BLL.Services.Interfaces;
using DAL.Entity;
using DAL.Repositories.Interfaces;
using Google.Apis.Auth;
using Microsoft.Extensions.Configuration;

namespace BLL.Services;

public sealed class GoogleOAuthService : IGoogleOAuthService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly IUserRepository _userRepository;
    private readonly ITokenService _tokenService;

    public GoogleOAuthService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        IUserRepository userRepository,
        ITokenService tokenService)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _userRepository = userRepository;
        _tokenService = tokenService;
    }

    public GoogleOAuthStartResponse BuildAuthorizationUrl()
    {
        var clientId = GetRequired("GoogleOAuth:ClientId");
        var redirectUri = GetRequired("GoogleOAuth:RedirectUri");

        // In production store state server-side (cache/db). Keeping simple here.
        var state = Guid.NewGuid().ToString("N");

        var query = new Dictionary<string, string?>
        {
            ["client_id"] = clientId,
            ["redirect_uri"] = redirectUri,
            ["response_type"] = "code",
            ["scope"] = "openid email profile",
            ["state"] = state,
            // NOTE: not required for id_token; keep if you need refresh_token.
            ["access_type"] = "offline",
            ["prompt"] = "consent"
        };

        var url = "https://accounts.google.com/o/oauth2/v2/auth?" +
                  string.Join("&", query
                      .Where(kv => !string.IsNullOrWhiteSpace(kv.Value))
                      .Select(kv => $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value!)}"));

        return new GoogleOAuthStartResponse
        {
            AuthorizationUrl = url,
            State = state
        };
    }

    public async Task<AuthResponse> ExchangeCodeAndLoginAsync(string code, string state, string? redirectUri = null)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentException("Code is required.", nameof(code));
        }

        // TODO: validate state (match with previously issued state)
        _ = state;

        var token = await ExchangeCodeForTokensAsync(code, redirectUri);
        if (string.IsNullOrWhiteSpace(token.IdToken))
        {
            var details = $"access_token_present={(!string.IsNullOrWhiteSpace(token.AccessToken))}, token_type={token.TokenType}, scope={token.Scope}";
            throw new InvalidOperationException($"Google token response did not contain id_token. {details}");
        }

        var clientId = GetRequired("GoogleOAuth:ClientId");
        var payload = await GoogleJsonWebSignature.ValidateAsync(token.IdToken, new GoogleJsonWebSignature.ValidationSettings
        {
            Audience = new[] { clientId }
        });

        if (string.IsNullOrWhiteSpace(payload.Email))
        {
            throw new InvalidOperationException("Google account does not provide an email.");
        }

        var email = payload.Email.Trim().ToLowerInvariant();
        var existing = await _userRepository.GetByEmailAsync(email);

        if (existing is null)
        {
            var user = new User
            {
                Email = email,
                DisplayName = payload.Name,
                Provider = "Google",
                Role = UserRole.User,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                LastLoginAt = DateTime.UtcNow
            };

            await _userRepository.CreateAsync(user);

            return new AuthResponse
            {
                AccessToken = _tokenService.CreateAccessToken(user),
                User = new UserDto
                {
                    Id = user.Id,
                    Email = user.Email,
                    PhoneNumber = user.PhoneNumber,
                    DisplayName = user.DisplayName,
                    Provider = user.Provider,
                    CreatedAt = user.CreatedAt,
                    IsActive = user.IsActive,
                    LastLoginAt = user.LastLoginAt
                }
            };
        }

        existing.LastLoginAt = DateTime.UtcNow;
        if (string.IsNullOrWhiteSpace(existing.DisplayName))
        {
            existing.DisplayName = payload.Name;
        }

        await _userRepository.UpdateAsync(existing);
        await _userRepository.SaveChangesAsync();

        return new AuthResponse
        {
            AccessToken = _tokenService.CreateAccessToken(existing),
            User = new UserDto
            {
                Id = existing.Id,
                Email = existing.Email,
                PhoneNumber = existing.PhoneNumber,
                DisplayName = existing.DisplayName,
                Provider = existing.Provider,
                CreatedAt = existing.CreatedAt,
                IsActive = existing.IsActive,
                LastLoginAt = existing.LastLoginAt
            }
        };
    }

    private async Task<GoogleTokenResponse> ExchangeCodeForTokensAsync(string code, string? customRedirectUri = null)
    {
        var clientId = GetRequired("GoogleOAuth:ClientId");
        var clientSecret = GetRequired("GoogleOAuth:ClientSecret");
        // Use custom redirect URI from mobile app if provided, otherwise use server config
        var redirectUri = !string.IsNullOrWhiteSpace(customRedirectUri) 
            ? customRedirectUri 
            : GetRequired("GoogleOAuth:RedirectUri");

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://oauth2.googleapis.com/token");
        request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["code"] = code,
            ["client_id"] = clientId,
            ["client_secret"] = clientSecret,
            ["redirect_uri"] = redirectUri,
            ["grant_type"] = "authorization_code"
        });

        var client = _httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var response = await client.SendAsync(request);
        var json = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Google token exchange failed: {(int)response.StatusCode} {json}");
        }

        var token = JsonSerializer.Deserialize<GoogleTokenResponse>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        return token ?? throw new InvalidOperationException("Failed to deserialize Google token response.");
    }

    private string GetRequired(string key) => _configuration[key] ?? throw new InvalidOperationException($"Missing configuration: {key}");

    private sealed class GoogleTokenResponse
    {
        [JsonPropertyName("access_token")]
        public string? AccessToken { get; set; }

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }

        [JsonPropertyName("refresh_token")]
        public string? RefreshToken { get; set; }

        [JsonPropertyName("scope")]
        public string? Scope { get; set; }

        [JsonPropertyName("token_type")]
        public string? TokenType { get; set; }

        [JsonPropertyName("id_token")]
        public string? IdToken { get; set; }
    }
}
