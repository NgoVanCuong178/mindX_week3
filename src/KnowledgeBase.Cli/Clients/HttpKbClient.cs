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
        var response = await PostAsync<SearchResponse>("search", query);
        return response.Results
            .Select(item => new SearchResult(new KbDocumentSummary(item.Id, item.Title, item.NodePath), item.MatchType))
            .ToList();
    }

    public async Task<IReadOnlyList<KbDocumentSummary>> ListAsync(string nodePath, int limit)
        => (await PostAsync<ListResponse>("list", new ListRequest(nodePath, limit))).Documents;

    public Task<KbDocument> RetrieveAsync(string docId)
        => PostAsync<KbDocument>("retrieve", new RetrieveRequest(docId), notFoundDocId: docId);

    public Task<KbDocument> AddAsync(NewKbDocument document)
        => PostAsync<KbDocument>("add", document);

    // Gửi một request POST JSON tới path (tương đối so với BaseAddress) và đọc response JSON.
    // Mọi lỗi đều được đổi thành exception của project, với thông báo dễ hiểu cho người dùng:
    //   mất kết nối / hết thời gian chờ / HTTP lỗi / JSON không đọc được → KbApiException (exit 3)
    //   HTTP 404 khi retrieve                                         → KbDocumentNotFoundException (exit 1)
    private async Task<TResponse> PostAsync<TResponse>(string path, object body, string? notFoundDocId = null)
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

            if (!response.IsSuccessStatusCode)
            {
                throw new KbApiException(
                    $"KB API returned {(int)response.StatusCode} ({response.ReasonPhrase}) for POST /{path}."); 
            }

            try
            {
                return (await response.Content.ReadFromJsonAsync<TResponse>(KbApiJson.Options))!; // 4xx, 5xx
            }
            catch (JsonException ex)
            {
                throw new KbApiException($"KB API returned an invalid response for POST /{path}.", ex); // JSON hỏng
            }
        }
    }
}
