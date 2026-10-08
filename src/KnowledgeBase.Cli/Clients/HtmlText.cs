using System.Net;
using System.Text.RegularExpressions;

namespace KnowledgeBase.Cli.Clients;

// Đổi nội dung HTML (bài viết của Zendesk) thành chữ thường để `kb retrieve` in ra terminal:
//   <p>, <h1>..<h6>, <div>  → mỗi khối một đoạn, cách nhau một dòng trống
//   <li>                    → dòng bắt đầu bằng "- "
//   <br>                    → xuống dòng
//   thẻ khác (a, img, b...) → bỏ thẻ, giữ chữ bên trong
//   &amp; &lt; &nbsp; ...   → ký tự tương ứng
// Không cố dựng lại layout (bảng, ảnh): chỉ cần đọc được nội dung.
public static partial class HtmlText
{
    public static string ToPlainText(string html)
    {
        var text = LineBreak().Replace(html, "\n");
        text = ListItemStart().Replace(text, "- ");
        text = ListItemEnd().Replace(text, "\n");
        text = BlockEnd().Replace(text, "\n\n");
        text = AnyTag().Replace(text, "");                                   // bỏ mọi thẻ còn lại, giữ chữ

        // Giải mã entity SAU khi bỏ thẻ, để "&lt;3" thành chữ "<3" chứ không bị coi là thẻ.
        text = WebUtility.HtmlDecode(text).Replace(' ', ' ');           // &nbsp; → dấu cách thường

        // Bỏ khoảng trắng đầu/cuối mỗi dòng (thụt lề của HTML), rồi gộp nhiều dòng trống thành một.
        text = string.Join("\n", text.Split('\n').Select(line => line.Trim()));
        return ExtraBlankLines().Replace(text, "\n\n").Trim();
    }

    [GeneratedRegex(@"<br\s*/?>", RegexOptions.IgnoreCase)]
    private static partial Regex LineBreak();

    [GeneratedRegex(@"<li\b[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex ListItemStart();

    [GeneratedRegex(@"</li\s*>", RegexOptions.IgnoreCase)]
    private static partial Regex ListItemEnd();

    [GeneratedRegex(@"</(p|div|h[1-6]|ul|ol|table|tr|blockquote|pre)\s*>", RegexOptions.IgnoreCase)]
    private static partial Regex BlockEnd();

    [GeneratedRegex(@"<[^>]*>")]
    private static partial Regex AnyTag();

    [GeneratedRegex(@"\n{3,}")]
    private static partial Regex ExtraBlankLines();
}
