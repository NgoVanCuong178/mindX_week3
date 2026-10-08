namespace KnowledgeBase.Cli.Clients;

// Chọn client theo biến môi trường:
//   KB_CLIENT          mock (mặc định) | http | zendesk
//   KB_API_URL         bắt buộc khi KB_CLIENT=http hoặc zendesk
//   KB_API_TOKEN       không bắt buộc, gửi kèm header "Authorization: Bearer <token>"
//   KB_ZENDESK_LOCALE  không bắt buộc, mặc định "en-us" (chỉ dùng với zendesk)
public static class KbClientFactory
{
    // tên biến môi trường
    public const string ClientVariable = "KB_CLIENT";
    public const string ApiUrlVariable = "KB_API_URL";
    public const string ApiTokenVariable = "KB_API_TOKEN";
    public const string ZendeskLocaleVariable = "KB_ZENDESK_LOCALE"; // ngôn ngữ bài viết khi KB_CLIENT=zendesk
    // thời gian tối đa mỗi request là 10 giây
    public static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(10);

    public static IKbClient Create(IReadOnlyDictionary<string, string?> environment, TextWriter? log = null)
    {

        var kind = Read(environment, ClientVariable);

        // Không đặt hoặc để rỗng → dùng mock, để chạy được ngay mà không cần server.
        if (kind is null || kind.Equals("mock", StringComparison.OrdinalIgnoreCase))
        {
            return new MockKbClient();
        }
        var isZendesk = kind.Equals("zendesk", StringComparison.OrdinalIgnoreCase);
        // "ftp" và các giá trị lạ
        if (!isZendesk && !kind.Equals("http", StringComparison.OrdinalIgnoreCase))
        {
            throw new KbConfigurationException($"{ClientVariable} must be 'mock', 'http' or 'zendesk', not '{kind}'.");
        }
        // http / zendesk mà thiếu url
        var url = Read(environment, ApiUrlVariable)
                  ?? throw new KbConfigurationException(
                      $"{ApiUrlVariable} is required when {ClientVariable}={kind.ToLowerInvariant()}.");

        // Thêm "/" cuối nếu thiếu: không có nó, HttpClient ghép "https://kb.example.test" + "search"
        // thành "https://kb.example.testsearch".
        if (!Uri.TryCreate(url.TrimEnd('/') + "/", UriKind.Absolute, out var baseAddress)
            || (baseAddress.Scheme != Uri.UriSchemeHttp && baseAddress.Scheme != Uri.UriSchemeHttps))
        {
            throw new KbConfigurationException($"{ApiUrlVariable} must be an http(s) URL, not '{url}'.");
        }

        var httpClient = new HttpClient { BaseAddress = baseAddress, Timeout = RequestTimeout };
        var token = Read(environment, ApiTokenVariable);
        return isZendesk
            ? new ZendeskKbClient(httpClient, Read(environment, ZendeskLocaleVariable) ?? ZendeskKbClient.DefaultLocale,
                                  token, log)
            : new HttpKbClient(httpClient, token, log);
    }

    // Biến không có, hoặc có nhưng rỗng / chỉ khoảng trắng → coi như không đặt.
    private static string? Read(IReadOnlyDictionary<string, string?> environment, string name)
        => environment.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(value) ? value.Trim() : null;
}
