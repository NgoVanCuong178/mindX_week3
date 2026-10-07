using System.Net;
using System.Text.Json.Nodes;
using KnowledgeBase.Cli.Clients;
using KnowledgeBase.Cli.Models;
using KnowledgeBase.UnitTests.Fakes;

namespace KnowledgeBase.UnitTests.Clients;

// Unit test cho HttpKbClient: kiểm tra phần "dịch" giữa C# và HTTP/JSON theo API contract
// (architecture.md), không cần server thật — FakeHttpMessageHandler thay cho mạng.
public class HttpKbClientTests
{
    private const string BaseUrl = "https://kb.example.test/";

    private const string DocumentJson =
        """{"id":"doc-001","title":"Customer Response Template","content":"Dear customer","nodePath":"/templates/email","tags":["template","email"]}""";

    private static HttpKbClient CreateClient(FakeHttpMessageHandler handler, string? token = null,
                                             TextWriter? log = null)
        => new(new HttpClient(handler) { BaseAddress = new Uri(BaseUrl) }, token, log);

    // Dữ liệu cho U09: (thao tác, đường dẫn mong đợi, body JSON mong đợi, response trả về để lệnh chạy xong).
    public static TheoryData<Func<HttpKbClient, Task>, string, string, string> Requests => new()
    {
        { c => c.SearchAsync(new KbQuery("response", 3)), "/search",
          """{"query":"response","topK":3}""", """{"results":[]}""" },
        { c => c.ListAsync("/templates/email", 10), "/list",
          """{"nodePath":"/templates/email","limit":10}""", """{"documents":[]}""" },
        { c => c.RetrieveAsync("doc-001"), "/retrieve",
          """{"docId":"doc-001"}""", DocumentJson },
        { c => c.AddAsync(new NewKbDocument("New Template", "Hello", "/templates/sms", ["sms"])), "/add",
          """{"title":"New Template","content":"Hello","nodePath":"/templates/sms","tags":["sms"]}""", DocumentJson },
    };

    // U09 — Mỗi thao tác gửi đúng POST, đúng đường dẫn, đúng body JSON (camelCase như contract).
    // Loại: Normal. [EP] mỗi thao tác là một miền.
    [Theory]
    [MemberData(nameof(Requests))]
    public async Task U09_SendsJsonPostMatchingContract(Func<HttpKbClient, Task> call, string expectedPath,
                                                        string expectedBody, string response)
    {
        var handler = FakeHttpMessageHandler.Json(response);

        await call(CreateClient(handler));

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal(expectedPath, request.Uri.AbsolutePath);
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(expectedBody), JsonNode.Parse(request.Body!)),
                    $"Body was: {request.Body}");
    }

    // Dữ liệu cho U10: (thao tác trả về kết quả, response JSON của server, kết quả C# mong đợi).
    public static TheoryData<Func<HttpKbClient, Task<object>>, string, object> Responses => new()
    {
        { async c => await c.SearchAsync(new KbQuery("response", 5)),
          """{"results":[{"id":"doc-001","title":"Customer Response Template","nodePath":"/templates/email","matchType":"title"}]}""",
          new[] { new SearchResult(new KbDocumentSummary("doc-001", "Customer Response Template", "/templates/email"), MatchKind.Title) } },
        { async c => await c.ListAsync("/team/devops", 10),
          """{"documents":[{"id":"doc-002","title":"DevOps Team Members","nodePath":"/team/devops"}]}""",
          new[] { new KbDocumentSummary("doc-002", "DevOps Team Members", "/team/devops") } },
        { async c => await c.RetrieveAsync("doc-001"), DocumentJson,
          new KbDocument("doc-001", "Customer Response Template", "Dear customer", "/templates/email", ["template", "email"]) },
        { async c => await c.AddAsync(new NewKbDocument("x", "y", "/z", [])), DocumentJson,
          new KbDocument("doc-001", "Customer Response Template", "Dear customer", "/templates/email", ["template", "email"]) },
    };

    // U10 — Đọc đúng response JSON thành đối tượng C# cho cả 4 thao tác.
    // Loại: Normal. [EP] [EG] matchType trả về dạng chữ thường ("title") phải đọc được thành enum.
    [Theory]
    [MemberData(nameof(Responses))]
    public async Task U10_ParsesResponseMatchingContract(Func<HttpKbClient, Task<object>> call,
                                                         string response, object expected)
    {
        var result = await call(CreateClient(FakeHttpMessageHandler.Json(response)));

        Assert.Equivalent(expected, result, strict: true);
    }

    // U11 — retrieve: server trả HTTP 404 → KbDocumentNotFoundException mang đúng docId (giống mock).
    // Loại: Abnormal. [EP] miền "tài liệu không tồn tại".
    [Fact]
    public async Task U11_Retrieve_NotFound_ThrowsDocumentNotFound()
    {
        var client = CreateClient(FakeHttpMessageHandler.Json("""{"error":"not found"}""", HttpStatusCode.NotFound));

        var exception = await Assert.ThrowsAsync<KbDocumentNotFoundException>(() => client.RetrieveAsync("doc-999"));

        Assert.Equal("doc-999", exception.DocId);
    }

    // Dữ liệu cho U12: (handler giả lập lỗi, đoạn chữ phải có trong thông báo lỗi).
    public static TheoryData<FakeHttpMessageHandler, string> Failures => new()
    {
        { FakeHttpMessageHandler.Json("""{"error":"boom"}""", HttpStatusCode.InternalServerError), "500" },
        { FakeHttpMessageHandler.Json("""{"error":"bad"}""", HttpStatusCode.BadRequest), "400" },
        { FakeHttpMessageHandler.Json("this is not json"), "invalid response" },
        { FakeHttpMessageHandler.Throws(new HttpRequestException("Connection refused")), "Cannot reach KB API" },
        { FakeHttpMessageHandler.Throws(new TaskCanceledException("timeout", new TimeoutException())), "did not respond" },
    };

    // U12 — Lỗi từ API / lỗi đọc dữ liệu / lỗi kết nối → KbApiException với thông báo dễ hiểu.
    // Loại: Abnormal. [EG] 5 lỗi hay gặp khi gọi API (tasks.md: "connection, parsing, API errors").
    // Thông báo phải có địa chỉ server khi lỗi mạng, và không lộ chữ "Exception".
    [Theory]
    [MemberData(nameof(Failures))]
    public async Task U12_Failures_ThrowKbApiExceptionWithClearMessage(FakeHttpMessageHandler handler,
                                                                       string expectedMessagePart)
    {
        var exception = await Assert.ThrowsAsync<KbApiException>(
            () => CreateClient(handler).SearchAsync(new KbQuery("response", 5)));

        Assert.Contains(expectedMessagePart, exception.Message);
        Assert.DoesNotContain("Exception", exception.Message);
    }

    // U13 — Có KB_API_TOKEN thì gửi header "Authorization: Bearer <token>"; không có thì không gửi.
    // Loại: Normal. [DT] có token / không có token.
    [Theory]
    [InlineData("secret-token", "Bearer secret-token")]
    [InlineData(null, null)]
    public async Task U13_SendsBearerTokenOnlyWhenConfigured(string? token, string? expectedHeader)
    {
        var handler = FakeHttpMessageHandler.Json("""{"results":[]}""");

        await CreateClient(handler, token).SearchAsync(new KbQuery("response", 5));

        Assert.Equal(expectedHeader, Assert.Single(handler.Requests).Authorization);
    }

    // U14 — --verbose: mỗi request ghi một dòng log gồm method, URL và status code; KHÔNG BAO GIỜ chứa token.
    // Loại: Normal. [EG] lộ token trong log là lỗi bảo mật hay gặp.
    [Fact]
    public async Task U14_LogsRequestWithoutToken()
    {
        var log = new StringWriter();
        var handler = FakeHttpMessageHandler.Json("""{"results":[]}""");

        await CreateClient(handler, "secret-token", log).SearchAsync(new KbQuery("response", 5));

        var line = log.ToString();
        Assert.Contains("POST", line);
        Assert.Contains("https://kb.example.test/search", line);
        Assert.Contains("200", line);
        Assert.DoesNotContain("secret-token", line);
    }
}
