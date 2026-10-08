using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using KnowledgeBase.Cli.Clients;
using KnowledgeBase.Cli.Models;
using KnowledgeBase.IntegrationTests.Support;

namespace KnowledgeBase.IntegrationTests.Api;

// Integration test cho phần validate request của KnowledgeBase.Api: request sai phải bị từ chối bằng
// HTTP 400 kèm {"error": "..."} nói rõ sai ở đâu, và không được làm thay đổi dữ liệu.
// Gửi JSON thô bằng HttpClient (không qua HttpKbClient), vì CLI đã validate trước khi gửi nên
// không bao giờ tạo ra những request sai này — nhưng client khác (curl, Postman, chương trình khác) thì có thể.
public class KbApiValidationTests
{
    private static async Task<HttpResponseMessage> PostRawAsync(RunningKbApi server, string path, string body,
                                                                string contentType = "application/json")
    {
        using var http = new HttpClient { BaseAddress = new Uri(server.Url) };
        return await http.PostAsync(path, new StringContent(body, Encoding.UTF8, contentType));
    }

    private static async Task<string> ReadErrorAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("error").GetString()!;
    }

    // Dữ liệu cho V04: (body của /add, đoạn chữ phải có trong "error").
    public static TheoryData<string, string> InvalidAddBodies => new()
    {
        { """{"title":"","content":"Hello","nodePath":"/templates/sms","tags":[]}""", "title" },
        { """{"title":"   ","content":"Hello","nodePath":"/templates/sms","tags":[]}""", "title" },
        { """{"content":"Hello","nodePath":"/templates/sms","tags":[]}""", "title" },
        { """{"title":"T","content":"  ","nodePath":"/templates/sms","tags":[]}""", "content" },
        { """{"title":"T","content":"Hello","nodePath":"templates/sms","tags":[]}""", "nodePath" },
        { """{"title":"T","content":"Hello","tags":[]}""", "nodePath" },
        { """{"title":"T","content":"Hello","nodePath":"/templates/sms","tags":["sms",""]}""", "tags" },
        { """{"title":"T","content":"Hello","nodePath":"/templates/sms","tags":["sms",null]}""", "tags" },
    };

    // V04 — /add với dữ liệu không hợp lệ → HTTP 400, "error" nói rõ field sai, tài liệu KHÔNG được lưu.
    // Loại: Abnormal, Boundary (chuỗi rỗng / chỉ khoảng trắng). [EP] mỗi field bắt buộc là một miền.
    // [EG] thiếu field, field null, node thiếu "/", tag rỗng.
    [Theory]
    [MemberData(nameof(InvalidAddBodies))]
    public async Task V04_Add_InvalidDocument_Returns400AndSavesNothing(string body, string expectedField)
    {
        var store = new MockKbClient();
        await using var server = await RunningKbApi.StartAsync(store);

        using var response = await PostRawAsync(server, "add", body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(expectedField, await ReadErrorAsync(response));
        Assert.Empty(await store.ListAsync("/templates/sms", 100));
    }

    // V04 — /add không có "tags" vẫn hợp lệ (tags không bắt buộc), lưu với danh sách tag rỗng.
    // Loại: Boundary. [EG] tag là field tuỳ chọn, client khác có thể bỏ qua.
    [Fact]
    public async Task V04_Add_WithoutTags_IsAcceptedWithEmptyTags()
    {
        await using var server = await RunningKbApi.StartAsync(new MockKbClient());

        using var response = await PostRawAsync(server, "add",
                                                """{"title":"T","content":"Hello","nodePath":"/templates/sms"}""");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var document = await response.Content.ReadFromJsonAsync<KbDocument>(KbApiJson.Options);
        Assert.Empty(document!.Tags);
    }

    // Dữ liệu cho V05: (endpoint, body, đoạn chữ phải có trong "error"). Giới hạn giống CLI (KbService).
    public static TheoryData<string, string, string> InvalidQueries => new()
    {
        { "search", """{"query":"","topK":5}""", "query" },
        { "search", """{"topK":5}""", "query" },
        { "search", """{"query":"response","topK":0}""", "topK" },
        { "search", """{"query":"response","topK":51}""", "topK" },
        { "list", """{"nodePath":"templates/email","limit":10}""", "nodePath" },
        { "list", """{"nodePath":"/templates/email","limit":0}""", "limit" },
        { "list", """{"nodePath":"/templates/email","limit":101}""", "limit" },
        { "retrieve", """{"docId":"  "}""", "docId" },
        { "retrieve", """{}""", "docId" },
    };

    // V05 — /search, /list, /retrieve với dữ liệu không hợp lệ → HTTP 400 nói rõ field sai.
    // Loại: Abnormal, Boundary. [BVA] topK 0/51, limit 0/101 (giới hạn 1–50 và 1–100 như CLI). [EP] [EG]
    [Theory]
    [MemberData(nameof(InvalidQueries))]
    public async Task V05_InvalidQuery_Returns400(string path, string body, string expectedField)
    {
        await using var server = await RunningKbApi.StartAsync(new MockKbClient());

        using var response = await PostRawAsync(server, path, body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(expectedField, await ReadErrorAsync(response));
    }

    // Dữ liệu cho V06: (body, Content-Type, mã HTTP mong đợi).
    public static TheoryData<string, string, HttpStatusCode> MalformedBodies => new()
    {
        { "{", "application/json", HttpStatusCode.BadRequest },                  // JSON hỏng
        { "", "application/json", HttpStatusCode.BadRequest },                   // body rỗng
        { "null", "application/json", HttpStatusCode.BadRequest },               // JSON null
        { """{"title":123}""", "application/json", HttpStatusCode.BadRequest },  // sai kiểu dữ liệu
        { "hello", "text/plain", HttpStatusCode.UnsupportedMediaType },          // không phải JSON
    };

    // V06 — Body không đọc được thành JSON đúng dạng → lỗi 4xx vẫn có body {"error": "..."} như mọi lỗi khác,
    // không phải body rỗng hay trang HTML.
    // Loại: Abnormal. [EG] JSON hỏng, body rỗng, sai Content-Type.
    [Theory]
    [MemberData(nameof(MalformedBodies))]
    public async Task V06_MalformedBody_ReturnsJsonError(string body, string contentType, HttpStatusCode expectedStatus)
    {
        await using var server = await RunningKbApi.StartAsync(new MockKbClient());

        using var response = await PostRawAsync(server, "add", body, contentType);

        Assert.Equal(expectedStatus, response.StatusCode);
        Assert.False(string.IsNullOrWhiteSpace(await ReadErrorAsync(response)));
    }

    // V07 — Từ đầu tới cuối: HttpKbClient gửi tài liệu sai (bỏ qua CLI) → KbApiException có mã 400 và lý do
    // của server, để người dùng biết phải sửa gì.
    // Loại: Abnormal. [EG]
    [Fact]
    public async Task V07_HttpKbClient_ReportsServerValidationMessage()
    {
        await using var server = await RunningKbApi.StartAsync(new MockKbClient());
        var client = new HttpKbClient(new HttpClient { BaseAddress = new Uri(server.Url) });

        var exception = await Assert.ThrowsAsync<KbApiException>(
            () => client.AddAsync(new NewKbDocument("", "Hello", "/templates/sms", [])));

        Assert.Contains("400", exception.Message);
        Assert.Contains("title", exception.Message);
    }
}
