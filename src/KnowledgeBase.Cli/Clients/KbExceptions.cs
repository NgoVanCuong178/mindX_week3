namespace KnowledgeBase.Cli.Clients;

// Không có tài liệu nào mang id được yêu cầu. Exit code 1.
public class KbDocumentNotFoundException(string docId) : Exception($"Document '{docId}' not found")
{
    public string DocId { get; } = docId;
}

// Không kết nối được KB API, KB API báo lỗi, hoặc trả về dữ liệu không đọc được. Exit code 3.
public class KbApiException(string message, Exception? innerException = null)
    : Exception(message, innerException);

// Cấu hình KB_CLIENT / KB_API_URL bị thiếu hoặc sai. Exit code 1.
public class KbConfigurationException(string message) : Exception(message);
