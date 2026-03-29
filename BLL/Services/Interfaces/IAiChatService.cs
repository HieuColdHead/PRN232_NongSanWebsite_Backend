using BLL.DTOs;

namespace BLL.Services.Interfaces;

public interface IAiChatService
{
    Task<(bool ApiKeyConfigured, string Provider, string BaseUrl, string ModelId, string? TestError)> GetDiagnosticAsync(
        CancellationToken cancellationToken = default);

    Task<AiChatResponseDto> SendChatAsync(AiChatRequestDto request, CancellationToken cancellationToken = default);
}

