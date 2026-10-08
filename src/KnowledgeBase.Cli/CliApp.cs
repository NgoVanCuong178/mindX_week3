using System.CommandLine;
using KnowledgeBase.Cli.Clients;
using KnowledgeBase.Cli.Commands;
using KnowledgeBase.Cli.Services;

namespace KnowledgeBase.Cli;

public static class CliApp
{
    // Điểm vào của mọi lệnh `kb`: dựng 4 lệnh, parse, chạy, và chuyển lỗi thành exit code.
    // Biến môi trường được truyền vào (thay vì tự đọc ở đây) để test đổi được giữa mock và HTTP
    // mà không đụng tới biến môi trường thật của máy.
    public static async Task<int> RunAsync(string[] args, TextWriter output, TextWriter error,
                                           IReadOnlyDictionary<string, string?> environment)
    {
        var verboseOption = new Option<bool>("--verbose") // dùng với mọi lệnh
        {
            Description = "Log every KB API request to stderr",
            Recursive = true,
        };

        var context = new CommandContext(output, result =>
            new KbService(KbClientFactory.Create(environment, result.GetValue(verboseOption) ? error : null)));

        var root = new RootCommand("Knowledge Base CLI: search, list, retrieve and add documents")
        {
            verboseOption,
            SearchCommand.Create(context),
            ListCommand.Create(context),
            RetrieveCommand.Create(context),
            AddCommand.Create(context),
        };

        // Lỗi cú pháp lệnh được báo ở đây, không qua InvokeAsync() — vì InvokeAsync() còn in cả phần
        // help ra stdout (giống Tuần 2).
        var parseResult = root.Parse(args);
        if (parseResult.Errors.Count > 0)
        {
            foreach (var parseError in parseResult.Errors)
            {
                error.WriteLine(parseError.Message);
            }
            return 1;
        }

        // Chỗ DUY NHẤT chuyển exception thành exit code.
        try
        {
            return await parseResult.InvokeAsync(new InvocationConfiguration
            {
                Output = output,
                Error = error,
                EnableDefaultExceptionHandler = false,
            });
        }
        catch (Exception ex) when (ex is ValidationException or KbDocumentNotFoundException or KbConfigurationException
                                      or KbOperationNotSupportedException)
        {
            error.WriteLine(ex.Message);
            return 1;
        }
        catch (KbApiException ex)
        {
            error.WriteLine(ex.Message);
            return 3;
        }
    }
}
