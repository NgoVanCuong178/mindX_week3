using KnowledgeBase.Cli.Clients;
using KnowledgeBase.Cli.Models;

namespace KnowledgeBase.IntegrationTests.Support;

// Nơi lưu dữ liệu luôn lỗi: đặt vào KnowledgeBase.Api để server trả HTTP 500 (giả lập KB API bị sự cố).
public class FailingKbClient : IKbClient
{
    public Task<IReadOnlyList<SearchResult>> SearchAsync(KbQuery query) => throw Failure();

    public Task<IReadOnlyList<KbDocumentSummary>> ListAsync(string nodePath, int limit) => throw Failure();

    public Task<KbDocument> RetrieveAsync(string docId) => throw Failure();

    public Task<KbDocument> AddAsync(NewKbDocument document) => throw Failure();

    private static InvalidOperationException Failure() => new("Simulated KB server failure");
}
