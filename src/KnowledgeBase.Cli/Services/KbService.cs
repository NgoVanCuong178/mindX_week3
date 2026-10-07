using KnowledgeBase.Cli.Clients;
using KnowledgeBase.Cli.Models;

namespace KnowledgeBase.Cli.Services;

// Kiểm tra (validate) và chuẩn hoá input của người dùng ở MỘT chỗ duy nhất, để MockKbClient và
// HttpKbClient luôn nhận cùng một dữ liệu sạch, nhờ vậy hai client xử lý input sai giống hệt nhau.
public class KbService
{
    public const int MaxTopK = 50;
    public const int MaxLimit = 100;

    private readonly IKbClient _client;

    public KbService(IKbClient client)
    {
        _client = client;
    }

    public Task<IReadOnlyList<SearchResult>> SearchAsync(string? query, int topK)
        => throw new NotImplementedException();

    public Task<IReadOnlyList<KbDocumentSummary>> ListAsync(string? nodePath, int limit)
        => throw new NotImplementedException();

    public Task<KbDocument> RetrieveAsync(string? docId)
        => throw new NotImplementedException();

    // sourceName là tên file; được dùng làm title khi không có --title và nội dung không có dòng "# heading".
    public Task<KbDocument> AddAsync(string? content, string sourceName, string? nodePath,
                                     IEnumerable<string>? tags, string? title = null)
        => throw new NotImplementedException();
}
