using KnowledgeBase.Cli.Models;

namespace KnowledgeBase.Cli.Clients;

// Client gọi KB API qua HTTP: mỗi thao tác là một request POST dạng JSON (/search, /list, /retrieve, /add).
public class HttpKbClient : IKbClient
{
    private readonly HttpClient _httpClient;
    private readonly string? _apiToken; // token xác thực
    private readonly TextWriter? _log;

    // httpClient.BaseAddress phải trỏ tới địa chỉ gốc của KB API.
    // log: nơi ghi một dòng cho mỗi request (khi bật --verbose); null thì không ghi log.
    public HttpKbClient(HttpClient httpClient, string? apiToken = null, TextWriter? log = null)
    {
        _httpClient = httpClient;
        _apiToken = apiToken;
        _log = log;
    }

    public Uri? BaseAddress => _httpClient.BaseAddress;

    public Task<IReadOnlyList<SearchResult>> SearchAsync(KbQuery query) => throw new NotImplementedException();

    public Task<IReadOnlyList<KbDocumentSummary>> ListAsync(string nodePath, int limit) => throw new NotImplementedException();

    public Task<KbDocument> RetrieveAsync(string docId) => throw new NotImplementedException();

    public Task<KbDocument> AddAsync(NewKbDocument document) => throw new NotImplementedException();
}
