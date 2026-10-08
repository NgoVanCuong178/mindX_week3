using KnowledgeBase.Cli.Clients;

namespace KnowledgeBase.UnitTests.Clients;

// Unit test cho HtmlText: bài viết Zendesk là HTML, nhưng `kb retrieve` in ra terminal nên cần chữ thường.
public class HtmlTextTests
{
    // Z08 — Các thẻ HTML hay gặp trong bài viết Help Center được đổi thành chữ dễ đọc.
    // Loại: Normal, Boundary (chuỗi rỗng, không có thẻ). [EP] mỗi loại thẻ là một miền.
    // [EG] entity (&amp;, &nbsp;), ảnh không có chữ, nhiều dòng trống liên tiếp.
    [Theory]
    [InlineData("<h2>Question</h2>\n<p>How long?</p>", "Question\n\nHow long?")]          // heading và đoạn văn
    [InlineData("<ul><li>One</li><li>Two</li></ul>", "- One\n- Two")]                    // danh sách
    [InlineData("<p>Line 1<br>Line 2<br/>Line 3</p>", "Line 1\nLine 2\nLine 3")]        // xuống dòng
    [InlineData("<p>Tom &amp; Jerry&nbsp;&lt;3</p>", "Tom & Jerry <3")]                  // entity
    [InlineData("<p>See <a href=\"https://x.test\">the guide</a>.</p><p><img src=\"a.png\"></p>", "See the guide.")] // link, ảnh
    [InlineData("<p>A</p>\n\n\n<p>B</p>", "A\n\nB")]                                     // gộp dòng trống thừa
    [InlineData("<h2>Answer </h2>", "Answer")]                                      // khoảng trắng không ngắt (dữ liệu thật)
    [InlineData("Hello", "Hello")]                                                       // không có thẻ
    [InlineData("", "")]                                                                 // rỗng
    public void Z08_ToPlainText_ConvertsCommonHtml(string html, string expected)
    {
        Assert.Equal(expected, HtmlText.ToPlainText(html));
    }
}
