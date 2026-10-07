namespace KnowledgeBase.Cli.Models;

// Từ khoá được tìm thấy ở đâu. Kết quả xếp theo thứ tự: khớp title trước, rồi tag, rồi content.
public enum MatchKind { Title, Tag, Content }

public record SearchResult(KbDocumentSummary Document, MatchKind MatchType);

public record KbQuery(string Query, int TopK);
