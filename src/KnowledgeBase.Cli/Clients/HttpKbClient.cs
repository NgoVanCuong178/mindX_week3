using KnowledgeBase.Cli.Models;

namespace KnowledgeBase.Cli.Clients;

// Client gọi KB API qua HTTP: mỗi thao tác là một request POST dạng JSON (/search, /list, /retrieve, /add).
// Phần gửi request, log, timeout và đổi lỗi nằm ở KbHttpSender; ở đây chỉ còn phần riêng của contract.
public class HttpKbClient : IKbClient
{
    private readonly KbHttpSender _sender;

    // httpClient.BaseAddress phải trỏ tới địa chỉ gốc của KB API.
    // log: nơi ghi một dòng cho mỗi request (khi bật --verbose); null thì không ghi log.
    public HttpKbClient(HttpClient httpClient, string? apiToken = null, TextWriter? log = null)
    {
        _sender = new KbHttpSender(httpClient, apiToken, log, KbApiJson.Options);
    }

    public Uri? BaseAddress => _sender.BaseAddress;

    public async Task<IReadOnlyList<SearchResult>> SearchAsync(KbQuery query)
    {
        // gọi Post/ search, đổi định dạng phẳng của JSON, về record của C#
        var response = await PostAsync<SearchResponse>("search", query, MissingField);
        return response.Results
            .Select(item => new SearchResult(new KbDocumentSummary(item.Id, item.Title, item.NodePath), item.MatchType))
            .ToList();
    }

    public async Task<IReadOnlyList<KbDocumentSummary>> ListAsync(string nodePath, int limit)
        => (await PostAsync<ListResponse>("list", new ListRequest(nodePath, limit), MissingField)).Documents;

    public Task<KbDocument> RetrieveAsync(string docId)
        => PostAsync<KbDocument>("retrieve", new RetrieveRequest(docId), MissingField, notFoundDocId: docId);

    public Task<KbDocument> AddAsync(NewKbDocument document)
        => PostAsync<KbDocument>("add", document, MissingField);

    // Mọi thao tác của contract đều là POST với body JSON.
    private Task<TResponse> PostAsync<TResponse>(string path, object body, Func<TResponse, string?> findMissingField,
                                                 string? notFoundDocId = null)
        => _sender.SendAsync(HttpMethod.Post, path, body, findMissingField, notFoundDocId);

    // Các hàm dưới đây trả về tên field bắt buộc đầu tiên bị thiếu (null) trong response, hoặc null nếu đủ.
    // System.Text.Json không tự báo lỗi khi thiếu field: field thiếu chỉ đơn giản nhận giá trị null.
    // matchType không bắt buộc (architecture.md không có field này), thiếu thì mặc định là "title".
    private static string? MissingField(SearchResponse response)
        => response.Results is null
            ? "results"
            : response.Results.Select(item => MissingField(item?.Id, item?.Title, item?.NodePath))
                              .FirstOrDefault(field => field is not null);

    private static string? MissingField(ListResponse response)
        => response.Documents is null
            ? "documents"
            : response.Documents.Select(document => MissingField(document?.Id, document?.Title, document?.NodePath))
                                .FirstOrDefault(field => field is not null);

    private static string? MissingField(KbDocument document)
        => MissingField(document.Id, document.Title, document.NodePath)
           ?? (document.Content is null ? "content" : null)
           ?? (document.Tags is null || document.Tags.Any(tag => tag is null) ? "tags" : null);

    private static string? MissingField(string? id, string? title, string? nodePath)
        => id is null ? "id" : title is null ? "title" : nodePath is null ? "nodePath" : null;
}
