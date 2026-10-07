using KnowledgeBase.IntegrationTests.Support;

namespace KnowledgeBase.IntegrationTests.Integration;

// Chọn KB API cho test tích hợp R01 (dùng chung cho cả class, khởi động một lần):
//   - có biến KB_REAL_API_URL (và KB_REAL_API_TOKEN nếu cần) → dùng KB API ở URL đó;
//   - không có → khởi động KnowledgeBase.Api thành một TIẾN TRÌNH RIÊNG, dữ liệu lưu trong file tạm.
// Nhờ vậy R01 luôn chạy (không bị skip), và luôn đi qua mạng tới một server nằm ngoài tiến trình test.
public sealed class KbApiTarget : IAsyncLifetime
{
    private readonly TempDirectory _temp = new();
    private KbApiProcess? _process;

    public string Url { get; private set; } = "";
    public string? Token { get; private set; }

    public async Task InitializeAsync()
    {
        var externalUrl = Environment.GetEnvironmentVariable("KB_REAL_API_URL");
        if (!string.IsNullOrWhiteSpace(externalUrl))
        {
            Url = externalUrl;
            Token = Environment.GetEnvironmentVariable("KB_REAL_API_TOKEN");
            return;
        }

        _process = await KbApiProcess.StartAsync(_temp.File("kb-data.json"));
        Url = _process.Url;
    }

    public async Task DisposeAsync()
    {
        if (_process is not null)
        {
            await _process.DisposeAsync();
        }
        _temp.Dispose();
    }
}
