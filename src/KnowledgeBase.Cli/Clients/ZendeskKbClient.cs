using System.Globalization;
using System.Text.RegularExpressions;
using KnowledgeBase.Cli.Models;

namespace KnowledgeBase.Cli.Clients;

// Client đọc một Zendesk Help Center (KB thật, công khai), ví dụ https://support.zendesk.com.
// Chỉ đọc: tạo bài viết cần tài khoản agent của Zendesk, nên AddAsync không được hỗ trợ.
// Ánh xạ: section → node "/sections/{id}", label_names → tags, body (HTML) → chữ thường.
// Phần gửi request, log, timeout và đổi lỗi dùng lại KbHttpSender (giống HttpKbClient).
public partial class ZendeskKbClient : IKbClient
{
    // Ngôn ngữ mặc định của bài viết khi không đặt KB_ZENDESK_LOCALE.
    public const string DefaultLocale = "en-us";

    private const string ApiRoot = "api/v2/help_center";

    private readonly KbHttpSender _sender;

    // httpClient.BaseAddress phải trỏ tới địa chỉ gốc của Help Center, ví dụ "https://support.zendesk.com/".
    // locale: ngôn ngữ của bài viết ("en-us", "vi"...), nằm trong đường dẫn hoặc query của Zendesk.
    // apiToken: OAuth token của Zendesk, không bắt buộc với Help Center công khai.
    // log: nơi ghi một dòng cho mỗi request (khi bật --verbose); null thì không ghi log.
    public ZendeskKbClient(HttpClient httpClient, string locale = DefaultLocale, string? apiToken = null,
                           TextWriter? log = null)
    {
        _sender = new KbHttpSender(httpClient, apiToken, log, ZendeskJson.Options);
        Locale = locale;
    }

    public Uri? BaseAddress => _sender.BaseAddress;

    public string Locale { get; }

    // GET api/v2/help_center/articles/search.json?query=..&per_page=topK&locale=..
    // Giữ thứ tự xếp hạng của Zendesk; MATCH tự tính bằng KbMatcher (Zendesk không trả field này).
    public async Task<IReadOnlyList<SearchResult>> SearchAsync(KbQuery query)
    {
        var path = $"{ApiRoot}/articles/search.json?query={Uri.EscapeDataString(query.Query)}" +
                   $"&per_page={query.TopK}&locale={Uri.EscapeDataString(Locale)}";
        var response = await _sender.SendAsync<ZendeskSearchResponse>(
            HttpMethod.Get, path, body: null, r => MissingField(r.Results, "results"));

        return response.Results!
            .Take(query.TopK) // phòng khi server trả nhiều hơn per_page
            .Select(article => ToDocument(article!))
            .Select(document => new SearchResult(Summarize(document),
                                                 // Zendesk tìm cả biến thể của từ ("resetting"), nên có bài
                                                 // không chứa nguyên từ khoá: tính là khớp ở content.
                                                 KbMatcher.FindMatch(document, query.Query) ?? MatchKind.Content))
            .ToList();
    }

    // nodePath "/sections/{id}" → GET api/v2/help_center/{locale}/sections/{id}/articles.json?per_page=limit
    // Node khác dạng này, hoặc section không tồn tại (404) → danh sách rỗng, giống mock.
    public async Task<IReadOnlyList<KbDocumentSummary>> ListAsync(string nodePath, int limit)
    {
        var match = SectionNode().Match(nodePath);
        if (!match.Success)
        {
            return [];
        }

        var path = $"{ApiRoot}/{Uri.EscapeDataString(Locale)}/sections/{match.Groups[1].Value}/articles.json" +
                   $"?per_page={limit}";
        ZendeskArticlesResponse response;
        try
        {
            response = await _sender.SendAsync<ZendeskArticlesResponse>(
                HttpMethod.Get, path, body: null, r => MissingField(r.Articles, "articles"), notFoundDocId: nodePath);
        }
        catch (KbDocumentNotFoundException) // section không tồn tại
        {
            return [];
        }

        return response.Articles!.Take(limit).Select(article => Summarize(ToDocument(article!))).ToList();
    }

    // GET api/v2/help_center/{locale}/articles/{id}.json
    // id không phải số, hoặc Zendesk trả 404 → KbDocumentNotFoundException.
    public async Task<KbDocument> RetrieveAsync(string docId)
    {
        if (!NumericId().IsMatch(docId)) // id của Zendesk luôn là số: khỏi gửi request vô ích
        {
            throw new KbDocumentNotFoundException(docId);
        }

        var path = $"{ApiRoot}/{Uri.EscapeDataString(Locale)}/articles/{docId}.json";
        var response = await _sender.SendAsync<ZendeskArticleResponse>(
            HttpMethod.Get, path, body: null,
            r => r.Article is null ? "article" : MissingField(r.Article, requireBody: true),
            notFoundDocId: docId);

        return ToDocument(response.Article!);
    }

    // Không gửi request: báo KbOperationNotSupportedException vì Help Center chỉ đọc.
    public Task<KbDocument> AddAsync(NewKbDocument document)
        => Task.FromException<KbDocument>(new KbOperationNotSupportedException(
            "Zendesk Help Center is read-only: kb add is not supported with KB_CLIENT=zendesk."));

    // Bài viết Zendesk → tài liệu của bài. Chỉ gọi sau khi MissingField đã xác nhận đủ field.
    private static KbDocument ToDocument(ZendeskArticle article)
        => new(article.Id!.Value.ToString(CultureInfo.InvariantCulture),
               article.Title!,
               HtmlText.ToPlainText(article.Body ?? ""),
               $"/sections/{article.SectionId!.Value.ToString(CultureInfo.InvariantCulture)}",
               article.LabelNames?.Where(label => label is not null).ToList() ?? []); // thiếu label_names → không có tag

    private static KbDocumentSummary Summarize(KbDocument document)
        => new(document.Id, document.Title, document.NodePath);

    // Tên field bắt buộc đầu tiên bị thiếu trong danh sách bài viết, hoặc null nếu đủ.
    private static string? MissingField(IReadOnlyList<ZendeskArticle?>? articles, string listName)
        => articles is null
            ? listName
            : articles.Select(article => article is null ? "id" : MissingField(article, requireBody: false))
                      .FirstOrDefault(field => field is not null);

    // body chỉ bắt buộc khi xem chi tiết; ở search / list thiếu body thì coi như rỗng.
    private static string? MissingField(ZendeskArticle article, bool requireBody)
        => article.Id is null ? "id"
         : article.Title is null ? "title"
         : article.SectionId is null ? "section_id"
         : requireBody && article.Body is null ? "body"
         : null;

    [GeneratedRegex(@"^/sections/(\d+)$")]
    private static partial Regex SectionNode();

    [GeneratedRegex(@"^\d+$")]
    private static partial Regex NumericId();
}
