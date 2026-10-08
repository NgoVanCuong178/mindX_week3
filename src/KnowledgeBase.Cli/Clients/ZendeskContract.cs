using System.Text.Json;

namespace KnowledgeBase.Cli.Clients;

// Định dạng JSON của Zendesk Help Center API (chỉ các field client cần; field khác được bỏ qua).
//   GET api/v2/help_center/articles/search.json?query=&per_page=&locale=   → ZendeskSearchResponse
//   GET api/v2/help_center/{locale}/sections/{id}/articles.json?per_page=  → ZendeskArticlesResponse
//   GET api/v2/help_center/{locale}/articles/{id}.json                     → ZendeskArticleResponse
// Mọi field đều nullable: Zendesk có thể thiếu field, và client phải báo rõ field nào thiếu.
// id và section_id dùng long vì id của Zendesk (ví dụ 4408894162714) vượt quá giới hạn của int.
public record ZendeskArticle(long? Id, string? Title, string? Body, long? SectionId, IReadOnlyList<string>? LabelNames);

public record ZendeskSearchResponse(IReadOnlyList<ZendeskArticle?>? Results);

public record ZendeskArticlesResponse(IReadOnlyList<ZendeskArticle?>? Articles);

public record ZendeskArticleResponse(ZendeskArticle? Article);

public static class ZendeskJson
{
    // Zendesk dùng snake_case: "section_id", "label_names".
    public static JsonSerializerOptions Options { get; } = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };
}
