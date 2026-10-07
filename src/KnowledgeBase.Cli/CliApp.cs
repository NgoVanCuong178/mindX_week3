namespace KnowledgeBase.Cli;

public static class CliApp
{
    // Điểm vào của mọi lệnh `kb`: parse lệnh, gọi KbService, in kết quả, chuyển lỗi thành exit code.
    // Biến môi trường được truyền vào (thay vì tự đọc ở đây) để test đổi được giữa mock và HTTP
    // mà không đụng tới biến môi trường thật của máy.
    public static Task<int> RunAsync(string[] args, TextWriter output, TextWriter error,
                                     IReadOnlyDictionary<string, string?> environment)
        => throw new NotImplementedException();
}
