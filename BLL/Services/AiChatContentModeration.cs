using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace BLL.Services;

/// <summary>
/// Lọc nội dung nhạy cảm cho AI chatbot (thực phẩm / rau củ).
/// Đồng bộ logic với <c>PRN232-Mobile/lib/chatModeration.ts</c>.
/// </summary>
internal static class AiChatContentModeration
{
    public const string StandardMessage =
        "Xin lỗi, tin nhắn có từ ngữ không phù hợp. Vui lòng nhắn chính xác và lịch sự. " +
        "Trợ lý NongXanh chỉ hỗ trợ thông tin về thực phẩm, rau củ và gợi ý mua sắm tại cửa hàng.";

    /// <summary>
    /// Chuỗi con sau khi chuẩn hóa (bỏ dấu, thường). Ưu tiên cụm dài để tránh dương tính giả.
    /// </summary>
    private static readonly string[] BlockedNormalizedSubstrings =
    {
        // Chất thải / vệ sinh
        "cut", "dai", "dit", "dum", "cac", "loz", "sex", "xxx", "nude", "porn",
        "thoa than", "khieu dam", "quan he tinh duc", "khieu dam tre em",
        "chat thai", "rac thai", "nuoc tieu", "o nhiem",
        // Từ ngữ 18+ (cụm — bổ sung khi cần)
        "buoi bu", "dit me", "dit cha", "lon me",
        "clip nong", "anh nong", "video sex",
    };

    private static readonly string[] PhanSafeSubstrings =
    {
        "phan loai",
        "phan bon",
        "phan hoa",
        "phan mem",
        "phan quang",
        "phan huu co",
        "phan huu co hoc",
    };

    public static bool ContainsSensitiveContent(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;
        var n = NormalizeForMatch(text);
        if (n.Length == 0) return false;

        foreach (var sub in BlockedNormalizedSubstrings)
        {
            if (n.Contains(sub, StringComparison.Ordinal))
                return true;
        }

        // "phân" khi không phải ngữ cảnh phân loại / phân bón / phân hữu cơ...
        if (n.Contains("phan", StringComparison.Ordinal))
        {
            var safe = PhanSafeSubstrings.Any(p => n.Contains(p, StringComparison.Ordinal));
            if (!safe)
                return true;
        }

        return false;
    }

    private static string NormalizeForMatch(string text)
    {
        var s = text.Trim().ToLowerInvariant().Replace('đ', 'd').Replace('Đ', 'd');
        var normalized = s.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(normalized.Length);
        foreach (var ch in normalized)
        {
            var cat = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (cat != UnicodeCategory.NonSpacingMark)
                sb.Append(ch);
        }
        s = sb.ToString();
        // ư/ơ sau khi bỏ dấu có thể còn ký tự đặc biệt
        s = s.Replace('ư', 'u').Replace('Ư', 'u').Replace('ơ', 'o').Replace('Ơ', 'o');
        s = Regex.Replace(s, @"\s+", " ");
        return s.Trim();
    }
}
