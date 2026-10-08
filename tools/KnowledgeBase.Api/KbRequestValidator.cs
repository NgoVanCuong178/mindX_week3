using KnowledgeBase.Cli.Clients;
using KnowledgeBase.Cli.Models;
using KnowledgeBase.Cli.Services;

namespace KnowledgeBase.Api;

// Kiểm tra request gửi tới KB API. Mỗi hàm trả về thông báo lỗi đầu tiên tìm thấy, hoặc null nếu hợp lệ;
// server trả HTTP 400 {"error": "<thông báo>"}.
// CLI đã validate trước khi gửi (KbService), nhưng server không được tin client: request có thể đến từ
// curl, Postman hay chương trình khác. Giới hạn dùng chung với KbService để hai bên không lệch nhau.
//
// System.Text.Json không báo lỗi khi thiếu field: field thiếu nhận giá trị mặc định (null, 0),
// nên "thiếu field" và "field rỗng" được bắt chung ở đây.
public static class KbRequestValidator
{
    public static string? Validate(KbQuery query)
    {
        if (string.IsNullOrWhiteSpace(query.Query))
        {
            return "query is required.";
        }

        return query.TopK < 1 || query.TopK > KbService.MaxTopK
            ? $"topK must be between 1 and {KbService.MaxTopK}."
            : null;
    }

    public static string? Validate(ListRequest request)
    {
        return ValidateNodePath(request.NodePath)
               ?? (request.Limit < 1 || request.Limit > KbService.MaxLimit
                   ? $"limit must be between 1 and {KbService.MaxLimit}."
                   : null);
    }

    public static string? Validate(RetrieveRequest request)
        => string.IsNullOrWhiteSpace(request.DocId) ? "docId is required." : null;

    // tags không bắt buộc (null được coi như danh sách rỗng), nhưng nếu có thì không được chứa phần tử rỗng.
    public static string? Validate(NewKbDocument document)
    {
        if (string.IsNullOrWhiteSpace(document.Title))
        {
            return "title is required.";
        }

        if (string.IsNullOrWhiteSpace(document.Content))
        {
            return "content is required.";
        }

        return ValidateNodePath(document.NodePath)
               ?? (document.Tags is not null && document.Tags.Any(string.IsNullOrWhiteSpace)
                   ? "tags must not contain empty values."
                   : null);
    }

    private static string? ValidateNodePath(string? nodePath)
        => string.IsNullOrWhiteSpace(nodePath) || !nodePath.StartsWith('/')
            ? "nodePath must start with '/', for example /templates/email."
            : null;
}
