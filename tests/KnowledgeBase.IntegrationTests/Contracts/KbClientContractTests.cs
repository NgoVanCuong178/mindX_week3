using KnowledgeBase.Cli.Clients;
using KnowledgeBase.Cli.Models;

namespace KnowledgeBase.IntegrationTests.Contracts;

// Contract test: MỘT bộ test mô tả hành vi bắt buộc của mọi IKbClient.
// Mỗi lớp con chạy lại toàn bộ bộ test này với một client khác (mock, HTTP qua KnowledgeBase.Api).
// Nếu HttpKbClient và MockKbClient khác nhau ở bất kỳ điểm nào, bộ test này phát hiện ngay
// (tasks.md: "HTTPKBClient follows the same behavior as MockKBClient").
public abstract class KbClientContractTests : IAsyncLifetime
{
    // Dữ liệu dùng chung cho mọi lớp con. Được chọn để mỗi tài liệu khớp "response" theo một cách khác nhau:
    //   doc-001 khớp ở title, doc-002 khớp ở tag, doc-003 khớp ở content, doc-004 không khớp.
    //   /templates/email có 2 tài liệu để test --limit.
    protected static readonly IReadOnlyList<KbDocument> Seed =
    [
        new("doc-001", "Email Response Template", "Use this to reply to a customer.", "/templates/email", ["template", "email"]),
        new("doc-002", "Escalation Guide", "How to escalate an incident.", "/docs/guides", ["response", "escalation"]),
        new("doc-003", "Weekly Report", "Summarize the response times of the week.", "/templates/email", ["report"]),
        new("doc-004", "DevOps Members", "Alice and Bob.", "/team/devops", ["team"]),
    ];

    protected IKbClient Client { get; private set; } = null!;

    // Lớp con tạo client cần test, với dữ liệu ban đầu là Seed.
    protected abstract Task<IKbClient> CreateClientAsync(IReadOnlyList<KbDocument> seed);

    protected virtual Task CleanUpAsync() => Task.CompletedTask;

    public async Task InitializeAsync() => Client = await CreateClientAsync(Seed);

    public Task DisposeAsync() => CleanUpAsync();

    // K01 — search tìm ở title, tag và content; không phân biệt hoa thường; xếp title → tag → content.
    // Loại: Normal. [EP] mỗi tài liệu đại diện một miền "khớp ở đâu". [EG] "RESPONSE" viết hoa.
    [Fact]
    public async Task K01_Search_MatchesTitleTagContentInRankedOrder()
    {
        var results = await Client.SearchAsync(new KbQuery("RESPONSE", 10));

        Assert.Equal(["doc-001", "doc-002", "doc-003"], results.Select(r => r.Document.Id));
        Assert.Equal([MatchKind.Title, MatchKind.Tag, MatchKind.Content], results.Select(r => r.MatchType));
        Assert.Equal(new KbDocumentSummary("doc-001", "Email Response Template", "/templates/email"),
                     results[0].Document);
    }

    // K02 — search: topK cắt đúng số kết quả; không có tài liệu nào khớp → danh sách rỗng.
    // Loại: Boundary. [BVA] topK nhỏ hơn số kết quả (3 khớp, lấy 2); 0 kết quả.
    [Theory]
    [InlineData("response", 2, new[] { "doc-001", "doc-002" })]
    [InlineData("zzz-no-match", 5, new string[0])]
    public async Task K02_Search_RespectsTopKAndEmptyResult(string query, int topK, string[] expectedIds)
    {
        var results = await Client.SearchAsync(new KbQuery(query, topK));

        Assert.Equal(expectedIds, results.Select(r => r.Document.Id));
    }

    // K03 — list chỉ trả tài liệu trong đúng node, sắp theo id; limit cắt đúng; node không tồn tại → rỗng.
    // Loại: Normal (dòng 1) — Boundary (dòng 2: limit = 1; dòng 3: 0 kết quả). [EP] [BVA]
    [Theory]
    [InlineData("/templates/email", 10, new[] { "doc-001", "doc-003" })]
    [InlineData("/templates/email", 1, new[] { "doc-001" })]
    [InlineData("/unknown/node", 10, new string[0])]
    public async Task K03_List_ReturnsDocumentsOfNode(string nodePath, int limit, string[] expectedIds)
    {
        var documents = await Client.ListAsync(nodePath, limit);

        Assert.Equal(expectedIds, documents.Select(d => d.Id));
        Assert.All(documents, d => Assert.Equal(nodePath, d.NodePath));
    }

    // K04 — retrieve trả về đủ mọi field của tài liệu.
    // Loại: Normal. [EP] miền "id tồn tại".
    [Fact]
    public async Task K04_Retrieve_ReturnsFullDocument()
    {
        var document = await Client.RetrieveAsync("doc-002");

        Assert.Equivalent(Seed[1], document, strict: true);
    }

    // K05 — retrieve id không tồn tại → KbDocumentNotFoundException mang đúng id.
    // Loại: Abnormal. [EP] miền "id không tồn tại".
    [Fact]
    public async Task K05_Retrieve_UnknownId_ThrowsNotFound()
    {
        var exception = await Assert.ThrowsAsync<KbDocumentNotFoundException>(() => Client.RetrieveAsync("doc-999"));

        Assert.Equal("doc-999", exception.DocId);
    }

    // K06 — add cấp id mới (số lớn nhất + 1, dạng doc-NNN); sau đó retrieve và list đều thấy tài liệu vừa thêm.
    // Loại: Normal — kèm Boundary (id = max + 1). [EG] cấp id bằng count + 1 có thể trùng id cũ.
    [Fact]
    public async Task K06_Add_AssignsNewIdAndDocumentBecomesVisible()
    {
        var added = await Client.AddAsync(new NewKbDocument("SMS Reminder", "Hi {name}!", "/templates/sms", ["sms"]));

        Assert.Equal("doc-005", added.Id);
        Assert.Equivalent(new KbDocument("doc-005", "SMS Reminder", "Hi {name}!", "/templates/sms", ["sms"]),
                          await Client.RetrieveAsync("doc-005"), strict: true);
        Assert.Equal(["doc-005"], (await Client.ListAsync("/templates/sms", 10)).Select(d => d.Id));
    }
}
