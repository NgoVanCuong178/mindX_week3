using System.CommandLine;
using KnowledgeBase.Cli.Clients;
using KnowledgeBase.Cli.Models;
using KnowledgeBase.Cli.Services;

namespace KnowledgeBase.Cli;

public static class CliApp
{
    // Điểm vào của mọi lệnh `kb`: parse lệnh, gọi KbService, in kết quả, chuyển lỗi thành exit code.
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

        // Tạo service khi lệnh thật sự chạy (không tạo lúc khai báo lệnh), để `kb --help`
        // vẫn chạy được kể cả khi cấu hình KB_CLIENT / KB_API_URL đang sai.
        KbService CreateService(ParseResult result)
            => new(KbClientFactory.Create(environment, result.GetValue(verboseOption) ? error : null));

        // kb search <query> [--top-k]
        var searchQuery = new Argument<string>("query") { Description = "Words to search for" };
        var searchTopK = new Option<int>("--top-k")
        {
            Description = $"Maximum number of results (1-{KbService.MaxTopK})",
            DefaultValueFactory = _ => 5,
        };
        var search = new Command("search", "Search documents in the knowledge base") { searchQuery, searchTopK };
        search.SetAction(async (result, _) =>
        {
            var results = await CreateService(result).SearchAsync(result.GetValue(searchQuery), result.GetValue(searchTopK));
            WriteTable(output, results.Select(r => (r.Document, r.MatchType.ToString().ToLowerInvariant())).ToList(),
                       showMatch: true);
            return 0;
        });

        // kb list --node <path> [--limit]
        var listNode = new Option<string>("--node")
        {
            Description = "Node path, for example /templates/email",
            Required = true,
        };
        var listLimit = new Option<int>("--limit")
        {
            Description = $"Maximum number of documents (1-{KbService.MaxLimit})",
            DefaultValueFactory = _ => 10,
        };
        var list = new Command("list", "List documents in a node") { listNode, listLimit };
        list.SetAction(async (result, _) =>
        {
            var documents = await CreateService(result).ListAsync(result.GetValue(listNode), result.GetValue(listLimit));
            WriteTable(output, documents.Select(d => (d, "")).ToList(), showMatch: false);
            return 0;
        });

        // kb retrieve <docId>
        var retrieveId = new Argument<string>("docId") { Description = "Document id, for example doc-001" };
        var retrieve = new Command("retrieve", "Show a document") { retrieveId };
        retrieve.SetAction(async (result, _) =>
        {
            var document = await CreateService(result).RetrieveAsync(result.GetValue(retrieveId));
            output.WriteLine($"ID:    {document.Id}");
            output.WriteLine($"Title: {document.Title}");
            output.WriteLine($"Node:  {document.NodePath}");
            output.WriteLine($"Tags:  {string.Join(", ", document.Tags)}");
            output.WriteLine();
            output.WriteLine(document.Content);
            return 0;
        });

        // kb add --file <file> --path <node> [--tags ...] [--title]
        var addFile = new Option<string>("--file") { Description = "Markdown file to add", Required = true };
        var addPath = new Option<string>("--path") { Description = "Node path to add the document to", Required = true };
        var addTags = new Option<string[]>("--tags")
        {
            Description = "Tags for the document",
            AllowMultipleArgumentsPerToken = true,
        };
        var addTitle = new Option<string?>("--title")
        {
            Description = "Document title (default: first '# heading' of the file, then the file name)",
        };
        var add = new Command("add", "Add a document from a Markdown file") { addFile, addPath, addTags, addTitle };
        add.SetAction(async (result, _) =>
        {
            var file = result.GetValue(addFile)!;
            if (!File.Exists(file))
            {
                throw new ValidationException($"File '{file}' not found.");
            }

            var document = await CreateService(result).AddAsync(await File.ReadAllTextAsync(file), Path.GetFileName(file),
                                                                 result.GetValue(addPath), result.GetValue(addTags),
                                                                 result.GetValue(addTitle));
            output.WriteLine($"Added document {document.Id}");
            return 0;
        });

        var root = new RootCommand("Knowledge Base CLI: search, list, retrieve and add documents")
        {
            verboseOption, search, list, retrieve, add,
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
        catch (Exception ex) when (ex is ValidationException or KbDocumentNotFoundException or KbConfigurationException)
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

    // In bảng tài liệu dùng chung cho `search` (có cột MATCH) và `list`.
    private static void WriteTable(TextWriter output, IReadOnlyList<(KbDocumentSummary Document, string Match)> rows,
                                   bool showMatch)
    {
        if (rows.Count == 0)
        {
            output.WriteLine("No documents found.");
            return;
        }

        var matchHeader = showMatch ? $"{"MATCH",-9}" : "";
        output.WriteLine($"{"ID",-10}{matchHeader}{"NODE",-20}TITLE");
        foreach (var (document, match) in rows)
        {
            var matchColumn = showMatch ? $"{match,-9}" : "";
            output.WriteLine($"{document.Id,-10}{matchColumn}{document.NodePath,-20}{document.Title}");
        }
    }
}
