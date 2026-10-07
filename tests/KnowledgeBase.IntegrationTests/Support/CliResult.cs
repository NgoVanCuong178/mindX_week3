namespace KnowledgeBase.IntegrationTests.Support;

// Kết quả một lần chạy CLI: exit code, nội dung stdout và nội dung stderr (lấy từ Tuần 2).
public record CliResult(int ExitCode, string Output, string Error);
