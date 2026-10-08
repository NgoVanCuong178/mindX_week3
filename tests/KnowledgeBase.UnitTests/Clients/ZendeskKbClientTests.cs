using System.Net;
using KnowledgeBase.Cli.Clients;
using KnowledgeBase.Cli.Models;
using KnowledgeBase.UnitTests.Fakes;

namespace KnowledgeBase.UnitTests.Clients;

// Unit test cho ZendeskKbClient: dịch giữa model của bài (IKbClient) và Zendesk Help Center API,
// không cần mạng — FakeHttpMessageHandler thay cho Zendesk, response lấy từ dữ liệu thật (ZendeskFixtures).
public class ZendeskKbClientTests
{
    private const string BaseUrl = "https://kb.example.test/";

    // Tạo client gửi request vào handler giả thay vì mạng thật.
    private static ZendeskKbClient CreateClient(FakeHttpMessageHandler handler, string locale = "en-us",
                                                string? token = null, TextWriter? log = null)
        => new(new HttpClient(handler) { BaseAddress = new Uri(BaseUrl) }, locale, token, log);

    // Bọc một bài viết thành response của "GET .../articles/{id}.json": {"article": {...}}.
    private static string ArticleJson(string article) => """{"article":""" + article + "}";

    // Dữ liệu cho Z01: (locale, thao tác, đường dẫn + query mong đợi, response để thao tác chạy xong).
    public static TheoryData<string, Func<ZendeskKbClient, Task>, string, string> Requests => new()
    {
        { "en-us", c => c.SearchAsync(new KbQuery("password reset", 3)),
          "/api/v2/help_center/articles/search.json?query=password%20reset&per_page=3&locale=en-us",
          """{"results":[]}""" },
        { "en-us", c => c.SearchAsync(new KbQuery("a&b=c", 5)),                   // ký tự đặc biệt của query string
          "/api/v2/help_center/articles/search.json?query=a%26b%3Dc&per_page=5&locale=en-us",
          """{"results":[]}""" },
        { "vi", c => c.SearchAsync(new KbQuery("đăng nhập", 5)),                  // tiếng Việt, locale khác
          "/api/v2/help_center/articles/search.json?query=%C4%91%C4%83ng%20nh%E1%BA%ADp&per_page=5&locale=vi",
          """{"results":[]}""" },
        { "en-us", c => c.ListAsync("/sections/4405298881946", 10),
          "/api/v2/help_center/en-us/sections/4405298881946/articles.json?per_page=10",
          """{"articles":[]}""" },
        { "en-us", c => c.RetrieveAsync("4408894162714"),
          "/api/v2/help_center/en-us/articles/4408894162714.json",
          ZendeskFixtures.Read("article.json") },
    };

    // Z01 — Mỗi thao tác gửi GET (không có body) tới đúng đường dẫn của Zendesk, với query đã mã hoá URL.
    // Loại: Normal. [EP] mỗi thao tác là một miền. [EG] từ khoá có dấu cách, "&", "=", tiếng Việt.
    [Theory]
    [MemberData(nameof(Requests))]
    public async Task Z01_SendsGetMatchingZendeskApi(string locale, Func<ZendeskKbClient, Task> call,
                                                     string expectedPathAndQuery, string response)
    {
        var handler = FakeHttpMessageHandler.Json(response);

        await call(CreateClient(handler, locale));

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal(expectedPathAndQuery, request.Uri.PathAndQuery);
        Assert.Null(request.Body);
    }

    // Z02 — search (response thật): id thành chuỗi, section thành node "/sections/{id}", giữ thứ tự của Zendesk.
    // Loại: Normal. [EP]
    [Fact]
    public async Task Z02_Search_MapsRealResponse()
    {
        var client = CreateClient(FakeHttpMessageHandler.Json(ZendeskFixtures.Read("search.json")));

        var results = await client.SearchAsync(new KbQuery("password reset", 5));

        Assert.Equal(2, results.Count);
        Assert.Equal(new KbDocumentSummary(ZendeskFixtures.ArticleId, ZendeskFixtures.ArticleTitle,
                                           ZendeskFixtures.SectionNode), results[0].Document);
        Assert.Equal("10489214197786", results[1].Document.Id);
        Assert.Equal("/sections/4405298833818", results[1].Document.NodePath);
    }

    // Z02 — Zendesk trả nhiều hơn topK (server bỏ qua per_page) → client vẫn chỉ lấy topK kết quả.
    // Loại: Boundary. [BVA] topK = 1 khi có 2 kết quả.
    [Fact]
    public async Task Z02_Search_NeverReturnsMoreThanTopK()
    {
        var client = CreateClient(FakeHttpMessageHandler.Json(ZendeskFixtures.Read("search.json")));

        var results = await client.SearchAsync(new KbQuery("password reset", 1));

        Assert.Equal(ZendeskFixtures.ArticleId, Assert.Single(results).Document.Id);
    }

    // Z02 — list (response thật): các bài của section, cùng node.
    // Loại: Normal. [EP]
    [Fact]
    public async Task Z02_List_MapsRealResponse()
    {
        var client = CreateClient(FakeHttpMessageHandler.Json(ZendeskFixtures.Read("section-articles.json")));

        var documents = await client.ListAsync(ZendeskFixtures.SectionNode, 10);

        Assert.Equal(["11302967356058", "11270498158490"], documents.Select(d => d.Id));
        Assert.All(documents, d => Assert.Equal(ZendeskFixtures.SectionNode, d.NodePath));
        Assert.Equal("Error: Zendesk Support could not be loaded due to an error", documents[0].Title);
    }

    // Z02 — retrieve (response thật): label_names thành tags, body HTML thành chữ (không còn thẻ).
    // Loại: Normal. [EP]
    [Fact]
    public async Task Z02_Retrieve_MapsRealResponse()
    {
        var client = CreateClient(FakeHttpMessageHandler.Json(ZendeskFixtures.Read("article.json")));

        var document = await client.RetrieveAsync(ZendeskFixtures.ArticleId);

        Assert.Equal(ZendeskFixtures.ArticleId, document.Id);
        Assert.Equal(ZendeskFixtures.ArticleTitle, document.Title);
        Assert.Equal(ZendeskFixtures.SectionNode, document.NodePath);
        Assert.Equal(["mtpe", "ks", "ab0", "Support"], document.Tags);
        Assert.StartsWith("Question\n\nHow long are account verification emails", document.Content);
        Assert.Contains("Both account verification emails and password reset emails expire after 24 hours.",
                        document.Content);
        Assert.DoesNotContain("<", document.Content);
    }

    // Z03 — Zendesk không cho biết khớp ở đâu → client tự tính như mock (title → tag → content);
    // bài Zendesk tìm thấy nhờ biến thể từ (không chứa nguyên từ khoá) được tính là content.
    // Thứ tự kết quả giữ nguyên thứ tự xếp hạng của Zendesk, không sắp lại theo MATCH.
    // Loại: Normal. [DT] khớp title / tag / content / không chứa nguyên từ khoá.
    [Fact]
    public async Task Z03_Search_ComputesMatchKindAndKeepsZendeskOrder()
    {
        const string response = """
            {"results":[
              {"id":1,"title":"Account access","body":"<p>How to <b>reset</b> it</p>","section_id":9,"label_names":[]},
              {"id":2,"title":"Reset your password","body":"<p>Steps</p>","section_id":9,"label_names":[]},
              {"id":3,"title":"Login help","body":"<p>Steps</p>","section_id":9,"label_names":["password-reset"]},
              {"id":4,"title":"Sign in","body":"<p>Recover access</p>","section_id":9,"label_names":[]}
            ]}
            """;
        var client = CreateClient(FakeHttpMessageHandler.Json(response));

        var results = await client.SearchAsync(new KbQuery("reset", 10));

        Assert.Equal(["1", "2", "3", "4"], results.Select(r => r.Document.Id));
        Assert.Equal([MatchKind.Content, MatchKind.Title, MatchKind.Tag, MatchKind.Content],
                     results.Select(r => r.MatchType));
    }

    // Z04 — Node không có dạng "/sections/<số>" → không có tài liệu (giống mock với node không tồn tại),
    // và không gửi request nào.
    // Loại: Abnormal, Boundary. [EP] node của KB khác. [BVA] "/sections/" thiếu id. [EG] id không phải số.
    [Theory]
    [InlineData("/templates/email")]
    [InlineData("/sections/")]
    [InlineData("/sections/abc")]
    [InlineData("/sections/12/articles")]
    public async Task Z04_List_NodeIsNotASection_ReturnsEmptyWithoutRequest(string nodePath)
    {
        var handler = FakeHttpMessageHandler.Json("""{"articles":[]}""");

        var documents = await CreateClient(handler).ListAsync(nodePath, 10);

        Assert.Empty(documents);
        Assert.Empty(handler.Requests);
    }

    // Z04 — Section không tồn tại (Zendesk trả 404) → không có tài liệu, không báo lỗi.
    // Loại: Abnormal. [EP] node không tồn tại.
    [Fact]
    public async Task Z04_List_UnknownSection_ReturnsEmpty()
    {
        var handler = FakeHttpMessageHandler.Json("""{"error":"RecordNotFound","description":"Not found"}""",
                                                  HttpStatusCode.NotFound);

        Assert.Empty(await CreateClient(handler).ListAsync("/sections/1", 10));
    }

    // Z05 — id không phải số (id của Zendesk luôn là số) → not found ngay, không gửi request.
    // Loại: Abnormal. [EP] [EG] dùng nhầm id của mock ("doc-001").
    [Theory]
    [InlineData("doc-001")]
    [InlineData("abc")]
    [InlineData("12a")]
    public async Task Z05_Retrieve_NonNumericId_ThrowsNotFoundWithoutRequest(string docId)
    {
        var handler = FakeHttpMessageHandler.Json(ZendeskFixtures.Read("article.json"));

        var exception = await Assert.ThrowsAsync<KbDocumentNotFoundException>(
            () => CreateClient(handler).RetrieveAsync(docId));

        Assert.Equal(docId, exception.DocId);
        Assert.Empty(handler.Requests);
    }

    // Z05 — Zendesk trả 404 → KbDocumentNotFoundException mang đúng id (giống mock và HTTP client).
    // Loại: Abnormal. [EP]
    [Fact]
    public async Task Z05_Retrieve_NotFound_ThrowsDocumentNotFound()
    {
        var handler = FakeHttpMessageHandler.Json("""{"error":"RecordNotFound","description":"Not found"}""",
                                                  HttpStatusCode.NotFound);

        var exception = await Assert.ThrowsAsync<KbDocumentNotFoundException>(
            () => CreateClient(handler).RetrieveAsync("1"));

        Assert.Equal("1", exception.DocId);
    }

    // Z06 — add: Help Center chỉ đọc với người dùng không phải agent → báo rõ, không gửi request.
    // Loại: Abnormal. [DT] thao tác ghi × KB chỉ đọc.
    [Fact]
    public async Task Z06_Add_IsNotSupportedAndSendsNothing()
    {
        var handler = FakeHttpMessageHandler.Json("{}");

        var exception = await Assert.ThrowsAsync<KbOperationNotSupportedException>(
            () => CreateClient(handler).AddAsync(new NewKbDocument("T", "C", "/sections/1", [])));

        Assert.Contains("read-only", exception.Message);
        Assert.Empty(handler.Requests);
    }

    // Dữ liệu cho Z07: (handler giả lập lỗi, đoạn chữ phải có trong thông báo lỗi).
    public static TheoryData<FakeHttpMessageHandler, string> Failures => new()
    {
        { FakeHttpMessageHandler.Json("""{"error":"boom"}""", HttpStatusCode.InternalServerError), "500" },
        { FakeHttpMessageHandler.Json("""{"error":"TooManyRequests"}""", HttpStatusCode.TooManyRequests), "429" },
        { FakeHttpMessageHandler.Json("<html>maintenance</html>"), "invalid response" },
        { FakeHttpMessageHandler.Throws(new HttpRequestException("Connection refused")), "Cannot reach KB API" },
        { FakeHttpMessageHandler.Throws(new TaskCanceledException("timeout", new TimeoutException())), "did not respond" },
    };

    // Z07 — Lỗi từ Zendesk / lỗi mạng → KbApiException với thông báo dễ hiểu (giống HttpKbClient).
    // Loại: Abnormal. [EG] Zendesk giới hạn tốc độ gọi (429), bảo trì (trả HTML), mất mạng, quá thời gian.
    [Theory]
    [MemberData(nameof(Failures))]
    public async Task Z07_Failures_ThrowKbApiExceptionWithClearMessage(FakeHttpMessageHandler handler,
                                                                       string expectedMessagePart)
    {
        var exception = await Assert.ThrowsAsync<KbApiException>(
            () => CreateClient(handler).SearchAsync(new KbQuery("password", 5)));

        Assert.Contains(expectedMessagePart, exception.Message);
        Assert.DoesNotContain("Exception", exception.Message);
    }

    // Dữ liệu cho Z07: (thao tác, response thiếu field, tên field phải có trong thông báo lỗi).
    public static TheoryData<Func<ZendeskKbClient, Task>, string, string> MissingFields => new()
    {
        { c => c.SearchAsync(new KbQuery("x", 5)), "null", "body" },
        { c => c.SearchAsync(new KbQuery("x", 5)), """{"count":0}""", "results" },
        { c => c.SearchAsync(new KbQuery("x", 5)), """{"results":[{"id":1,"body":"","section_id":9}]}""", "title" },
        { c => c.SearchAsync(new KbQuery("x", 5)), """{"results":[{"id":1,"title":"T","body":""}]}""", "section_id" },
        { c => c.ListAsync("/sections/9", 10), """{"articles":null}""", "articles" },
        { c => c.ListAsync("/sections/9", 10), """{"articles":[{"title":"T","section_id":9}]}""", "id" },
        { c => c.RetrieveAsync("1"), """{}""", "article" },
        { c => c.RetrieveAsync("1"), ArticleJson("""{"id":1,"title":"T","section_id":9,"label_names":[]}"""), "body" },
    };

    // Z07 — Response thiếu field bắt buộc → KbApiException nói rõ field nào thiếu (giống HttpKbClient).
    // label_names không bắt buộc: thiếu thì coi như không có tag.
    // Loại: Abnormal. [EP] mỗi loại response là một miền. [EG] Zendesk đổi API.
    [Theory]
    [MemberData(nameof(MissingFields))]
    public async Task Z07_MissingField_ThrowsInvalidResponseNamingTheField(Func<ZendeskKbClient, Task> call,
                                                                           string response, string field)
    {
        var exception = await Assert.ThrowsAsync<KbApiException>(
            () => call(CreateClient(FakeHttpMessageHandler.Json(response))));

        Assert.Contains("invalid response", exception.Message);
        Assert.Contains($"'{field}'", exception.Message);
    }

    // Z07 — Thiếu label_names → tài liệu không có tag, không báo lỗi.
    // Loại: Boundary. [EG]
    [Fact]
    public async Task Z07_MissingLabels_MeansNoTags()
    {
        var client = CreateClient(FakeHttpMessageHandler.Json(
            ArticleJson("""{"id":1,"title":"T","body":"<p>C</p>","section_id":9}""")));

        Assert.Empty((await client.RetrieveAsync("1")).Tags);
    }

    // Z09 — Dùng lại phần gửi request của HttpKbClient: --verbose ghi một dòng log có method, URL, mã HTTP,
    // không chứa token; có token thì gửi header "Authorization: Bearer <token>" (OAuth của Zendesk).
    // Loại: Normal. [DT] có token × có log. [EG] lộ token trong log.
    [Fact]
    public async Task Z09_LogsRequestAndSendsTokenWithoutLoggingIt()
    {
        var log = new StringWriter();
        var handler = FakeHttpMessageHandler.Json("""{"results":[]}""");

        await CreateClient(handler, token: "secret-token", log: log).SearchAsync(new KbQuery("password", 5));

        Assert.Equal("Bearer secret-token", Assert.Single(handler.Requests).Authorization);
        var line = log.ToString();
        Assert.Contains("GET https://kb.example.test/api/v2/help_center/articles/search.json", line);
        Assert.Contains("200", line);
        Assert.DoesNotContain("secret-token", line);
    }
}
