using System.Text.Json;
using KnowledgeBase.Cli.Clients;
using KnowledgeBase.Cli.Models;

namespace KnowledgeBase.Api;

// Lưu kho tri thức vào file JSON để tài liệu vẫn còn sau khi khởi động lại server.
// Nếu file chưa tồn tại, tạo file mới với dữ liệu mẫu KbSeedData.Documents.
// Logic tìm kiếm / liệt kê / cấp id dùng lại MockKbClient, nên server có đúng hành vi của mock.
public class FileKbStore : IKbClient
{
    private static readonly JsonSerializerOptions FileJsonOptions = new(KbApiJson.Options) { WriteIndented = true };

    private readonly string _dataFile;
    private readonly List<KbDocument> _documents;
    private readonly MockKbClient _logic;

    // Server nhận nhiều request cùng lúc: khoá này bảo đảm mỗi lúc chỉ một request đọc/ghi dữ liệu.
    private readonly SemaphoreSlim _lock = new(1, 1);

    public FileKbStore(string dataFile)
    {
        _dataFile = Path.GetFullPath(dataFile);
        Directory.CreateDirectory(Path.GetDirectoryName(_dataFile)!); // tạo thư mục mới nếu chưa có

        if (File.Exists(_dataFile))
        {
            _documents = JsonSerializer.Deserialize<List<KbDocument>>(File.ReadAllText(_dataFile), FileJsonOptions)!; // đã có, đọc
        }
        else
        {
            _documents = KbSeedData.Documents.ToList(); // chưa có : lấy 8 tài liệu mẫu, ghi file 
            Save();
        }

        _logic = new MockKbClient(_documents); // dùng lại logic tìm kiếm của mock
    }

    public Task<IReadOnlyList<SearchResult>> SearchAsync(KbQuery query) => Locked(() => _logic.SearchAsync(query));

    public Task<IReadOnlyList<KbDocumentSummary>> ListAsync(string nodePath, int limit)
        => Locked(() => _logic.ListAsync(nodePath, limit));

    public Task<KbDocument> RetrieveAsync(string docId) => Locked(() => _logic.RetrieveAsync(docId));

    // Thêm tài liệu rồi ghi lại toàn bộ file.
    public Task<KbDocument> AddAsync(NewKbDocument document) => Locked(async () =>
    {
        var added = await _logic.AddAsync(document);
        _documents.Add(added);
        Save();
        return added;
    });

    private async Task<T> Locked<T>(Func<Task<T>> action)
    {
        await _lock.WaitAsync();
        try
        {
            return await action();
        }
        finally
        {
            _lock.Release();
        }
    }

    private void Save() => File.WriteAllText(_dataFile, JsonSerializer.Serialize(_documents, FileJsonOptions));
}
