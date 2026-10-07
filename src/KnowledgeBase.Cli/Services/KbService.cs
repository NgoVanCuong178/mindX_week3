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
    {
        if (string.IsNullOrWhiteSpace(query)) // null, "" và "  " (U01)
        {
            throw new ValidationException("Search query is required.");
        }

        if (topK < 1 || topK > MaxTopK) // 0 và 51, 1 và 50 (U02)
        {
            throw new ValidationException($"--top-k must be between 1 and {MaxTopK}.");
        }

        return _client.SearchAsync(new KbQuery(query.Trim(), topK));
    }

    public Task<IReadOnlyList<KbDocumentSummary>> ListAsync(string? nodePath, int limit)
    {
        ValidateNodePath(nodePath, "--node");

        if (limit < 1 || limit > MaxLimit)
        {
            throw new ValidationException($"--limit must be between 1 and {MaxLimit}.");
        }

        return _client.ListAsync(nodePath!, limit);
    }

    public Task<KbDocument> RetrieveAsync(string? docId)
    {
        if (string.IsNullOrWhiteSpace(docId))
        {
            throw new ValidationException("Document id is required.");
        }

        return _client.RetrieveAsync(docId.Trim());
    }

    // sourceName là tên file; được dùng làm title khi không có --title và nội dung không có dòng "# heading".
    public Task<KbDocument> AddAsync(string? content, string sourceName, string? nodePath,
                                     IEnumerable<string>? tags, string? title = null)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new ValidationException($"File '{sourceName}' is empty.");
        }

        ValidateNodePath(nodePath, "--path");

        var document = new NewKbDocument(ResolveTitle(title, content, sourceName), content, nodePath!,
                                         NormalizeTags(tags));
        return _client.AddAsync(document);
    }

    private static void ValidateNodePath(string? nodePath, string optionName)
    {
        if (string.IsNullOrWhiteSpace(nodePath) || !nodePath.StartsWith('/'))
        {
            throw new ValidationException($"{optionName} must be a node path starting with '/', for example /templates/email.");
        }
    }

    // Thứ tự ưu tiên: --title → dòng "# heading" đầu tiên trong nội dung → tên file (bỏ đuôi).
    private static string ResolveTitle(string? title, string content, string sourceName)
    {
        if (!string.IsNullOrWhiteSpace(title)) // có --title thì dùng luôn
        {
            return title.Trim();
        }

        var heading = content.Split('\n')
                             .Select(line => line.Trim())
                             .FirstOrDefault(line => line.StartsWith("# ")); // tìm dòng "# ..." đầu tiên
        if (heading is not null)
        {
            return heading[2..].Trim(); // bỏ 2 ký tự "# "
        }

        return Path.GetFileNameWithoutExtension(sourceName); // tên file bỏ ".md"
    }

    // Giống tags của Tuần 2: trim → chữ thường → bỏ rỗng → bỏ trùng.
    private static List<string> NormalizeTags(IEnumerable<string>? tags)
        => (tags ?? [])
            .Select(tag => tag.Trim().ToLowerInvariant())
            .Where(tag => tag.Length > 0)
            .Distinct()
            .ToList();
}
