using System.Collections;
using KnowledgeBase.Cli;

// Đọc biến môi trường thật của tiến trình (KB_CLIENT, KB_API_URL...) rồi truyền vào CliApp.
// Không phân biệt hoa thường tên biến, giống cách Windows xử lý biến môi trường.
var environment = Environment.GetEnvironmentVariables()
    .Cast<DictionaryEntry>() // chuyển sang dạng DictionaryEntry cặp key value để dùng được hàm LINQ ToDictionary
    .ToDictionary(entry => (string)entry.Key, entry => (string?)entry.Value, StringComparer.OrdinalIgnoreCase);
// Chờ run async chạy xong
return await CliApp.RunAsync(args, Console.Out, Console.Error, environment);
