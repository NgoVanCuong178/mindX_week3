using System.CommandLine;
using KnowledgeBase.Cli.Services;

namespace KnowledgeBase.Cli.Commands;

// Những thứ mọi lệnh `kb` đều cần: nơi in kết quả (stdout) và cách tạo KbService cho lần chạy này.
// CliApp tạo một CommandContext rồi truyền cho từng lệnh, để các lệnh không phải biết
// về biến môi trường hay về việc chọn mock / HTTP client.
internal sealed class CommandContext(TextWriter output, Func<ParseResult, KbService> createService)
{
    public TextWriter Output { get; } = output;

    // Tạo service khi lệnh thật sự chạy (không tạo lúc khai báo lệnh), để `kb --help`
    // vẫn chạy được kể cả khi cấu hình KB_CLIENT / KB_API_URL đang sai.
    public KbService CreateService(ParseResult result) => createService(result);
}
