using KnowledgeBase.Cli;
using KnowledgeBase.Cli.Clients;
using KnowledgeBase.IntegrationTests.Support;

namespace KnowledgeBase.IntegrationTests.Commands;

// Integration test cho 4 lệnh `kb`: gọi CliApp.RunAsync ngay trong tiến trình test, đi qua mọi lớp thật
// (parse lệnh → KbService → client). Biến môi trường được truyền vào dưới dạng Dictionary nên
// đổi được giữa mock và HTTP mà không đụng tới biến môi trường thật của máy.
// Kiểm tra những gì người dùng thấy: exit code, stdout, stderr.
public sealed class KbCommandTests : IDisposable
{
    private static readonly Dictionary<string, string?> MockEnvironment = new();

    private readonly TempDirectory _temp = new();
    private readonly string _templateFile;

    public KbCommandTests()
    {
        _templateFile = _temp.File("new-template.md");
        File.WriteAllText(_templateFile, "# SMS Reminder Template\n\nHi {name}, your appointment is tomorrow.\n");
    }

    public void Dispose() => _temp.Dispose();

    // C01 — `kb search "response" --top-k 3` với mock (đúng ví dụ trong tasks.md).
    // Loại: Normal. Exit 0; có mẫu email doc-001, cột MATCH ghi "title"; stderr rỗng.
    [Fact]
    public async Task C01_Search_PrintsMatchingDocuments()
    {
        var result = await RunAsync(MockEnvironment, "search", "response", "--top-k", "3");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("doc-001", result.Output);
        Assert.Contains("Customer Response Template", result.Output);
        Assert.Contains("title", result.Output);
        Assert.Equal("", result.Error);
    }

    // C02 — `kb list --node /team/devops --limit 10`: tra cứu thông tin nhóm (AC1).
    // Loại: Normal. Chỉ có tài liệu của node /team/devops.
    [Fact]
    public async Task C02_List_PrintsDocumentsOfNode()
    {
        var result = await RunAsync(MockEnvironment, "list", "--node", "/team/devops", "--limit", "10");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("doc-002", result.Output);
        Assert.Contains("DevOps Team Members", result.Output);
        Assert.DoesNotContain("doc-001", result.Output);
    }

    // C03 — `kb retrieve doc-001`: in đủ title, node, tags và toàn bộ nội dung mẫu email (AC1).
    // Loại: Normal.
    [Fact]
    public async Task C03_Retrieve_PrintsFullDocument()
    {
        var result = await RunAsync(MockEnvironment, "retrieve", "doc-001");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Customer Response Template", result.Output);
        Assert.Contains("/templates/email", result.Output);
        Assert.Contains("template, email", result.Output);
        Assert.Contains("Thank you for contacting MindX support", result.Output);
    }

    // C04 — `kb add --file new-template.md --path /templates/sms --tags sms` (đúng ví dụ trong tasks.md).
    // Loại: Normal. Mock có 3 tài liệu nên tài liệu mới là doc-004.
    [Fact]
    public async Task C04_Add_PrintsNewDocumentId()
    {
        var result = await RunAsync(MockEnvironment, "add", "--file", _templateFile,
                                    "--path", "/templates/sms", "--tags", "sms");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Added document doc-004", result.Output);
    }

    // Dữ liệu cho C05: mỗi dòng là một đường xử lý lỗi khác nhau; các biến thể giá trị đã có ở unit test.
    public static TheoryData<string[]> InvalidInputs => new()
    {
        new[] { "list" },                                                              // thư viện parse: thiếu --node
        new[] { "search", "response", "--top-k", "0" },                                // KbService validate (U02)
        new[] { "add", "--file", "does-not-exist.md", "--path", "/templates/sms" },    // file không tồn tại
    };

    // C05 — Input sai → exit 1, thông báo chỉ ở stderr, stdout rỗng, không lộ exception/stack trace.
    // Loại: Abnormal. [EP] mỗi đường xử lý lỗi là một miền. [EG] thiếu file khi `kb add` (tasks.md).
    [Theory]
    [MemberData(nameof(InvalidInputs))]
    public async Task C05_InvalidInput_ExitsWithOne(string[] args)
    {
        var result = await RunAsync(MockEnvironment, args);

        Assert.Equal(1, result.ExitCode);
        Assert.False(string.IsNullOrWhiteSpace(result.Error));
        AssertNoTechnicalDetails(result.Error);
        Assert.Equal("", result.Output);
    }

    // C06 — `kb retrieve doc-999` → exit 1, "Document 'doc-999' not found".
    // Loại: Abnormal. [EP] miền "tài liệu không tồn tại".
    [Fact]
    public async Task C06_Retrieve_UnknownId_ReportsNotFound()
    {
        var result = await RunAsync(MockEnvironment, "retrieve", "doc-999");

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("Document 'doc-999' not found", result.Error);
        Assert.Equal("", result.Output);
    }

    // C07 — KB_CLIENT=http mà thiếu KB_API_URL → exit 1, thông báo nói rõ biến nào thiếu.
    // Loại: Abnormal. [DT] luật "http + không có URL" của bảng chọn client.
    [Fact]
    public async Task C07_HttpClientWithoutUrl_ReportsConfigurationError()
    {
        var result = await RunAsync(new() { ["KB_CLIENT"] = "http" }, "search", "response");

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("KB_API_URL", result.Error);
        AssertNoTechnicalDetails(result.Error);
    }

    // Dữ liệu cho C08: (lệnh, đoạn chữ phải có trong stdout). "{file}" được thay bằng file mẫu tạm.
    public static TheoryData<string[], string> HttpCommands => new()
    {
        { new[] { "search", "response", "--top-k", "3" }, "doc-001" },
        { new[] { "list", "--node", "/team/devops" }, "doc-002" },
        { new[] { "retrieve", "doc-001" }, "Thank you for contacting MindX support" },
        { new[] { "add", "--file", "{file}", "--path", "/templates/sms", "--tags", "sms" }, "Added document doc-004" },
    };

    // C08 — KB_CLIENT=http, KB_API_URL trỏ tới KnowledgeBase.Api → cả 4 lệnh chạy qua HTTP thật (AC2, AC3).
    // Loại: Normal. Kết quả phải giống khi chạy với mock (C01–C04).
    [Theory]
    [MemberData(nameof(HttpCommands))]
    public async Task C08_HttpClient_AllCommandsWork(string[] args, string expectedOutput)
    {
        await using var server = await RunningKbApi.StartAsync(new MockKbClient());

        var result = await RunAsync(HttpEnvironment(server.Url),
                                    args.Select(a => a == "{file}" ? _templateFile : a).ToArray());

        Assert.Equal(0, result.ExitCode);
        Assert.Contains(expectedOutput, result.Output);
        Assert.Equal("", result.Error);
    }

    // C09 — Không kết nối được KB API (cổng không có server) → exit 3, "Cannot reach KB API".
    // Loại: Abnormal. [EG] server tắt hoặc sai URL là lỗi tích hợp hay gặp nhất.
    [Fact]
    public async Task C09_ApiUnreachable_ExitsWithThree()
    {
        var result = await RunAsync(HttpEnvironment("http://127.0.0.1:9/"), "search", "response");

        Assert.Equal(3, result.ExitCode);
        Assert.Contains("Cannot reach KB API", result.Error);
        AssertNoTechnicalDetails(result.Error);
        Assert.Equal("", result.Output);
    }

    // C10 — KB API bị sự cố (server trả HTTP 500) → exit 3, thông báo có mã 500.
    // Loại: Abnormal. [EG] lỗi phía server.
    [Fact]
    public async Task C10_ApiServerError_ExitsWithThree()
    {
        await using var server = await RunningKbApi.StartAsync(new FailingKbClient());

        var result = await RunAsync(HttpEnvironment(server.Url), "search", "response");

        Assert.Equal(3, result.ExitCode);
        Assert.Contains("500", result.Error);
        AssertNoTechnicalDetails(result.Error);
    }

    // C11 — `--help` của CLI và của từng lệnh có mô tả rõ ràng (tasks.md: "Help text for each command").
    // Loại: Normal.
    [Theory]
    [InlineData(new[] { "--help" }, "Knowledge Base CLI")]
    [InlineData(new[] { "search", "--help" }, "Search documents")]
    [InlineData(new[] { "list", "--help" }, "List documents")]
    [InlineData(new[] { "retrieve", "--help" }, "Show a document")]
    [InlineData(new[] { "add", "--help" }, "Add a document")]
    public async Task C11_Help_DescribesCommands(string[] args, string expectedDescription)
    {
        var result = await RunAsync(MockEnvironment, args);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains(expectedDescription, result.Output);
    }

    // C12 — `--verbose` với HTTP client: in một dòng log mỗi request ra stderr (tasks.md: "Logging to
    // debug integration issues"); không có --verbose thì stderr rỗng (đã kiểm tra ở C08).
    // Loại: Normal. [DT] có / không có --verbose.
    [Fact]
    public async Task C12_Verbose_LogsHttpRequestsToStderr()
    {
        await using var server = await RunningKbApi.StartAsync(new MockKbClient());

        var result = await RunAsync(HttpEnvironment(server.Url), "search", "response", "--verbose");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("POST", result.Error);
        Assert.Contains("/search", result.Error);
        Assert.Contains("doc-001", result.Output);
    }

    // C13 — File có tồn tại nhưng không đọc được (đang bị chương trình khác khoá) → exit 1,
    // "Cannot read file", không để exception lọt ra ngoài.
    // Loại: Abnormal. [EG] file đang mở trong editor khác / không có quyền đọc.
    [Fact]
    public async Task C13_Add_UnreadableFile_ReportsReadError()
    {
        using var locked = new FileStream(_templateFile, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

        var result = await RunAsync(MockEnvironment, "add", "--file", _templateFile, "--path", "/templates/sms");

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("Cannot read file", result.Error);
        AssertNoTechnicalDetails(result.Error);
        Assert.Equal("", result.Output);
    }

    private static Dictionary<string, string?> HttpEnvironment(string url) => new()
    {
        [KbClientFactory.ClientVariable] = "http",
        [KbClientFactory.ApiUrlVariable] = url,
    };

    // Chạy CLI, thu stdout/stderr vào StringWriter thay vì in ra màn hình.
    private static async Task<CliResult> RunAsync(Dictionary<string, string?> environment, params string[] args)
    {
        var output = new StringWriter();
        var error = new StringWriter();

        var exitCode = await CliApp.RunAsync(args, output, error, environment);

        return new CliResult(exitCode, output.ToString(), error.ToString());
    }

    // Thông báo lỗi phải dễ hiểu: không lộ tên exception hay stack trace (giống Tuần 2).
    private static void AssertNoTechnicalDetails(string error)
    {
        Assert.DoesNotContain("Exception", error);
        Assert.DoesNotContain("   at ", error);
    }
}
