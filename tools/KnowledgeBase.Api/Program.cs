using KnowledgeBase.Api;

var app = KbApiServer.Build(args);

// Mặc định lắng nghe ở http://localhost:5080, trừ khi --urls hoặc ASPNETCORE_URLS chỉ định địa chỉ khác.
if (app.Urls.Count == 0 && string.IsNullOrEmpty(app.Configuration["urls"]))
{
    app.Urls.Add(KbApiServer.DefaultUrl);
}

app.Run();
