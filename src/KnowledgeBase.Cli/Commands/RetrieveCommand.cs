using System.CommandLine;

namespace KnowledgeBase.Cli.Commands;

// kb retrieve <docId>
internal static class RetrieveCommand
{
    public static Command Create(CommandContext context)
    {
        var docId = new Argument<string>("docId") { Description = "Document id, for example doc-001" };

        var command = new Command("retrieve", "Show a document") { docId };
        command.SetAction(async (result, _) =>
        {
            var document = await context.CreateService(result).RetrieveAsync(result.GetValue(docId));
            DocumentFormatter.WriteDocument(context.Output, document);
            return 0;
        });
        return command;
    }
}
