using System.Text.Json.Serialization;

namespace BLL.DTOs;

public sealed class AiChatMessageDto
{
    public string Role { get; set; } = "user"; // system|user|assistant
    public string Content { get; set; } = string.Empty;
}

public sealed class AiChatRequestDto
{
    public string? Message { get; set; }
    public List<AiChatMessageDto>? Messages { get; set; }

    public string? SystemPrompt { get; set; }
    public string? Model { get; set; }
    public int? MaxTokens { get; set; }
    public double? Temperature { get; set; }
}

public sealed class AiChatUsageDto
{
    public int PromptTokens { get; set; }
    public int CompletionTokens { get; set; }
    public int TotalTokens { get; set; }
}

public sealed class AiChatResponseDto
{
    public string Content { get; set; } = string.Empty;
    public AiChatUsageDto? Usage { get; set; }
}

internal sealed class MegaChatCompletionRequest
{
    [JsonPropertyName("model")]
    public string Model { get; set; } = string.Empty;

    [JsonPropertyName("messages")]
    public List<MegaTextMessage> Messages { get; set; } = new();

    [JsonPropertyName("max_tokens")]
    public int MaxTokens { get; set; }

    [JsonPropertyName("temperature")]
    public double Temperature { get; set; }
}

internal sealed class MegaTextMessage
{
    [JsonPropertyName("role")]
    public string Role { get; set; } = "user";

    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;
}

