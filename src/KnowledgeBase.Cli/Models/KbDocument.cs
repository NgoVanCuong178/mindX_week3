namespace KnowledgeBase.Cli.Models;

// record là kiểu dữ liệu gọn của C# => tự tạo 5 property chỉ đọc, constructor nhận 5 giá trị.
// Một tài liệu đầy đủ trong kho tri thức, như kết quả của `kb retrieve`.
public record KbDocument(string Id, string Title, string Content, string NodePath, IReadOnlyList<string> Tags)
{
    // Bản rút gọn (bỏ nội dung và tag) dùng cho kết quả search và list.
    public KbDocumentSummary ToSummary() => new(Id, Title, NodePath);
}

// Dạng rút gọn của tài liệu (không có nội dung), dùng trong kết quả search và list.
public record KbDocumentSummary(string Id, string Title, string NodePath);

// Tài liệu chưa được lưu (chưa có id), là dữ liệu mà `kb add` gửi đi.
public record NewKbDocument(string Title, string Content, string NodePath, IReadOnlyList<string> Tags);
