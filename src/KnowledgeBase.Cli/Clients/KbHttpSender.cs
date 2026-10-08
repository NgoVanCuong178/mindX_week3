using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace KnowledgeBase.Cli.Clients;

// Phần gửi request dùng chung cho mọi client gọi KB qua HTTP (HttpKbClient, và các client khác sau này).
// Mỗi client chỉ cần lo phần riêng của mình: tạo đường dẫn / body và ánh xạ dữ liệu.
// Mọi lỗi đều được đổi thành exception của project, với thông báo dễ hiểu cho người dùng:
//   mất kết nối / hết thời gian chờ / HTTP lỗi / JSON không đọc được  → KbApiException (exit 3)
//   response là null / thiếu field bắt buộc (findMissingField)       → KbApiException (exit 3)
//   HTTP 404 khi đang tìm một tài liệu (notFoundDocId)              → KbDocumentNotFoundException (exit 1)
internal sealed class KbHttpSender
{
    private readonly HttpClient _httpClient;
    private readonly string? _apiToken; // token xác thực
    private readonly TextWriter? _log;
    private readonly JsonSerializerOptions _jsonOptions;

    // httpClient.BaseAddress phải trỏ tới địa chỉ gốc của KB.
    // log: nơi ghi một dòng cho mỗi request (khi bật --verbose); null thì không ghi log.
    public KbHttpSender(HttpClient httpClient, string? apiToken, TextWriter? log, JsonSerializerOptions jsonOptions)
    {
        _httpClient = httpClient;
        _apiToken = apiToken;
        _log = log;
        _jsonOptions = jsonOptions;
    }

    public Uri? BaseAddress => _httpClient.BaseAddress;

    // Gửi request tới path (tương đối so với BaseAddress, có thể kèm query string) và đọc response JSON.
    // body: null thì không gửi body (GET); khác null thì gửi dạng JSON (POST).
    public async Task<TResponse> SendAsync<TResponse>(HttpMethod method, string path, object? body,
                                                      Func<TResponse, string?> findMissingField,
                                                      string? notFoundDocId = null)
    {
        using var request = new HttpRequestMessage(method, path);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body, options: _jsonOptions); // body --> JSON
        }
        if (_apiToken is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiToken); // gửi kèm token
        }

        // Tên thao tác dùng trong thông báo lỗi, bỏ phần query string: "POST /search", "GET /api/v2/...".
        var operation = $"{method} /{path.Split('?')[0]}";

        var stopwatch = Stopwatch.StartNew();
        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(request);
        }
        catch (HttpRequestException ex)
        {
            throw new KbApiException($"Cannot reach KB API at {BaseAddress}: {ex.Message}", ex); // mất kết nối
        }
        catch (TaskCanceledException ex)
        {
            throw new KbApiException(
                $"KB API at {BaseAddress} did not respond within {_httpClient.Timeout.TotalSeconds:0} seconds.", ex); // hết thời gian chờ
        }

        using (response)
        {
            // Log không bao giờ chứa token (token chỉ nằm trong header, không nằm trong URL).
            _log?.WriteLine($"{method} {request.RequestUri} -> {(int)response.StatusCode} {response.ReasonPhrase} " +
                            $"({stopwatch.ElapsedMilliseconds} ms)");

            if (response.StatusCode == HttpStatusCode.NotFound && notFoundDocId is not null)
            {
                throw new KbDocumentNotFoundException(notFoundDocId); // 404 khi tìm một tài liệu
            }

            if (!response.IsSuccessStatusCode) // 4xx, 5xx: kèm lý do server đưa ra, nếu có
            {
                var serverError = await ReadServerErrorAsync(response);
                throw new KbApiException(
                    $"KB API returned {(int)response.StatusCode} ({response.ReasonPhrase}) for {operation}" +
                    (serverError is null ? "." : $": {serverError}"));
            }

            TResponse? result;
            try
            {
                result = await response.Content.ReadFromJsonAsync<TResponse>(_jsonOptions);
            }
            catch (JsonException ex)
            {
                throw new KbApiException($"KB API returned an invalid response for {operation}.", ex); // JSON hỏng
            }

            // JSON hợp lệ nhưng không đúng contract: body là null, hoặc thiếu field bắt buộc.
            var missingField = result is null ? "body" : findMissingField(result);
            if (missingField is not null)
            {
                throw new KbApiException(
                    $"KB API returned an invalid response for {operation}: field '{missingField}' is missing.");
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
}
