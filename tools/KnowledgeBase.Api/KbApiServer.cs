using KnowledgeBase.Cli.Clients;

namespace KnowledgeBase.Api;

// KB API (HTTP) của project, tự dựng theo API contract trong architecture.md vì không được cung cấp KB API.
// Chạy độc lập (dotnet run) và lưu dữ liệu vào file JSON.
//
// Cấu hình (biến môi trường hoặc tham số --KEY=value):
//   KB_SERVER_DATA_FILE  đường dẫn file dữ liệu (mặc định: kb-data.json trong thư mục đang chạy)
//   KB_SERVER_TOKEN      nếu đặt, mọi request trừ GET /health phải có "Authorization: Bearer <token>"
//
// Test có thể truyền vào một store trong bộ nhớ (store) thay cho file dữ liệu.
public static class KbApiServer
{
    public const string DataFileSetting = "KB_SERVER_DATA_FILE";
    public const string TokenSetting = "KB_SERVER_TOKEN";
    public const string DefaultUrl = "http://localhost:5080";

    public static WebApplication Build(string[] args, IKbClient? store = null)
    {
        var app = WebApplication.CreateBuilder(args).Build();

        // Stub: mọi endpoint trả 501 Not Implemented cho tới giai đoạn Green.
        app.MapGet("/health", () => Results.StatusCode(StatusCodes.Status501NotImplemented));
        app.MapPost("/search", () => Results.StatusCode(StatusCodes.Status501NotImplemented));
        app.MapPost("/list", () => Results.StatusCode(StatusCodes.Status501NotImplemented));
        app.MapPost("/retrieve", () => Results.StatusCode(StatusCodes.Status501NotImplemented));
        app.MapPost("/add", () => Results.StatusCode(StatusCodes.Status501NotImplemented));

        return app;
    }
}
