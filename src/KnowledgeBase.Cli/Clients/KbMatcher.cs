using KnowledgeBase.Cli.Models;

namespace KnowledgeBase.Cli.Clients;

// Quy tắc "khớp ở đâu" dùng chung cho các client tự tính MatchKind (mock, và client của KB không trả matchType).
internal static class KbMatcher
{
    // Tìm từ khoá (không phân biệt hoa thường) lần lượt trong title, tag rồi content.
    // Trả về null nếu không khớp ở đâu cả.
    public static MatchKind? FindMatch(KbDocument document, string query)
    {
        if (document.Title.Contains(query, StringComparison.OrdinalIgnoreCase))
        {
            return MatchKind.Title;
        }

        if (document.Tags.Any(tag => tag.Contains(query, StringComparison.OrdinalIgnoreCase)))
        {
            return MatchKind.Tag;
        }

        return document.Content.Contains(query, StringComparison.OrdinalIgnoreCase) ? MatchKind.Content : null;
    }
}
