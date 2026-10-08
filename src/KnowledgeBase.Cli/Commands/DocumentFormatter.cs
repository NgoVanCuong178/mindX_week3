using KnowledgeBase.Cli.Models;

namespace KnowledgeBase.Cli.Commands;

// Toàn bộ phần in kết quả ra màn hình của các lệnh `kb`, tách khỏi logic của từng lệnh.
internal static class DocumentFormatter
{
    public const string NoDocumentsMessage = "No documents found.";

    // `kb search`: bảng có cột MATCH (khớp ở title / tag / content).
    public static void WriteSearchResults(TextWriter output, IReadOnlyList<SearchResult> results)
        => WriteTable(output, results.Select(r => (r.Document, r.MatchType.ToString().ToLowerInvariant())).ToList(),
                      showMatch: true);

    // `kb list`: bảng không có cột MATCH.
    public static void WriteSummaries(TextWriter output, IReadOnlyList<KbDocumentSummary> documents)
        => WriteTable(output, documents.Select(d => (d, "")).ToList(), showMatch: false);

    // `kb retrieve`: thông tin tài liệu, một dòng trống, rồi toàn bộ nội dung.
    public static void WriteDocument(TextWriter output, KbDocument document)
    {
        output.WriteLine($"ID:    {document.Id}");
        output.WriteLine($"Title: {document.Title}");
        output.WriteLine($"Node:  {document.NodePath}");
        output.WriteLine($"Tags:  {string.Join(", ", document.Tags)}");
        output.WriteLine();
        output.WriteLine(document.Content);
    }

    // In bảng tài liệu dùng chung cho `search` (có cột MATCH) và `list`.
    private static void WriteTable(TextWriter output, IReadOnlyList<(KbDocumentSummary Document, string Match)> rows,
                                   bool showMatch)
    {
        if (rows.Count == 0)
        {
            output.WriteLine(NoDocumentsMessage);
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
