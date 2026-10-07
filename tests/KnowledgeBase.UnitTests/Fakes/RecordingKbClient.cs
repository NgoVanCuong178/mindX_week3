using KnowledgeBase.Cli.Clients;
using KnowledgeBase.Cli.Models;

namespace KnowledgeBase.UnitTests.Fakes;

// Bản giả của IKbClient dùng cho unit test KbService: ghi lại tham số của lần gọi gần nhất
// và trả về dữ liệu cố định. Nhờ vậy test kiểm tra được KbService đã chuẩn hoá input chưa,
// và KHÔNG gọi client khi input sai.
public class RecordingKbClient : IKbClient
{
    public int CallCount { get; private set; }
    public KbQuery? LastQuery { get; private set; }
    public (string NodePath, int Limit)? LastList { get; private set; }
    public string? LastDocId { get; private set; }
    public NewKbDocument? LastAdded { get; private set; }

    public Task<IReadOnlyList<SearchResult>> SearchAsync(KbQuery query)
    {
        CallCount++;
        LastQuery = query;
        return Task.FromResult<IReadOnlyList<SearchResult>>([]);
    }

    public Task<IReadOnlyList<KbDocumentSummary>> ListAsync(string nodePath, int limit)
    {
        CallCount++;
        LastList = (nodePath, limit);
        return Task.FromResult<IReadOnlyList<KbDocumentSummary>>([]);
    }

    public Task<KbDocument> RetrieveAsync(string docId)
    {
        CallCount++;
        LastDocId = docId;
        return Task.FromResult(new KbDocument(docId, "Title", "Content", "/node", []));
    }

    public Task<KbDocument> AddAsync(NewKbDocument document)
    {
        CallCount++;
        LastAdded = document;
        return Task.FromResult(new KbDocument("doc-100", document.Title, document.Content,
                                              document.NodePath, document.Tags));
    }
}
