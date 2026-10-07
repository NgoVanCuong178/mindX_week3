using System.Text.Json;
using System.Text.Json.Serialization;
using KnowledgeBase.Cli.Clients;
using KnowledgeBase.Cli.Models;

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
        var builder = WebApplication.CreateBuilder(args);

        // Ghi enum thành chữ ("title") thay vì số, đúng như HttpKbClient mong đợi (KbApiJson).
        builder.Services.ConfigureHttpJsonOptions(options =>
            options.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)));

        var app = builder.Build();
        // nếu store chưa có thì gán. Test truyền store vào trong bộ nhớ, chạy thật thì dùng file.
        store ??= new FileKbStore(app.Configuration[DataFileSetting] ?? "kb-data.json");

        var token = app.Configuration[TokenSetting];
        if (!string.IsNullOrEmpty(token))
        {
            app.Use(async (context, next) => // chạy trước mọi endpoint (middleware).
            {
                if (context.Request.Path != "/health"
                    && context.Request.Headers.Authorization != $"Bearer {token}")
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    await context.Response.WriteAsJsonAsync(new { error = "Missing or invalid token." });
                    return; // chặn
                }

                await next(context); // hợp lệ chuyển tiếp
            });
        }

        app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

        app.MapPost("/search", async (KbQuery query) =>
        {
            var results = await store.SearchAsync(query);
            return Results.Ok(new SearchResponse(results
                .Select(r => new SearchResultItem(r.Document.Id, r.Document.Title, r.Document.NodePath, r.MatchType))
                .ToList()));
        });

        app.MapPost("/list", async (ListRequest request)
            => Results.Ok(new ListResponse(await store.ListAsync(request.NodePath, request.Limit))));

        app.MapPost("/retrieve", async (RetrieveRequest request) =>
        {
            try
            {
                return Results.Ok(await store.RetrieveAsync(request.DocId));
            }
            catch (KbDocumentNotFoundException ex) // 404
            {
                return Results.NotFound(new { error = ex.Message });
            }
        });

        app.MapPost("/add", async (NewKbDocument document) => Results.Ok(await store.AddAsync(document)));

        return app;
    }
}
