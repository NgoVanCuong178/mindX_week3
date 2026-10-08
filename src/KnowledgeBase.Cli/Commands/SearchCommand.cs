using System.CommandLine;
using KnowledgeBase.Cli.Services;

namespace KnowledgeBase.Cli.Commands;

// kb search <query> [--top-k]
internal static class SearchCommand
{
    public static Command Create(CommandContext context)
    {
        var query = new Argument<string>("query") { Description = "Words to search for" };
        var topK = new Option<int>("--top-k")
        {
            Description = $"Maximum number of results (1-{KbService.MaxTopK})",
            DefaultValueFactory = _ => 5,
        };

        var command = new Command("search", "Search documents in the knowledge base") { query, topK };
        command.SetAction(async (result, _) =>
        {
            var results = await context.CreateService(result).SearchAsync(result.GetValue(query), result.GetValue(topK));
            DocumentFormatter.WriteSearchResults(context.Output, results);
            return 0;
        });
        return command;
    }
}
