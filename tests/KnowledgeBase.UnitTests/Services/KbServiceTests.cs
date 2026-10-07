using KnowledgeBase.Cli.Services;
using KnowledgeBase.UnitTests.Fakes;

namespace KnowledgeBase.UnitTests.Services;

// Unit test cho KbService: quy tắc validate và chuẩn hoá input, dùng chung cho cả mock lẫn HTTP client.
// Dùng RecordingKbClient (bản giả) để kiểm tra service truyền gì xuống client, và không gọi client khi input sai.
public class KbServiceTests
{
    private readonly RecordingKbClient _client = new();
    private readonly KbService _service;

    public KbServiceTests()
    {
        _service = new KbService(_client);
    }

    // U01 — search: query là bắt buộc.
    // Loại: Abnormal — kèm Boundary ("" là chuỗi 0 ký tự).
    // [EP] Cả 3 thuộc miền "query không có nội dung". [EG] null → NullReferenceException;
    //      "   " → bắt lỗi dùng IsNullOrEmpty thay vì IsNullOrWhiteSpace.
    // Không được gọi client khi input sai.
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task U01_Search_QueryMissingOrBlank_Throws(string? query)
    {
        await Assert.ThrowsAsync<ValidationException>(() => _service.SearchAsync(query, 5));
        Assert.Equal(0, _client.CallCount);
    }

    // U02 — search: --top-k phải trong khoảng 1–50.
    // Loại: Boundary. [BVA] Hàm này: min - 1 (0) và max + 1 (51) → lỗi, không gọi client.
    [Theory]
    [InlineData(0)]
    [InlineData(51)]
    public async Task U02_Search_TopKOutOfRange_Throws(int topK)
    {
        await Assert.ThrowsAsync<ValidationException>(() => _service.SearchAsync("response", topK));
        Assert.Equal(0, _client.CallCount);
    }

    // U02 — Hàm này: min (1) và max (50) → hợp lệ; client nhận đúng topK và query đã được trim.
    [Theory]
    [InlineData(1)]
    [InlineData(50)]
    public async Task U02_Search_TopKAtBoundary_IsPassedToClient(int topK)
    {
        await _service.SearchAsync("  response  ", topK);

        Assert.Equal(new("response", topK), _client.LastQuery);
    }

    // U03 — list: --limit phải trong khoảng 1–100.
    // Loại: Boundary. [BVA] Hàm này: min - 1 (0) và max + 1 (101) → lỗi, không gọi client.
    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public async Task U03_List_LimitOutOfRange_Throws(int limit)
    {
        await Assert.ThrowsAsync<ValidationException>(() => _service.ListAsync("/templates/email", limit));
        Assert.Equal(0, _client.CallCount);
    }

    // U03 — Hàm này: min (1) và max (100) → hợp lệ, truyền nguyên vẹn xuống client.
    [Theory]
    [InlineData(1)]
    [InlineData(100)]
    public async Task U03_List_LimitAtBoundary_IsPassedToClient(int limit)
    {
        await _service.ListAsync("/templates/email", limit);

        Assert.Equal(("/templates/email", limit), _client.LastList);
    }

    // U04 — list: --node bắt buộc và phải bắt đầu bằng "/".
    // Loại: Abnormal. [EP] Miền "không có node" (null, "") và miền "node sai định dạng".
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("templates/email")]
    public async Task U04_List_InvalidNodePath_Throws(string? nodePath)
    {
        await Assert.ThrowsAsync<ValidationException>(() => _service.ListAsync(nodePath, 10));
        Assert.Equal(0, _client.CallCount);
    }

    // U05 — retrieve: docId bắt buộc.
    // Loại: Abnormal. [EP] [EG] null và chuỗi chỉ có khoảng trắng.
    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public async Task U05_Retrieve_DocIdMissingOrBlank_Throws(string? docId)
    {
        await Assert.ThrowsAsync<ValidationException>(() => _service.RetrieveAsync(docId));
        Assert.Equal(0, _client.CallCount);
    }

    // U06 — add: lấy title theo thứ tự ưu tiên --title → dòng "# heading" đầu tiên → tên file (bỏ .md).
    // Loại: Normal. [DT] Bảng quyết định 3 luật về nguồn của title.
    // Đồng thời kiểm tra: nội dung và node được truyền nguyên vẹn; tags được chuẩn hoá
    // (trim → chữ thường → bỏ rỗng → bỏ trùng, giống Tuần 2).
    [Theory]
    [InlineData("Custom Title", "# Heading\n\nBody", "new-template.md", "Custom Title")]
    [InlineData(null, "Intro line\n# SMS Reminder\n\nBody", "new-template.md", "SMS Reminder")]
    [InlineData(null, "No heading here", "new-template.md", "new-template")]
    public async Task U06_Add_ResolvesTitleAndNormalizesTags(string? title, string content,
                                                              string sourceName, string expectedTitle)
    {
        var added = await _service.AddAsync(content, sourceName, "/templates/sms", [" SMS ", "sms", "   "], title);

        Assert.Equal(expectedTitle, _client.LastAdded!.Title);
        Assert.Equal(content, _client.LastAdded.Content);
        Assert.Equal("/templates/sms", _client.LastAdded.NodePath);
        Assert.Equal(["sms"], _client.LastAdded.Tags);
        Assert.Equal("doc-100", added.Id);
    }

    // U07 — add: nội dung file rỗng, hoặc node sai định dạng → lỗi, không gọi client.
    // Loại: Abnormal — kèm Boundary (file 0 ký tự). [EG] file rỗng hay gặp khi tạo file mới quên lưu.
    [Theory]
    [InlineData("", "/templates/sms")]
    [InlineData("   ", "/templates/sms")]
    [InlineData("# Title", "templates/sms")]
    public async Task U07_Add_EmptyContentOrInvalidPath_Throws(string content, string nodePath)
    {
        await Assert.ThrowsAsync<ValidationException>(
            () => _service.AddAsync(content, "new-template.md", nodePath, ["sms"]));
        Assert.Equal(0, _client.CallCount);
    }
}
