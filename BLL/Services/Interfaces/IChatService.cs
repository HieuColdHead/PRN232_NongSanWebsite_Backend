using BLL.DTOs;

namespace BLL.Services.Interfaces;

public interface IChatService
{
    Task<(bool ApiKeyConfigured, string Provider, string BaseUrl, string ModelId, string? TestError)> GetDiagnosticAsync(
        CancellationToken cancellationToken = default);

    Task<ChatResponseDto> SendChatAsync(ChatRequestDto request, CancellationToken cancellationToken = default);
}
