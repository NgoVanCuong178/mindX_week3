using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;

namespace KnowledgeBase.IntegrationTests.Support;

// Khởi động một WebApplication bằng Kestrel thật trên một cổng trống của 127.0.0.1, ngay trong tiến trình test.
// Dùng chung cho mọi server của test (KnowledgeBase.Api, server giả lập KB bên ngoài...).
public static class LocalServer
{
    // Trả về địa chỉ gốc của server, luôn có "/" ở cuối, ví dụ "http://127.0.0.1:53124/".
    public static async Task<string> StartOnFreePortAsync(WebApplication app)
    {
        app.Urls.Add("http://127.0.0.1:0");      // cổng 0 = để hệ điều hành chọn cổng trống
        await app.StartAsync();

        var address = app.Services.GetRequiredService<IServer>()
                         .Features.Get<IServerAddressesFeature>()!
                         .Addresses.First();
        return address.TrimEnd('/') + "/";
    }
}
