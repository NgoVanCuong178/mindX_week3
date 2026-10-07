using KnowledgeBase.Cli.Models;

namespace KnowledgeBase.Cli.Clients;

// "Hợp đồng" 4 thao tác với kho tri thức. MockKbClient giữ tài liệu trong bộ nhớ;
// HttpKbClient gọi KB API qua mạng. Hai client bắt buộc có cùng hành vi.
public interface IKbClient
{
    // hàm tìm kiếm bất đồng bộ
    Task<IReadOnlyList<SearchResult>> SearchAsync(KbQuery query);
    // Hàm liệt kê tài liệu trong node, lấy tối đa limit tài liệu
    Task<IReadOnlyList<KbDocumentSummary>> ListAsync(string nodePath, int limit);

    //Lấy một tài liệu khớp với id ==> Không có tài liệu nào mang id này → ném KbDocumentNotFoundException.
    Task<KbDocument> RetrieveAsync(string docId);

    // Trả về tài liệu đã lưu, kèm id mới được cấp.
    Task<KbDocument> AddAsync(NewKbDocument document);
}
