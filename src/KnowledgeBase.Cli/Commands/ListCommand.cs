using System.CommandLine;
using KnowledgeBase.Cli.Services;

namespace KnowledgeBase.Cli.Commands;

// kb list --node <path> [--limit]
internal static class ListCommand
{
    public static Command Create(CommandContext context)
    {
        var node = new Option<string>("--node")
        {
            Description = "Node path, for example /templates/email",
            Required = true,
        };
        var limit = new Option<int>("--limit")
        {
            Description = $"Maximum number of documents (1-{KbService.MaxLimit})",
            DefaultValueFactory = _ => 10,
        };

        var command = new Command("list", "List documents in a node") { node, limit };
        command.SetAction(async (result, _) =>
        {
            var documents = await context.CreateService(result).ListAsync(result.GetValue(node), result.GetValue(limit));
            DocumentFormatter.WriteSummaries(context.Output, documents);
            return 0;
        });
        return command;
    }
}
