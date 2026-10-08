namespace KnowledgeBase.Cli.Clients;

// Đổi nội dung HTML (bài viết của Zendesk) thành chữ thường để `kb retrieve` in ra terminal:
//   <p>, <h1>..<h6>, <div>  → mỗi khối một đoạn, cách nhau một dòng trống
//   <li>                    → dòng bắt đầu bằng "- "
//   <br>                    → xuống dòng
//   thẻ khác (a, img, b...) → bỏ thẻ, giữ chữ bên trong
//   &amp; &lt; &nbsp; ...   → ký tự tương ứng
// Không cố dựng lại layout (bảng, ảnh): chỉ cần đọc được nội dung.
public static class HtmlText
{
    public static string ToPlainText(string html) => throw new NotImplementedException();
}
