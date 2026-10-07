using KnowledgeBase.Api;
using KnowledgeBase.Cli.Clients;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;

namespace KnowledgeBase.IntegrationTests.Support;

// Chạy KnowledgeBase.Api NGAY TRONG tiến trình test, bằng Kestrel thật trên một cổng ngẫu nhiên của
// 127.0.0.1, để HttpKbClient, lệnh CLI và tiến trình con (E2E) đều gọi được qua mạng thật.
// Tự dừng server khi Dispose.
public sealed class RunningKbApi : IAsyncDisposable
{
    private readonly WebApplication _app;

    private RunningKbApi(WebApplication app, string url)
    {
        _app = app;
        Url = url;
    }

    // Địa chỉ gốc của server, ví dụ "http://127.0.0.1:53124/".
    public string Url { get; }

    // Dữ liệu nằm trong bộ nhớ (store), dùng cho test cần dữ liệu cố định.
    public static Task<RunningKbApi> StartAsync(IKbClient store) => StartAsync([], store);

    // Dữ liệu nằm trong file (giống khi chạy thật); token tuỳ chọn.
    public static Task<RunningKbApi> StartWithDataFileAsync(string dataFile, string? token = null)
    {
        var args = new List<string> { $"--{KbApiServer.DataFileSetting}={dataFile}" };
        if (token is not null)
        {
            args.Add($"--{KbApiServer.TokenSetting}={token}");
        }
        return StartAsync(args, store: null);
    }

    private static async Task<RunningKbApi> StartAsync(IEnumerable<string> args, IKbClient? store)
    {
        var app = KbApiServer.Build([.. args, "--Logging:LogLevel:Default=Warning"], store);
        app.Urls.Add("http://127.0.0.1:0");      // cổng 0 = để hệ điều hành chọn cổng trống
        await app.StartAsync();

        var address = app.Services.GetRequiredService<IServer>()
                         .Features.Get<IServerAddressesFeature>()!
                         .Addresses.First();
        return new RunningKbApi(app, address.TrimEnd('/') + "/");
    }

    public async ValueTask DisposeAsync()
    {
        await _app.StopAsync();
        await _app.DisposeAsync();
    }
}
