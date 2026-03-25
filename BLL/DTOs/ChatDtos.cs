namespace BLL.DTOs;

public class ChatMessageDto
{
    public string Role { get; set; } = "user";
    public string Content { get; set; } = string.Empty;
}

public class ChatRequestDto
{
    public string? Message { get; set; }
    public List<ChatMessageDto>? Messages { get; set; }
    public string? SystemPrompt { get; set; }
    public string? Model { get; set; }
    public double? Temperature { get; set; }
    public int? MaxTokens { get; set; }
}

public class ChatUsageDto
{
    public int PromptTokens { get; set; }
    public int CompletionTokens { get; set; }
    public int TotalTokens { get; set; }
}

public class ChatResponseDto
{
    public string Content { get; set; } = string.Empty;
    public ChatUsageDto? Usage { get; set; }
}
