namespace KnowledgeBase.Cli.Clients;

// Chọn client theo biến môi trường:
//   KB_CLIENT     mock (mặc định) | http
//   KB_API_URL    bắt buộc khi KB_CLIENT=http
//   KB_API_TOKEN  không bắt buộc, gửi kèm header "Authorization: Bearer <token>"
public static class KbClientFactory
{
    // tên biến môi trường
    public const string ClientVariable = "KB_CLIENT";
    public const string ApiUrlVariable = "KB_API_URL";
    public const string ApiTokenVariable = "KB_API_TOKEN";
    // thời gian tối đa mỗi request là 10 giây
    public static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(10);

    public static IKbClient Create(IReadOnlyDictionary<string, string?> environment, TextWriter? log = null)
        => throw new NotImplementedException();
}
