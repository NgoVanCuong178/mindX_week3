using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace KnowledgeBase.IntegrationTests.Support;

// Server giả lập Zendesk Help Center chạy bằng Kestrel thật (cổng ngẫu nhiên, 127.0.0.1), trả response thật
// đã lưu trong tests/Fixtures/zendesk. Dùng để test lệnh `kb` với KB_CLIENT=zendesk qua HTTP thật mà
// không phụ thuộc mạng hay dữ liệu thay đổi của Zendesk. Tự dừng khi Dispose.
//   GET api/v2/help_center/articles/search.json                          → search.json
//   GET api/v2/help_center/{locale}/sections/4405298881946/articles.json → section-articles.json; section khác → 404
//   GET api/v2/help_center/{locale}/articles/4408894162714.json          → article.json; bài khác → 404
public sealed class FakeZendeskServer : IAsyncDisposable
{
    public const string ArticleId = "4408894162714";
    public const string SectionNode = "/sections/4405298881946";

    private const string SectionId = "4405298881946";
    private const string NotFoundJson = """{"error":"RecordNotFound","description":"Not found"}""";

    private readonly WebApplication _app;

    private FakeZendeskServer(WebApplication app, string url)
    {
        _app = app;
        Url = url;
    }

    // Địa chỉ gốc, ví dụ "http://127.0.0.1:53124/".
    public string Url { get; }

    public static async Task<FakeZendeskServer> StartAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Logging.SetMinimumLevel(LogLevel.Warning);
        var app = builder.Build();

        app.MapGet("/api/v2/help_center/articles/search.json", () => Fixture("search.json"));
        app.MapGet("/api/v2/help_center/{locale}/sections/{id}/articles.json",
                   (string id) => id == SectionId ? Fixture("section-articles.json") : NotFound());
        app.MapGet("/api/v2/help_center/{locale}/articles/{id}.json",
                   (string id) => id == ArticleId ? Fixture("article.json") : NotFound());

        return new FakeZendeskServer(app, await LocalServer.StartOnFreePortAsync(app));
    }

    private static IResult Fixture(string name)
        => Results.Text(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "zendesk", name)),
                        "application/json");

    private static IResult NotFound() => Results.Text(NotFoundJson, "application/json", statusCode: 404);

    public async ValueTask DisposeAsync()
    {
        await _app.StopAsync();
        await _app.DisposeAsync();
    }
}
