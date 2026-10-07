using System.Net;
using System.Text;

namespace KnowledgeBase.UnitTests.Fakes;

// Bản giả của tầng mạng cho unit test HttpKbClient: không gửi request ra ngoài,
// chỉ ghi lại request mà client tạo ra và trả về response định sẵn (hoặc ném lỗi mạng).
public sealed class FakeHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> respond)
    : HttpMessageHandler
{
    public List<RecordedRequest> Requests { get; } = new();

    // Trả về body JSON cố định với status code cho trước.
    public static FakeHttpMessageHandler Json(string json, HttpStatusCode status = HttpStatusCode.OK)
        => new(_ => new HttpResponseMessage(status)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        });

    // Giả lập lỗi mạng: mất kết nối, hết thời gian chờ...
    public static FakeHttpMessageHandler Throws(Exception exception) => new(_ => throw exception);

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
                                                                 CancellationToken cancellationToken)
    {
        var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        Requests.Add(new RecordedRequest(request.Method, request.RequestUri!, body,
                                         request.Headers.Authorization?.ToString()));
        return respond(request);
    }
}

public record RecordedRequest(HttpMethod Method, Uri Uri, string? Body, string? Authorization);
