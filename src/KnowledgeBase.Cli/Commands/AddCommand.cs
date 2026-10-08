using System.CommandLine;
using KnowledgeBase.Cli.Services;

namespace KnowledgeBase.Cli.Commands;

// kb add --file <file> --path <node> [--tags ...] [--title]
internal static class AddCommand
{
    public static Command Create(CommandContext context)
    {
        var file = new Option<string>("--file") { Description = "Markdown file to add", Required = true };
        var path = new Option<string>("--path") { Description = "Node path to add the document to", Required = true };
        var tags = new Option<string[]>("--tags")
        {
            Description = "Tags for the document",
            AllowMultipleArgumentsPerToken = true,
        };
        var title = new Option<string?>("--title")
        {
            Description = "Document title (default: first '# heading' of the file, then the file name)",
        };

        var command = new Command("add", "Add a document from a Markdown file") { file, path, tags, title };
        command.SetAction(async (result, _) =>
        {
            // Đọc file ở tầng lệnh; KbService chỉ nhận nội dung và tên file.
            var filePath = result.GetValue(file)!;
            if (!File.Exists(filePath))
            {
                throw new ValidationException($"File '{filePath}' not found.");
            }

            var document = await context.CreateService(result).AddAsync(
                await File.ReadAllTextAsync(filePath), Path.GetFileName(filePath),
                result.GetValue(path), result.GetValue(tags), result.GetValue(title));
            context.Output.WriteLine($"Added document {document.Id}");
            return 0;
        });
        return command;
    }
}
