using KnowledgeBase.Cli.Clients;
using KnowledgeBase.Cli.Models;
using KnowledgeBase.IntegrationTests.Support;

namespace KnowledgeBase.IntegrationTests.Api;

// Integration test cho KnowledgeBase.Api khi chạy với file dữ liệu (cách chạy thật, không phải store
// trong bộ nhớ): lưu bền vững, dữ liệu khởi tạo theo architecture.md, xác thực bằng token.
// Hành vi của 4 endpoint đã được bộ contract test (K01–K06) kiểm tra; ở đây chỉ test phần riêng của server.
public sealed class KbApiServerTests : IDisposable
{
    private readonly TempDirectory _temp = new();
    private readonly string _dataFile;

    public KbApiServerTests()
    {
        _dataFile = _temp.File("kb-data.json");
    }

    public void Dispose() => _temp.Dispose();

    private static HttpKbClient Client(RunningKbApi server, string? token = null)
        => new(new HttpClient { BaseAddress = new Uri(server.Url) }, token);

    // V01 — Dữ liệu còn nguyên sau khi khởi động lại server (lưu vào file, không chỉ trong bộ nhớ).
    // Loại: Normal. [EG] dịch vụ chỉ lưu trong RAM sẽ mất toàn bộ tài liệu khi restart.
    [Fact]
    public async Task V01_Add_SurvivesServerRestart()
    {
        string id;
        await using (var first = await RunningKbApi.StartWithDataFileAsync(_dataFile))
        {
            id = (await Client(first).AddAsync(
                new NewKbDocument("SMS Reminder", "Hi {name}!", "/templates/sms", ["sms"]))).Id;
        }

        await using var second = await RunningKbApi.StartWithDataFileAsync(_dataFile);
        var document = await Client(second).RetrieveAsync(id);

        Assert.Equal("SMS Reminder", document.Title);
        Assert.Equal("Hi {name}!", document.Content);
    }

    // V02 — Lần chạy đầu (chưa có file dữ liệu): server tự tạo file với dữ liệu KB theo đúng cấu trúc
    // trong architecture.md — 3 node: /templates/email (3 tài liệu), /team/devops (3), /docs/guides (2).
    // Loại: Boundary (trạng thái ban đầu, chưa có file). [EP] mỗi node là một loại dữ liệu thực tế (AC1).
    [Fact]
    public async Task V02_FirstRun_CreatesDataFileWithKbStructure()
    {
        await using var server = await RunningKbApi.StartWithDataFileAsync(_dataFile);
        var client = Client(server);

        Assert.Equal(["doc-001", "doc-002", "doc-003"], (await client.ListAsync("/templates/email", 10)).Select(d => d.Id));
        Assert.Equal(["doc-004", "doc-005", "doc-006"], (await client.ListAsync("/team/devops", 10)).Select(d => d.Id));
        Assert.Equal(["doc-007", "doc-008"], (await client.ListAsync("/docs/guides", 10)).Select(d => d.Id));
        Assert.True(File.Exists(_dataFile));
    }

    // V03 — Server đặt KB_SERVER_TOKEN: request có đúng "Authorization: Bearer <token>" được phục vụ.
    // Loại: Normal. [DT] luật "có token × đúng token".
    [Fact]
    public async Task V03_Token_CorrectTokenIsAccepted()
    {
        await using var server = await RunningKbApi.StartWithDataFileAsync(_dataFile, token: "s3cret-token");

        Assert.NotEmpty(await Client(server, "s3cret-token").ListAsync("/templates/email", 10));
    }

    // V03 — Không gửi token, hoặc gửi sai token → server trả HTTP 401; HttpKbClient báo KbApiException có mã 401.
    // Loại: Abnormal. [DT] luật "có token × không có / sai token". [EG] quên cấu hình KB_API_TOKEN ở CLI.
    [Theory]
    [InlineData(null)]
    [InlineData("wrong-token")]
    public async Task V03_Token_MissingOrWrongTokenIsRejected(string? clientToken)
    {
        await using var server = await RunningKbApi.StartWithDataFileAsync(_dataFile, token: "s3cret-token");

        var exception = await Assert.ThrowsAsync<KbApiException>(
            () => Client(server, clientToken).ListAsync("/templates/email", 10));

        Assert.Contains("401", exception.Message);
    }
}
