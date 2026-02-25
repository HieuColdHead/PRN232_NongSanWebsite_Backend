namespace BLL.DTOs;

public class ApiResponse<T>
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public T? Data { get; set; }
    public string? Role { get; set; }
    public List<string>? Errors { get; set; }

    public ApiResponse() { }

    public ApiResponse(bool success, string message, T? data = default, List<string>? errors = null, string? role = null)
    {
        Success = success;
        Message = message;
        Data = data;
        Errors = errors;
        Role = role;
    }

    public static ApiResponse<T> Ok(T data, string message = "Success", string? role = null)
    {
        return new ApiResponse<T>(true, message, data, role: role);
    }

    public static ApiResponse<T> Fail(string message, List<string>? errors = null)
    {
        return new ApiResponse<T>(false, message, default, errors);
    }
}
