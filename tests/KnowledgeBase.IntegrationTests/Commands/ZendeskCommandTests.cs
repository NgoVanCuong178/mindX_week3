using KnowledgeBase.Cli;
using KnowledgeBase.Cli.Clients;
using KnowledgeBase.IntegrationTests.Support;

namespace KnowledgeBase.IntegrationTests.Commands;

// Integration test cho các lệnh `kb` với KB_CLIENT=zendesk: đi qua mọi lớp thật (parse lệnh → KbService →
// ZendeskKbClient → HTTP thật) tới FakeZendeskServer, server trả response thật của Zendesk đã lưu sẵn.
// Kiểm tra những gì người dùng thấy: exit code, stdout, stderr — như KbCommandTests.
public sealed class ZendeskCommandTests : IAsyncLifetime
{
    private readonly TempDirectory _temp = new();
    private FakeZendeskServer _server = null!;

    // xUnit tạo một instance mới cho mỗi test: mỗi test có server giả riêng, chạy trước test và tắt sau test.
    public async Task InitializeAsync() => _server = await FakeZendeskServer.StartAsync();

    public async Task DisposeAsync()
    {
        await _server.DisposeAsync();
        _temp.Dispose();
    }

    // Biến môi trường để CLI dùng Zendesk client, trỏ tới server giả của test này.
    private Dictionary<string, string?> ZendeskEnvironment() => new()
    {
        [KbClientFactory.ClientVariable] = "zendesk",
        [KbClientFactory.ApiUrlVariable] = _server.Url,
    };

    // Dữ liệu cho C14: (lệnh, các đoạn chữ phải có trong stdout).
    public static TheoryData<string[], string[]> ReadCommands => new()
    {
        { new[] { "search", "password reset", "--top-k", "2" },
          new[] { FakeZendeskServer.ArticleId, FakeZendeskServer.SectionNode, "title", "How long are account verification" } },
        { new[] { "list", "--node", FakeZendeskServer.SectionNode, "--limit", "2" },
          new[] { "11302967356058", "Error: Zendesk Support could not be loaded due to an error" } },
        { new[] { "retrieve", FakeZendeskServer.ArticleId },
          new[] { "Node:  " + FakeZendeskServer.SectionNode, "Tags:  mtpe, ks, ab0, Support",
                  "Both account verification emails and password reset emails expire after 24 hours." } },
        { new[] { "list", "--node", "/sections/1" }, new[] { "No documents found." } },
    };

    // C14 — search, list, retrieve với KB_CLIENT=zendesk → exit 0, in dữ liệu của Zendesk bằng đúng định dạng
    // của mock/HTTP; retrieve in chữ thường, không còn thẻ HTML. Section không tồn tại → "No documents found.".
    // Loại: Normal, Abnormal (section không tồn tại).
    [Theory]
    [MemberData(nameof(ReadCommands))]
    public async Task C14_Zendesk_ReadCommandsWork(string[] args, string[] expectedOutput)
    {
        var result = await RunAsync(args);

        Assert.Equal(0, result.ExitCode);
        Assert.All(expectedOutput, expected => Assert.Contains(expected, result.Output));
        Assert.DoesNotContain("<p>", result.Output);
        Assert.Equal("", result.Error);
    }

    // C14 — retrieve bài không có trên Zendesk → exit 1, "Document '...' not found" (giống mock).
    // Loại: Abnormal. [EP]
    [Fact]
    public async Task C14_Zendesk_RetrieveUnknownArticle_ReportsNotFound()
    {
        var result = await RunAsync("retrieve", "999");

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("Document '999' not found", result.Error);
        Assert.Equal("", result.Output);
    }

    // C14 — add với Zendesk → exit 1, nói rõ Help Center chỉ đọc; không lộ exception / stack trace.
    // Loại: Abnormal. [DT] thao tác ghi × KB chỉ đọc.
    [Fact]
    public async Task C14_Zendesk_Add_ReportsReadOnly()
    {
        var file = _temp.File("new-article.md");
        File.WriteAllText(file, "# New article\n\nHello.\n");

        var result = await RunAsync("add", "--file", file, "--path", FakeZendeskServer.SectionNode);

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("read-only", result.Error);
        Assert.DoesNotContain("Exception", result.Error);
        Assert.Equal("", result.Output);
    }

    // C15 — --verbose với Zendesk: log "GET <url> -> 200" ra stderr, kết quả vẫn ở stdout.
    // Loại: Normal.
    [Fact]
    public async Task C15_Zendesk_Verbose_LogsGetRequests()
    {
        var result = await RunAsync("search", "password reset", "--verbose");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("GET " + _server.Url + "api/v2/help_center/articles/search.json", result.Error);
        Assert.Contains("200", result.Error);
        Assert.Contains(FakeZendeskServer.ArticleId, result.Output);
    }

    // Chạy CLI với KB_CLIENT=zendesk trỏ tới server giả; thu stdout/stderr vào StringWriter.
    private async Task<CliResult> RunAsync(params string[] args)
    {
        var output = new StringWriter();
        var error = new StringWriter();

        var exitCode = await CliApp.RunAsync(args, output, error, ZendeskEnvironment());

        return new CliResult(exitCode, output.ToString(), error.ToString());
    }
}
