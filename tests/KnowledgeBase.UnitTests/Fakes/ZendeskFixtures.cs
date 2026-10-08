namespace KnowledgeBase.UnitTests.Fakes;

// Đọc response thật của Zendesk Help Center (đã rút gọn) trong tests/Fixtures/zendesk.
// Lấy từ https://support.zendesk.com ngày 2026-10-08:
//   search.json            GET api/v2/help_center/articles/search.json?query=password%20reset&per_page=2
//   section-articles.json  GET api/v2/help_center/en-us/sections/4405298881946/articles.json?per_page=2
//   article.json           GET api/v2/help_center/en-us/articles/4408894162714.json
public static class ZendeskFixtures
{
    public const string ArticleId = "4408894162714";
    public const string ArticleTitle = "How long are account verification emails and password reset emails valid?";
    public const string SectionNode = "/sections/4405298881946";

    public static string Read(string name)
        => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "zendesk", name));
}
