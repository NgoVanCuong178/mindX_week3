using KnowledgeBase.Cli.Models;

namespace KnowledgeBase.Cli.Clients;

// Kho tri thức giả lưu trong bộ nhớ, dùng khi phát triển và khi test.
// Tài liệu thêm vào sẽ mất khi chương trình kết thúc.
public class MockKbClient : IKbClient
{
    // Dữ liệu mặc định: một mẫu email, một tài liệu về nhóm và một hướng dẫn (tasks.md: "2-3 test documents").
    public static IReadOnlyList<KbDocument> DefaultDocuments { get; } =
    [
        new("doc-001", "Customer Response Template",
            "# Customer Response Template\n\nDear {customer},\n\nThank you for contacting MindX support. " +
            "We have received your request and will reply as soon as possible.\n\nBest regards,\nMindX Support Team\n",
            "/templates/email", ["template", "email"]),
        new("doc-002", "DevOps Team Members",
            "# DevOps Team Members\n\n- Alice - Team Lead\n- Bob - Site Reliability Engineer\n- Carol - Cloud Engineer\n\n" +
            "The on-call rotation changes every Monday.\n",
            "/team/devops", ["team", "devops"]),
        new("doc-003", "Getting Started Guide",
            "# Getting Started Guide\n\nHow to set up your development environment and handle your first support ticket.\n",
            "/docs/guides", ["guide", "onboarding"]),
    ];
    // danh sách tài liệu của riêng object KbDocument
    private readonly List<KbDocument> _documents;
    // Constructor không tham số
    public MockKbClient() : this(DefaultDocuments)
    {
    }
    // Contructor nhận dữ liệu tùy chọn
    public MockKbClient(IEnumerable<KbDocument> documents)
    {
        _documents = documents.ToList();
    }

    // Tìm từ khoá (không phân biệt hoa thường) trong title, tag rồi content của từng tài liệu.
    // Xếp theo nơi khớp (Title → Tag → Content), cùng nơi khớp thì theo id; lấy tối đa TopK kết quả.
    public Task<IReadOnlyList<SearchResult>> SearchAsync(KbQuery query)
    {
        IReadOnlyList<SearchResult> results = _documents
            .Select(document => (Document: document, Match: KbMatcher.FindMatch(document, query.Query))) // tính "khớp ở đâu"
            .Where(candidate => candidate.Match is not null) // bỏ tài liệu không khớp
            .OrderBy(candidate => candidate.Match) // Title (0) --> Tag(1) --> Content(2)
            .ThenBy(candidate => candidate.Document.Id, StringComparer.Ordinal) // cùng loại khớp thì theo id
            .Take(query.TopK) // lấy tối đa topK
            .Select(candidate => new SearchResult(candidate.Document.ToSummary(), candidate.Match!.Value))
            .ToList();
        return Task.FromResult(results);
    }

    // Tài liệu thuộc đúng node, sắp theo id, lấy tối đa limit tài liệu.
    public Task<IReadOnlyList<KbDocumentSummary>> ListAsync(string nodePath, int limit)
    {
        IReadOnlyList<KbDocumentSummary> documents = _documents
            .Where(document => document.NodePath == nodePath)
            .OrderBy(document => document.Id, StringComparer.Ordinal)
            .Take(limit)
            .Select(document => document.ToSummary())
            .ToList();
        return Task.FromResult(documents);
    }

    public Task<KbDocument> RetrieveAsync(string docId)
        => Task.FromResult(_documents.FirstOrDefault(document => document.Id == docId)
                           ?? throw new KbDocumentNotFoundException(docId));

    // Cấp id mới = số lớn nhất trong các id "doc-NNN" hiện có + 1 (không dùng số lượng tài liệu + 1,
    // vì có thể trùng id cũ khi dãy id có lỗ hổng — bài học U02 của Tuần 2).
    public Task<KbDocument> AddAsync(NewKbDocument document)
    {
        var nextNumber = _documents
            .Select(existing => int.Parse(existing.Id["doc-".Length..])) // "doc-005" --> 5
            .DefaultIfEmpty(0) 
            .Max() + 1; // max + 1

        var added = new KbDocument($"doc-{nextNumber:D3}", document.Title, document.Content,
                                   document.NodePath, document.Tags.ToList()); // D3 : đủ 3 chữ số --> "doc-006"
        _documents.Add(added);
        return Task.FromResult(added);
    }
}
