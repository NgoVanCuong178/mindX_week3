using KnowledgeBase.Cli.Clients;

namespace KnowledgeBase.UnitTests.Clients;

// Unit test cho KbClientFactory: chọn mock hay HTTP client theo biến môi trường (AC2).
public class KbClientFactoryTests
{
    // Dữ liệu cho U08 (hợp lệ): (biến môi trường, kiểu client mong đợi, BaseAddress mong đợi hoặc null).
    public static TheoryData<Dictionary<string, string?>, Type, string?> ValidEnvironments => new()
    {
        { new(), typeof(MockKbClient), null },                                         // không đặt gì → mock
        { new() { ["KB_CLIENT"] = "" }, typeof(MockKbClient), null },                  // rỗng → coi như không đặt
        { new() { ["KB_CLIENT"] = "mock" }, typeof(MockKbClient), null },
        { new() { ["KB_CLIENT"] = "MOCK" }, typeof(MockKbClient), null },              // không phân biệt hoa thường
        { new() { ["KB_CLIENT"] = "http", ["KB_API_URL"] = "https://kb.example.test" },
          typeof(HttpKbClient), "https://kb.example.test/" },                          // tự thêm "/" cuối
    };

    // U08 — Bảng quyết định chọn client theo KB_CLIENT × KB_API_URL (AC2). Hàm này: các luật hợp lệ.
    // Loại: Normal — kèm Boundary (KB_CLIENT rỗng).
    // [DT] [EG] biến môi trường được đặt nhưng rỗng (giống U10 Tuần 2); URL thiếu "/" cuối làm
    //      HttpClient ghép sai đường dẫn ("https://kb.example.testsearch").
    [Theory]
    [MemberData(nameof(ValidEnvironments))]
    public void U08_Create_PicksClientFromEnvironment(Dictionary<string, string?> environment,
                                                      Type expectedType, string? expectedBaseAddress)
    {
        var client = KbClientFactory.Create(environment);

        Assert.IsType(expectedType, client);
        Assert.Equal(expectedBaseAddress, (client as HttpKbClient)?.BaseAddress?.ToString());
    }

    // Dữ liệu cho U08 (không hợp lệ): cấu hình sai → KbConfigurationException.
    public static TheoryData<Dictionary<string, string?>> InvalidEnvironments => new()
    {
        new() { ["KB_CLIENT"] = "http" },                                // http mà thiếu URL
        new() { ["KB_CLIENT"] = "http", ["KB_API_URL"] = "not-a-url" },  // URL sai định dạng
        new() { ["KB_CLIENT"] = "ftp" },                                 // giá trị không hỗ trợ
    };

    // U08 — Hàm này: các luật không hợp lệ. Loại: Abnormal. [DT] [EG]
    [Theory]
    [MemberData(nameof(InvalidEnvironments))]
    public void U08_Create_InvalidConfiguration_Throws(Dictionary<string, string?> environment)
    {
        Assert.Throws<KbConfigurationException>(() => KbClientFactory.Create(environment));
    }
}
