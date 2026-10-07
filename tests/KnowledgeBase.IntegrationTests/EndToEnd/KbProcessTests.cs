using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using KnowledgeBase.Cli.Clients;
using KnowledgeBase.IntegrationTests.Support;

namespace KnowledgeBase.IntegrationTests.EndToEnd;

// E2E test: chạy chương trình thật (KnowledgeBase.Cli.dll) trong tiến trình con, y như người dùng gõ `kb ...`.
// Đi qua Program.cs (đọc biến môi trường thật của tiến trình) — phần duy nhất các test khác không chạy qua.
public sealed class KbProcessTests : IDisposable
{
    private static readonly string CliDll = Path.Combine(AppContext.BaseDirectory, "KnowledgeBase.Cli.dll");

    private readonly TempDirectory _temp = new();

    public void Dispose() => _temp.Dispose();

    // E01 — Luồng đầy đủ qua mạng thật: add → search → retrieve, mỗi lệnh là một tiến trình riêng,
    // dữ liệu nằm trên KnowledgeBase.Api (AC3: "functional end-to-end").
    // Loại: Normal. Chứng minh: KB_CLIENT/KB_API_URL được đọc từ biến môi trường thật; tài liệu thêm ở
    // lần chạy thứ nhất vẫn thấy được ở lần chạy sau (vì nằm trên server, không phải trong bộ nhớ CLI).
    [Fact]
    public async Task E01_HttpFlow_AddSearchRetrieve()
    {
        await using var server = await RunningKbApi.StartAsync(new MockKbClient());
        var file = _temp.File("reminder.md");
        await File.WriteAllTextAsync(file, "# SMS Reminder Template\n\nHi {name}, your appointment is tomorrow.\n");
        var environment = new Dictionary<string, string?>
        {
            [KbClientFactory.ClientVariable] = "http",
            [KbClientFactory.ApiUrlVariable] = server.Url,
        };

        var add = await RunProcessAsync(environment, "add", "--file", file, "--path", "/templates/sms", "--tags", "sms");
        Assert.Equal(0, add.ExitCode);
        var id = Regex.Match(add.Output, @"doc-\d+").Value;
        Assert.NotEmpty(id);

        var search = await RunProcessAsync(environment, "search", "reminder");
        Assert.Equal(0, search.ExitCode);
        Assert.Contains(id, search.Output);

        var retrieve = await RunProcessAsync(environment, "retrieve", id);
        Assert.Equal(0, retrieve.ExitCode);
        Assert.Contains("Hi {name}, your appointment is tomorrow.", retrieve.Output);
    }

    // E02 — Không đặt biến môi trường nào → mặc định dùng mock client.
    // Loại: Boundary (cấu hình rỗng). [DT] luật mặc định của bảng chọn client.
    [Fact]
    public async Task E02_NoConfiguration_UsesMockClient()
    {
        var result = await RunProcessAsync(new Dictionary<string, string?>(), "search", "response");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("doc-001", result.Output);
    }

    // Chạy `dotnet KnowledgeBase.Cli.dll <args>`. Xoá các biến KB_* có sẵn trên máy rồi đặt đúng
    // những biến test cần, để kết quả không phụ thuộc cấu hình của máy chạy test.
    private static async Task<CliResult> RunProcessAsync(Dictionary<string, string?> environment, params string[] args)
    {
        var startInfo = new ProcessStartInfo("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            UseShellExecute = false,
        };
        startInfo.ArgumentList.Add(CliDll);
        foreach (var arg in args)
        {
            startInfo.ArgumentList.Add(arg);
        }
        foreach (var name in new[] { KbClientFactory.ClientVariable, KbClientFactory.ApiUrlVariable,
                                     KbClientFactory.ApiTokenVariable })
        {
            startInfo.Environment.Remove(name);
        }
        foreach (var (name, value) in environment)
        {
            startInfo.Environment[name] = value;
        }

        using var process = Process.Start(startInfo)!;
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync(new CancellationTokenSource(TimeSpan.FromSeconds(30)).Token);

        return new CliResult(process.ExitCode, await output, await error);
    }
}
