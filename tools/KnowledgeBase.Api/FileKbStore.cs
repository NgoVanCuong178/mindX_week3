using KnowledgeBase.Cli.Clients;
using KnowledgeBase.Cli.Models;

namespace KnowledgeBase.Api;

// Lưu kho tri thức vào file JSON để tài liệu vẫn còn sau khi khởi động lại server.
// Nếu file chưa tồn tại, tạo file mới với dữ liệu mẫu KbSeedData.Documents.
public class FileKbStore : IKbClient
{
    private readonly string _dataFile;

    public FileKbStore(string dataFile)
    {
        _dataFile = dataFile;
    }

    public Task<IReadOnlyList<SearchResult>> SearchAsync(KbQuery query) => throw new NotImplementedException();

    public Task<IReadOnlyList<KbDocumentSummary>> ListAsync(string nodePath, int limit) => throw new NotImplementedException();

    public Task<KbDocument> RetrieveAsync(string docId) => throw new NotImplementedException();

    public Task<KbDocument> AddAsync(NewKbDocument document) => throw new NotImplementedException();
}
