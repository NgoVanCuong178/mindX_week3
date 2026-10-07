using System.Text.Json;
using System.Text.Json.Serialization;
using KnowledgeBase.Cli.Models;

namespace KnowledgeBase.Cli.Clients;

// Định dạng JSON của KB API (architecture.md), dùng chung cho HttpKbClient (gửi request, đọc response)
// và KnowledgeBase.Api (đọc request, trả response), để hai phía không thể hiểu khác nhau.
//
//   POST /search    body: KbQuery         {"query","topK"}                    → SearchResponse
//   POST /list      body: ListRequest     {"nodePath","limit"}                → ListResponse
//   POST /retrieve  body: RetrieveRequest {"docId"}                           → KbDocument (404 nếu không có)
//   POST /add       body: NewKbDocument   {"title","content","nodePath","tags"} → KbDocument
public record ListRequest(string NodePath, int Limit);

public record RetrieveRequest(string DocId);

public record SearchResponse(IReadOnlyList<SearchResultItem> Results);

// Một dòng kết quả tìm kiếm dạng phẳng, đúng như contract: {"id","title","nodePath","matchType"}.
public record SearchResultItem(string Id, string Title, string NodePath, MatchKind MatchType);

public record ListResponse(IReadOnlyList<KbDocumentSummary> Documents);

public static class KbApiJson
{
    // Tên field dạng camelCase ("nodePath", "topK"), đọc không phân biệt hoa thường,
    // enum ghi thành chữ ("title", "tag", "content") thay vì số.
    public static JsonSerializerOptions Options { get; } = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };
}
