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

        // Dùng đúng quy ước JSON mà HttpKbClient dùng (enum thành chữ "title"...), định nghĩa ở KbApiJson.
        builder.Services.ConfigureHttpJsonOptions(options => KbApiJson.Configure(options.SerializerOptions));

        // Body không đọc được (JSON hỏng, rỗng, null, sai kiểu): mặc định ASP.NET Core trả 400 với body rỗng.
        // Bật ThrowOnBadRequest để middleware bên dưới bắt được và trả {"error": ...} như mọi lỗi khác.
        builder.Services.Configure<RouteHandlerOptions>(options => options.ThrowOnBadRequest = true);

        var app = builder.Build();
        // nếu store chưa có thì gán. Test truyền store vào trong bộ nhớ, chạy thật thì dùng file.
        store ??= new FileKbStore(app.Configuration[DataFileSetting] ?? "kb-data.json");

        app.Use(async (context, next) => // chạy ngoài cùng, bọc mọi middleware và endpoint phía sau
        {
            try
            {
                await next(context);
            }
            catch (BadHttpRequestException ex)
            {
                context.Response.StatusCode = ex.StatusCode;
                await context.Response.WriteAsJsonAsync(
                    new { error = "Request body is missing or is not valid JSON for this endpoint." });
                return;
            }

            // Sai Content-Type: ASP.NET Core chỉ đặt mã 415 (không ném exception) và chưa ghi body.
            if (context.Response.StatusCode == StatusCodes.Status415UnsupportedMediaType && !context.Response.HasStarted)
            {
                await context.Response.WriteAsJsonAsync(new { error = "Content-Type must be application/json." });
            }
        });

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

        // Mỗi endpoint validate request trước (sai → 400), rồi mới chạm tới dữ liệu.
        app.MapPost("/search", async (KbQuery query) =>
        {
            if (KbRequestValidator.Validate(query) is { } error)
            {
                return BadRequest(error);
            }

            var results = await store.SearchAsync(query);
            return Results.Ok(new SearchResponse(results
                .Select(r => new SearchResultItem(r.Document.Id, r.Document.Title, r.Document.NodePath, r.MatchType))
                .ToList()));
        });

        app.MapPost("/list", async (ListRequest request) =>
        {
            if (KbRequestValidator.Validate(request) is { } error)
            {
                return BadRequest(error);
            }

            return Results.Ok(new ListResponse(await store.ListAsync(request.NodePath, request.Limit)));
        });

        app.MapPost("/retrieve", async (RetrieveRequest request) =>
        {
            if (KbRequestValidator.Validate(request) is { } error)
            {
                return BadRequest(error);
            }

            try
            {
                return Results.Ok(await store.RetrieveAsync(request.DocId));
            }
            catch (KbDocumentNotFoundException ex) // 404
            {
                return Results.NotFound(new { error = ex.Message });
            }
        });

        app.MapPost("/add", async (NewKbDocument document) =>
        {
            if (KbRequestValidator.Validate(document) is { } error)
            {
                return BadRequest(error);
            }

            // tags không gửi lên → lưu danh sách rỗng, để response luôn có "tags": [].
            return Results.Ok(await store.AddAsync(document with { Tags = document.Tags ?? [] }));
        });

        return app;
    }

    private static IResult BadRequest(string error) => Results.BadRequest(new { error });
}
