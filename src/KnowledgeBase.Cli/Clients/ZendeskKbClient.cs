using KnowledgeBase.Cli.Models;

namespace KnowledgeBase.Cli.Clients;

// Client đọc một Zendesk Help Center (KB thật, công khai), ví dụ https://support.zendesk.com.
// Chỉ đọc: tạo bài viết cần tài khoản agent của Zendesk, nên AddAsync không được hỗ trợ.
// Ánh xạ: section → node "/sections/{id}", label_names → tags, body (HTML) → chữ thường.
// Phần gửi request, log, timeout và đổi lỗi dùng lại KbHttpSender (giống HttpKbClient).
public class ZendeskKbClient : IKbClient
{
    // Ngôn ngữ mặc định của bài viết khi không đặt KB_ZENDESK_LOCALE.
    public const string DefaultLocale = "en-us";

    // httpClient.BaseAddress phải trỏ tới địa chỉ gốc của Help Center, ví dụ "https://support.zendesk.com/".
    // locale: ngôn ngữ của bài viết ("en-us", "vi"...), nằm trong đường dẫn hoặc query của Zendesk.
    // apiToken: OAuth token của Zendesk, không bắt buộc với Help Center công khai.
    // log: nơi ghi một dòng cho mỗi request (khi bật --verbose); null thì không ghi log.
    public ZendeskKbClient(HttpClient httpClient, string locale = DefaultLocale, string? apiToken = null,
                           TextWriter? log = null)
    {
    }

    public Uri? BaseAddress => throw new NotImplementedException();

    public string Locale => throw new NotImplementedException();

    // GET api/v2/help_center/articles/search.json?query=..&per_page=topK&locale=..
    // Giữ thứ tự xếp hạng của Zendesk; MATCH tự tính bằng KbMatcher (Zendesk không trả field này).
    public Task<IReadOnlyList<SearchResult>> SearchAsync(KbQuery query) => throw new NotImplementedException();

    // nodePath "/sections/{id}" → GET api/v2/help_center/{locale}/sections/{id}/articles.json?per_page=limit
    // Node khác dạng này, hoặc section không tồn tại (404) → danh sách rỗng, giống mock.
    public Task<IReadOnlyList<KbDocumentSummary>> ListAsync(string nodePath, int limit)
        => throw new NotImplementedException();

    // GET api/v2/help_center/{locale}/articles/{id}.json
    // id không phải số, hoặc Zendesk trả 404 → KbDocumentNotFoundException.
    public Task<KbDocument> RetrieveAsync(string docId) => throw new NotImplementedException();

    // Không gửi request: báo KbOperationNotSupportedException vì Help Center chỉ đọc.
    public Task<KbDocument> AddAsync(NewKbDocument document) => throw new NotImplementedException();
}
