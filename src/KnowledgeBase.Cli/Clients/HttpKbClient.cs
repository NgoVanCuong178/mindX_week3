using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
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

    // Gửi một request POST JSON tới path (tương đối so với BaseAddress) và đọc response JSON.
    // Mọi lỗi đều được đổi thành exception của project, với thông báo dễ hiểu cho người dùng:
    //   mất kết nối / hết thời gian chờ / HTTP lỗi / JSON không đọc được  → KbApiException (exit 3)
    //   response là null / thiếu field bắt buộc (findMissingField)       → KbApiException (exit 3)
    //   HTTP 404 khi retrieve                                           → KbDocumentNotFoundException (exit 1)
    private async Task<TResponse> PostAsync<TResponse>(string path, object body,
                                                       Func<TResponse, string?> findMissingField,
                                                       string? notFoundDocId = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = JsonContent.Create(body, options: KbApiJson.Options), // body --> JSON
        };
        if (_apiToken is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiToken); // kiểm tra token
        }

        var stopwatch = Stopwatch.StartNew();
        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(request);
        }
        catch (HttpRequestException ex)
        {
            throw new KbApiException($"Cannot reach KB API at {BaseAddress}: {ex.Message}", ex); //kiểm tra mất kết nối
        }
        catch (TaskCanceledException ex)
        {
            throw new KbApiException(
                $"KB API at {BaseAddress} did not respond within {_httpClient.Timeout.TotalSeconds:0} seconds.", ex); // hết time response
        }

        using (response)
        {
            // Log không bao giờ chứa token (token chỉ nằm trong header, không nằm trong URL).
            _log?.WriteLine($"POST {request.RequestUri} -> {(int)response.StatusCode} {response.ReasonPhrase} " +
                            $"({stopwatch.ElapsedMilliseconds} ms)");

            if (response.StatusCode == HttpStatusCode.NotFound && notFoundDocId is not null)
            {
                throw new KbDocumentNotFoundException(notFoundDocId); // 404 khi retrieve
            }

            if (!response.IsSuccessStatusCode) // 4xx, 5xx: kèm lý do server đưa ra, nếu có
            {
                var serverError = await ReadServerErrorAsync(response);
                throw new KbApiException(
                    $"KB API returned {(int)response.StatusCode} ({response.ReasonPhrase}) for POST /{path}" +
                    (serverError is null ? "." : $": {serverError}"));
            }

            TResponse? result;
            try
            {
                result = await response.Content.ReadFromJsonAsync<TResponse>(KbApiJson.Options);
            }
            catch (JsonException ex)
            {
                throw new KbApiException($"KB API returned an invalid response for POST /{path}.", ex); // JSON hỏng
            }

            // JSON hợp lệ nhưng không đúng contract: body là null, hoặc thiếu field bắt buộc.
            var missingField = result is null ? "body" : findMissingField(result);
            if (missingField is not null)
            {
                throw new KbApiException(
                    $"KB API returned an invalid response for POST /{path}: field '{missingField}' is missing.");
            }

            return result!;
        }
    }

    // Đọc {"error": "..."} trong body lỗi của KB API. Body khác dạng này (HTML, rỗng...) thì bỏ qua.
    private static async Task<string?> ReadServerErrorAsync(HttpResponseMessage response)
    {
        try
        {
            using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            return body.RootElement.ValueKind == JsonValueKind.Object
                   && body.RootElement.TryGetProperty("error", out var error)
                   && error.ValueKind == JsonValueKind.String
                ? error.GetString()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

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
