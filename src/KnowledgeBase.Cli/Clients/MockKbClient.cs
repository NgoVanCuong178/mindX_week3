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

    public Task<IReadOnlyList<SearchResult>> SearchAsync(KbQuery query) => throw new NotImplementedException();

    public Task<IReadOnlyList<KbDocumentSummary>> ListAsync(string nodePath, int limit) => throw new NotImplementedException();

    public Task<KbDocument> RetrieveAsync(string docId) => throw new NotImplementedException();

    public Task<KbDocument> AddAsync(NewKbDocument document) => throw new NotImplementedException();
}
