using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using KnowledgeBase.Api;

namespace KnowledgeBase.IntegrationTests.Support;

// Chạy KnowledgeBase.Api thành một TIẾN TRÌNH RIÊNG (`dotnet KnowledgeBase.Api.dll`), lưu dữ liệu ra file —
// giống một dịch vụ KB bên ngoài mà CLI phải gọi qua mạng. Dùng cho test tích hợp R01 khi không có
// KB_REAL_API_URL. Tự tắt tiến trình khi Dispose.
public sealed class KbApiProcess : IAsyncDisposable
{
    private static readonly string ApiDll = Path.Combine(AppContext.BaseDirectory, "KnowledgeBase.Api.dll");

    private readonly Process _process;

    private KbApiProcess(Process process, string url)
    {
        _process = process;
        Url = url;
    }

    public string Url { get; }

    public static async Task<KbApiProcess> StartAsync(string dataFile)
    {
        var url = $"http://127.0.0.1:{FindFreePort()}/";
        var startInfo = new ProcessStartInfo("dotnet")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        startInfo.ArgumentList.Add(ApiDll);
        startInfo.ArgumentList.Add($"--urls={url}");
        startInfo.ArgumentList.Add($"--{KbApiServer.DataFileSetting}={dataFile}");
        startInfo.Environment.Remove(KbApiServer.TokenSetting);

        var process = Process.Start(startInfo)!;
        process.OutputDataReceived += (_, _) => { };   // đọc bỏ output để bộ đệm không bị đầy
        process.ErrorDataReceived += (_, _) => { };
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        var server = new KbApiProcess(process, url);
        await server.WaitUntilRespondingAsync();
        return server;
    }

    // Chờ tới khi server trả lời được bất kỳ HTTP response nào (tối đa 20 giây).
    private async Task WaitUntilRespondingAsync()
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
        var deadline = DateTime.UtcNow.AddSeconds(20);
        while (DateTime.UtcNow < deadline)
        {
            if (_process.HasExited)
            {
                throw new InvalidOperationException($"KnowledgeBase.Api exited with code {_process.ExitCode}.");
            }

            try
            {
                await http.GetAsync(Url + "health");
                return;
            }
            catch (HttpRequestException)
            {
                await Task.Delay(200);
            }
            catch (TaskCanceledException)
            {
                await Task.Delay(200);
            }
        }

        throw new TimeoutException($"KnowledgeBase.Api did not start at {Url}.");
    }

    private static int FindFreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    public async ValueTask DisposeAsync()
    {
        if (!_process.HasExited)
        {
            _process.Kill(entireProcessTree: true);
            await _process.WaitForExitAsync();
        }
        _process.Dispose();
    }
}
