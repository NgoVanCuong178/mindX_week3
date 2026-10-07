using KnowledgeBase.Cli.Clients;
using KnowledgeBase.Cli.Models;

namespace KnowledgeBase.IntegrationTests.Integration;

// R01 — Test tích hợp với KB API chạy như một dịch vụ bên ngoài (AC4).
// Mặc định: KnowledgeBase.Api chạy ở tiến trình riêng, lưu dữ liệu vào file (xem KbApiTarget).
// Khi có KB API khác (đặt KB_REAL_API_URL), cùng bộ test này chạy với KB API đó, không cần sửa code.
// Không giả định dữ liệu có sẵn trên server: mỗi test tự thêm một tài liệu có tên duy nhất vào node
// /tests/smoke rồi kiểm tra trên chính tài liệu đó.
public class KbApiIntegrationTests(KbApiTarget target) : IClassFixture<KbApiTarget>
{
    private const string NodePath = "/tests/smoke";

    private IKbClient CreateClient() => KbClientFactory.Create(new Dictionary<string, string?>
    {
        [KbClientFactory.ClientVariable] = "http",
        [KbClientFactory.ApiUrlVariable] = target.Url,
        [KbClientFactory.ApiTokenVariable] = target.Token,
    });

    private static NewKbDocument UniqueDocument(out string token)
    {
        token = "smoke" + Guid.NewGuid().ToString("N")[..12];
        return new NewKbDocument($"Week 3 smoke test {token}", $"Created by KbApiIntegrationTests ({token}).",
                                 NodePath, ["smoke-test"]);
    }

    // R01a — add rồi retrieve: tài liệu đọc lại giống tài liệu vừa thêm. Loại: Normal.
    [Fact]
    public async Task R01a_AddThenRetrieve_RoundTrips()
    {
        var client = CreateClient();
        var document = UniqueDocument(out _);

        var added = await client.AddAsync(document);
        var retrieved = await client.RetrieveAsync(added.Id);

        Assert.Equal(document.Title, retrieved.Title);
        Assert.Equal(document.Content, retrieved.Content);
        Assert.Equal(NodePath, retrieved.NodePath);
    }

    // R01b — search tìm được tài liệu vừa thêm theo từ khoá duy nhất trong title. Loại: Normal.
    [Fact]
    public async Task R01b_Search_FindsAddedDocument()
    {
        var client = CreateClient();
        var added = await client.AddAsync(UniqueDocument(out var token));

        var results = await client.SearchAsync(new KbQuery(token, 5));

        Assert.Contains(results, r => r.Document.Id == added.Id);
    }

    // R01c — list của node có chứa tài liệu vừa thêm. Loại: Normal.
    [Fact]
    public async Task R01c_List_ContainsAddedDocument()
    {
        var client = CreateClient();
        var added = await client.AddAsync(UniqueDocument(out _));

        var documents = await client.ListAsync(NodePath, 100);

        Assert.Contains(documents, d => d.Id == added.Id);
    }

    // R01d — retrieve id không tồn tại → KbDocumentNotFoundException (giống mock). Loại: Abnormal.
    [Fact]
    public async Task R01d_Retrieve_UnknownId_ThrowsNotFound()
    {
        var unknownId = "doc-does-not-exist-" + Guid.NewGuid().ToString("N")[..8];

        await Assert.ThrowsAsync<KbDocumentNotFoundException>(() => CreateClient().RetrieveAsync(unknownId));
    }
}
