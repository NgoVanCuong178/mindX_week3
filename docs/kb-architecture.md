# Kiến trúc: Knowledge Base CLI ↔ KB API

Tài liệu này mô tả các thành phần của `kb`, đường đi của một lệnh từ lúc người dùng gõ tới khi có kết quả, API contract giữa CLI và KB API, cách tích hợp với Zendesk Help Center, và lý do đằng sau các quyết định thiết kế. Hướng dẫn cài đặt và sử dụng nằm ở [README](../README.md).

## 1. Tổng quan

```
                  ┌──────────────────────────────────────────────┐
  người dùng ───▶ │ CLI commands (Commands/)                     │
  kb search ...   │  kb search · kb list · kb retrieve · kb add  │
                  │  đọc tham số, gọi service, in kết quả         │
                  └──────────────────────┬───────────────────────┘
                                         ▼
                  ┌──────────────────────────────────────────────┐
                  │ KbService (Services/)                        │
                  │  validate và chuẩn hoá input                  │
                  └──────────────────────┬───────────────────────┘
                                         ▼
                  ┌──────────────────────────────────────────────┐
                  │ IKbClient (interface)                        │
                  │  SearchAsync · ListAsync · RetrieveAsync ·    │
                  │  AddAsync                                    │
                  └──────┬──────────────────┬──────────────────┬─┘
     KB_CLIENT=mock      │   KB_CLIENT=http │  KB_CLIENT=zendesk│
     (mặc định)          ▼                  ▼                  ▼
          ┌────────────────┐  ┌──────────────────┐  ┌──────────────────┐
          │ MockKbClient   │  │ HttpKbClient     │  │ ZendeskKbClient  │
          │ 3 tài liệu     │  │ POST + JSON      │  │ GET, chỉ đọc     │
          │ trong bộ nhớ   │  │ theo contract    │  │ HTML → chữ       │
          └───────┬────────┘  └────────┬─────────┘  └────────┬─────────┘
                  │                    └──────────┬──────────┘
            KbMatcher               KbHttpSender (dùng chung):
            (cột MATCH, dùng        token, log, timeout, đổi lỗi
             chung với Zendesk)               │
                                   ┌──────────┴───────────┐
                                   ▼                      ▼
                        ┌────────────────────┐ ┌────────────────────────┐
                        │ KB API             │ │ Zendesk Help Center    │
                        │ tools/KnowledgeBase│ │ support.zendesk.com    │
                        │ .Api, hoặc KB API  │ │ (KB thật, bên ngoài,   │
                        │ thật khi được cấp  │ │  bên thứ ba vận hành)  │
                        └────────────────────┘ └────────────────────────┘
```

`KbClientFactory` đọc biến môi trường và quyết định tạo client nào. Các lớp phía trên `IKbClient` không biết đang dùng client nào. Khi thêm Zendesk, các lệnh và `KbService` **không phải sửa dòng nào**.

So với Tuần 2: lớp `Storage` (đọc/ghi file JSON) được thay bằng lớp `Clients`. Cách chia lớp còn lại giữ nguyên: lệnh → service → nơi lưu dữ liệu.

## 2. Thành phần

| Thành phần | File | Trách nhiệm | Không làm |
|---|---|---|---|
| `Program` | `Program.cs` | Đọc biến môi trường thật, gọi `CliApp` | Không có logic |
| `CliApp` | `CliApp.cs` | Ghép 4 lệnh và `--verbose`; báo lỗi cú pháp; **chỗ duy nhất** đổi exception thành exit code | Không validate, không in kết quả |
| Commands | `Commands/*Command.cs` | Khai báo tham số của từng lệnh; `add` đọc file Markdown | Không validate giá trị |
| `DocumentFormatter` | `Commands/DocumentFormatter.cs` | In bảng kết quả và chi tiết tài liệu | |
| `KbService` | `Services/KbService.cs` | Validate (`--top-k` 1–50, `--limit` 1–100, node bắt đầu bằng `/`, file không rỗng); chuẩn hoá (bỏ khoảng trắng, tag chữ thường, chọn title) | Không biết về dòng lệnh hay HTTP |
| `IKbClient` | `Clients/IKbClient.cs` | Hợp đồng 4 thao tác mà mọi client phải tuân theo | |
| `MockKbClient` | `Clients/MockKbClient.cs` | Thực hiện 4 thao tác trên danh sách trong bộ nhớ | |
| `HttpKbClient` | `Clients/HttpKbClient.cs` | Đổi mỗi thao tác thành `POST` JSON theo contract; kiểm tra field bắt buộc của response | Không validate input; không tự gửi request (dùng `KbHttpSender`) |
| `ZendeskKbClient` | `Clients/ZendeskKbClient.cs` | Đổi mỗi thao tác thành `GET` tới Zendesk Help Center; ánh xạ bài viết sang `KbDocument`; kiểm tra field bắt buộc; báo `add` không được hỗ trợ | Không ghi dữ liệu; không tự gửi request (dùng `KbHttpSender`) |
| `KbHttpSender` | `Clients/KbHttpSender.cs` | Phần dùng chung của 2 client HTTP: gửi request (GET/POST), token, log `--verbose`, timeout, đổi lỗi mạng/HTTP/JSON thành exception kèm lý do server đưa ra, báo response `null` hoặc thiếu field | Không biết contract của KB nào |
| `KbMatcher` | `Clients/KbMatcher.cs` | Quy tắc cột MATCH (title → tag → content), dùng chung cho mock và Zendesk | |
| `HtmlText` | `Clients/HtmlText.cs` | Đổi HTML của bài viết Zendesk sang chữ thường | |
| `KbClientFactory` | `Clients/KbClientFactory.cs` | Chọn client theo `KB_CLIENT`, kiểm tra `KB_API_URL`, đặt timeout 10 giây, đọc `KB_ZENDESK_LOCALE` | |
| `KbApiContract` | `Clients/KbApiContract.cs` | Các kiểu request/response và quy ước JSON, dùng chung cho client và server | |
| KB API | `tools/KnowledgeBase.Api/` | Server HTTP theo contract; validate request (`KbRequestValidator`, sai → 400); lưu file JSON; token tuỳ chọn; mọi lỗi trả `{"error": ...}` | |

## 3. Luồng dữ liệu

### Ví dụ: `kb search "response" --top-k 3` với `KB_CLIENT=http`

```
1. Program        đọc biến môi trường → CliApp.RunAsync(args, stdout, stderr, env)
2. CliApp         System.CommandLine parse: lệnh search, query="response", topK=3
                  (sai cú pháp → in lỗi ra stderr, exit 1, dừng)
3. SearchCommand  context.CreateService() → KbClientFactory.Create(env) → HttpKbClient
4. KbService      kiểm tra query không rỗng, 1 ≤ topK ≤ 50 → KbQuery("response", 3)
                  (sai → ValidationException)
5. HttpKbClient   POST {KB_API_URL}/search   body {"query":"response","topK":3}
                  header Authorization: Bearer <KB_API_TOKEN>   (nếu có)
6. KB API         tìm trong title, tag, content → {"results":[...]}
7. HttpKbClient   đọc JSON → List<SearchResult>
                  (mất kết nối / timeout / HTTP lỗi / JSON hỏng → KbApiException)
8. Formatter      in bảng ID · MATCH · NODE · TITLE ra stdout → exit 0
```

Với `KB_CLIENT=mock`, bước 5–7 được thay bằng `MockKbClient` tìm trong bộ nhớ. Với `KB_CLIENT=zendesk`, bước 5 là `GET {KB_API_URL}/api/v2/help_center/articles/search.json?query=response&per_page=3&locale=en-us`, và bước 7 ánh xạ bài viết Zendesk sang `SearchResult` (xem [mục 6](#6-tích-hợp-zendesk-help-center)). Mọi bước khác giống hệt.

### Lỗi đi đâu

| Nơi phát sinh | Exception | `CliApp` in ra stderr | Exit |
|---|---|---|---|
| Parse lệnh | (lỗi cú pháp của System.CommandLine) | `Option '--node' is required.` | 1 |
| `KbService` | `ValidationException` | `--top-k must be between 1 and 50.` | 1 |
| `AddCommand` (file không có / không đọc được) | `ValidationException` | `File 'x.md' not found.` / `Cannot read file 'x.md': ...` | 1 |
| Client (`retrieve` không thấy; HTTP 404) | `KbDocumentNotFoundException` | `Document 'doc-999' not found` | 1 |
| `KbClientFactory` | `KbConfigurationException` | `KB_API_URL is required when KB_CLIENT=http.` | 1 |
| `ZendeskKbClient.AddAsync` | `KbOperationNotSupportedException` | `Zendesk Help Center is read-only: kb add is not supported with KB_CLIENT=zendesk.` | 1 |
| `KbHttpSender` (cả 2 client HTTP) | `KbApiException` | `Cannot reach KB API at ...` / `KB API returned 400 (Bad Request) for POST /add: title is required.` / `... invalid response ...: field 'tags' is missing.` | 3 |

Exit `1` nghĩa là người dùng tự sửa được (sửa lệnh, sửa cấu hình). Exit `3` nghĩa là vấn đề nằm ở phía KB API, nên script gọi `kb` có thể thử lại. Người dùng không bao giờ thấy stack trace.

## 4. Chọn client

| `KB_CLIENT` | `KB_API_URL` | Kết quả |
|---|---|---|
| không đặt / rỗng / `mock` (không phân biệt hoa thường) | bỏ qua | `MockKbClient` |
| `http` | URL `http(s)://` hợp lệ | `HttpKbClient`, `BaseAddress` luôn có `/` cuối |
| `zendesk` | URL `http(s)://` hợp lệ | `ZendeskKbClient`, locale từ `KB_ZENDESK_LOCALE` (mặc định `en-us`) |
| `http` / `zendesk` | không đặt / rỗng | `KbConfigurationException` (exit 1) |
| `http` / `zendesk` | không phải URL `http(s)://` | `KbConfigurationException` (exit 1) |
| giá trị khác | | `KbConfigurationException` (exit 1) |

`KB_API_TOKEN` (không bắt buộc) được gửi trong header `Authorization: Bearer <token>`. `http` và `zendesk` dùng chung cách đọc và kiểm tra URL, timeout và token.

## 5. API contract

Contract giữa `HttpKbClient` và KB API (`KB_CLIENT=http`). Zendesk dùng API riêng, xem [mục 6](#6-tích-hợp-zendesk-help-center).

Mọi thao tác dùng `POST` với body JSON (`Content-Type: application/json`), tên field dạng camelCase. Phần request lấy đúng từ `architecture.md` của Tuần 3. `architecture.md` chỉ cho ví dụ response của `/search`, nên các response còn lại là **giả định** của project (xem [mục 8](#8-giả-định-và-giới-hạn)). Toàn bộ định dạng được khai báo một lần trong `Clients/KbApiContract.cs` và dùng chung cho cả `HttpKbClient` lẫn KB API.

Các ví dụ dưới đây là output thật của `tools/KnowledgeBase.Api` với dữ liệu mẫu.

### `POST /search`

```json
// request
{ "query": "response", "topK": 3 }

// 200 OK
{
  "results": [
    { "id": "doc-001", "title": "Customer Response Template", "nodePath": "/templates/email", "matchType": "title" },
    { "id": "doc-008", "title": "Ticket Handling Guide", "nodePath": "/docs/guides", "matchType": "content" }
  ]
}
```

`matchType` là `title`, `tag` hoặc `content` (field thêm so với `architecture.md`). Không khớp: `{"results": []}`.

### `POST /list`

```json
// request
{ "nodePath": "/team/devops", "limit": 2 }

// 200 OK
{
  "documents": [
    { "id": "doc-004", "title": "DevOps Team Members", "nodePath": "/team/devops" },
    { "id": "doc-005", "title": "DevOps On-call Schedule", "nodePath": "/team/devops" }
  ]
}
```

Chỉ lấy tài liệu nằm đúng node đó (không lấy node con), sắp theo id.

### `POST /retrieve`

```json
// request
{ "docId": "doc-001" }

// 200 OK
{
  "id": "doc-001",
  "title": "Customer Response Template",
  "content": "# Customer Response Template\n\nDear {customer}, ...",
  "nodePath": "/templates/email",
  "tags": ["template", "email"]
}

// 404 Not Found (id không tồn tại)
{ "error": "Document 'doc-999' not found" }
```

### `POST /add`

```json
// request
{ "title": "New Template", "content": "Hello", "nodePath": "/templates/email", "tags": ["template"] }

// 200 OK: tài liệu đã lưu, kèm id mới
{ "id": "doc-010", "title": "New Template", "content": "Hello", "nodePath": "/templates/email", "tags": ["template"] }
```

### Quy tắc request (KB API kiểm tra, sai → `400`)

| Field | Quy tắc |
|---|---|
| `query`, `docId`, `title`, `content` | Bắt buộc, không rỗng, không chỉ có khoảng trắng |
| `topK` | 1–50 |
| `limit` | 1–100 |
| `nodePath` | Bắt buộc, bắt đầu bằng `/` |
| `tags` | Không bắt buộc (không gửi thì lưu `[]`); có thì không được chứa phần tử rỗng hoặc `null` |

Đây cũng là các giới hạn mà `KbService` của CLI dùng (`KbRequestValidator` lấy `MaxTopK`, `MaxLimit` từ `KbService`), nên CLI và server không thể lệch nhau. Request bị từ chối thì không có dữ liệu nào được ghi.

```json
// POST /add  {"title": "", "content": "Hello", "nodePath": "/templates/sms", "tags": []}
// 400 Bad Request
{ "error": "title is required." }
```

### Xác thực và lỗi

Mọi lỗi đều có body `{"error": "<lý do>"}`.

| Trường hợp | Response | `HttpKbClient` |
|---|---|---|
| Request sai quy tắc ở trên | `400` `{"error":"title is required."}` | `KbApiException`: `KB API returned 400 (Bad Request) for POST /add: title is required.` |
| Body rỗng, JSON hỏng, `null`, sai kiểu dữ liệu | `400` `{"error":"Request body is missing or is not valid JSON for this endpoint."}` | `KbApiException` (exit 3) |
| `Content-Type` không phải JSON | `415` `{"error":"Content-Type must be application/json."}` | `KbApiException` (exit 3) |
| Server đặt token, request thiếu hoặc sai `Authorization: Bearer ...` | `401` `{"error":"Missing or invalid token."}` | `KbApiException` (exit 3) |
| `/retrieve` không có id | `404` | `KbDocumentNotFoundException` (exit 1) |
| Lỗi phía server | `500` | `KbApiException`: `KB API returned 500 (Internal Server Error) for POST /search.` |
| Response `200` nhưng body không phải JSON | | `KbApiException`: `KB API returned an invalid response for POST /search.` |
| Response `200` nhưng body là `null` hoặc thiếu field bắt buộc | | `KbApiException`: `KB API returned an invalid response for POST /retrieve: field 'content' is missing.` |
| Không kết nối được | | `KbApiException`: `Cannot reach KB API at ...` |
| Quá 10 giây | | `KbApiException`: `KB API at ... did not respond within 10 seconds.` |

Field bắt buộc trong response: `results` / `documents` và `id`, `title`, `nodePath` của từng phần tử; với tài liệu đầy đủ thêm `content` và `tags` (không chứa `null`). `matchType` không bắt buộc.

KB API của project có thêm `GET /health` → `{"status":"ok"}` (không cần token), để kiểm tra server đã chạy.

## 6. Tích hợp Zendesk Help Center

**Vì sao:** mentor góp ý cần bằng chứng kiểm thử với một KB API **bên ngoài**, trong khi bài tập chưa cấp KB API thật. [Zendesk Help Center](https://developer.zendesk.com/api-reference/help_center/help-center-api/) là một nền tảng KB thật, đang chạy production, và Help Center công khai cho đọc qua API mà không cần tài khoản. `ZendeskKbClient` cho CLI đọc trực tiếp một Help Center như vậy (`KB_CLIENT=zendesk`), ví dụ `https://support.zendesk.com`.

**Không thay thế KB API tự dựng:** Zendesk có API riêng (GET, field snake_case), khác contract của đề (POST JSON). Vì vậy đây là một **client thứ ba** sau `IKbClient`, không phải đổi `KB_API_URL` của `HttpKbClient`.

### Ánh xạ thao tác

Mọi request là `GET`, không có body. `{locale}` lấy từ `KB_ZENDESK_LOCALE` (mặc định `en-us`); từ khoá và locale được mã hoá URL.

| `IKbClient` | Zendesk | Ghi chú |
|---|---|---|
| `SearchAsync(query, topK)` | `api/v2/help_center/articles/search.json?query={q}&per_page={topK}&locale={locale}` | Giữ thứ tự xếp hạng của Zendesk; không bao giờ trả quá `topK` |
| `ListAsync(nodePath, limit)` | `api/v2/help_center/{locale}/sections/{id}/articles.json?per_page={limit}` | `nodePath` phải là `/sections/<số>`. Dạng khác thì trả rỗng, không gửi request; section 404 cũng trả rỗng. Giống mock với node không tồn tại |
| `RetrieveAsync(docId)` | `api/v2/help_center/{locale}/articles/{id}.json` | id không phải số thì `KbDocumentNotFoundException`, không gửi request; 404 cũng là not found |
| `AddAsync(document)` | (không gọi) | `KbOperationNotSupportedException`: tạo bài cần tài khoản agent (thử thật bị `403`) |

### Ánh xạ dữ liệu

| `KbDocument` | Bài viết Zendesk | Ghi chú |
|---|---|---|
| `Id` | `id` (số, kiểu `long`) | Đổi sang chuỗi: `"4408894162714"` |
| `Title` | `title` | Bắt buộc |
| `NodePath` | `section_id` | `/sections/{section_id}`; bắt buộc |
| `Tags` | `label_names` | Thiếu thì coi như không có tag |
| `Content` | `body` (HTML) | `HtmlText` đổi sang chữ thường. Bắt buộc khi `retrieve`; ở search/list thiếu thì coi như rỗng |
| `MatchType` | (không có) | `KbMatcher` tính theo cùng quy tắc với mock. Bài Zendesk tìm thấy nhờ biến thể của từ (không chứa nguyên từ khoá) được tính là `content` |

Response thiếu field bắt buộc, JSON hỏng, 429 (giới hạn tốc độ gọi), 5xx, mất mạng hay timeout đều do `KbHttpSender` đổi thành `KbApiException` (exit 3), giống `HttpKbClient`.

### Kiểm thử

| Mức | Cách làm | Test |
|---|---|---|
| Unit | `FakeHttpMessageHandler` + response **thật** của Zendesk đã lưu ở `tests/Fixtures/zendesk/` | Z01–Z09, U18 |
| Lệnh CLI | `FakeZendeskServer` (Kestrel, cổng ngẫu nhiên) trả cùng các fixture qua HTTP thật | C14–C15 |
| Chạy thật | Các lệnh `kb` với `https://support.zendesk.com` | [integration-evidence-zendesk.md](integration-evidence-zendesk.md) |

Bộ test tự động **không gọi Zendesk thật**, vì dữ liệu của Zendesk thay đổi theo thời gian và có giới hạn tốc độ gọi. Nhờ vậy test cho cùng kết quả ở mọi lần chạy, kể cả khi không có mạng. Contract test K01–K06 không áp dụng cho Zendesk: K06 cần `add`, còn các test khác cần dữ liệu mẫu cố định.

## 7. Quyết định thiết kế

**Mock trước, HTTP sau, cùng một interface.** Toàn bộ lệnh CLI được viết và test với `MockKbClient` trước khi có HTTP. Khi thêm `HttpKbClient`, không lệnh nào phải sửa, vì cả hai cài đặt cùng `IKbClient`.

**Contract test thay vì tin rằng hai client giống nhau.** Đề yêu cầu "HTTP client có cùng hành vi với mock". Bộ test K01–K06 được viết một lần (lớp trừu tượng `KbClientContractTests`) và chạy hai lần: một lần với `MockKbClient`, một lần với `HttpKbClient` gọi qua mạng thật tới KB API. Nếu hai client lệch nhau, test của một bên sẽ fail.

**Tự dựng KB API.** Bài tập không cung cấp KB API thật. Nếu không có server, các test HTTP sẽ phải bị skip, và AC "tích hợp với KB API đã được test" không được kiểm chứng. Vì vậy repo có `tools/KnowledgeBase.Api` theo đúng contract. Server này dùng lại logic tìm kiếm của `MockKbClient` (để hai bên có cùng hành vi) nhưng lưu dữ liệu vào file và chạy như một dịch vụ độc lập. Đây **không phải** KB API của MindX.

**Test tích hợp chạy được với bất kỳ KB API nào.** R01a–R01d mặc định khởi động KB API thành một tiến trình riêng, nên request đi qua mạng tới một chương trình khác, giống khi gọi dịch vụ bên ngoài. Đặt `KB_REAL_API_URL` thì cùng bộ test đó chạy với server ở URL đó. Mỗi test tự thêm tài liệu có tên riêng rồi kiểm tra trên chính tài liệu đó, nên không phụ thuộc dữ liệu có sẵn.

**Một định nghĩa JSON dùng chung.** `KbApiJson.Configure` là chỗ duy nhất quy định camelCase và enum dạng chữ. Client và server cùng gọi hàm này, nên không thể có chuyện server ghi `"matchType": 0` trong khi client chờ `"title"`.

**Validate ở cả hai đầu.** Phía CLI, `KbService` chặn input sai trước khi gửi request, nên mock và HTTP báo lỗi giống hệt nhau và không tốn một lượt gọi mạng. Phía server, KB API không tin client: request có thể đến từ curl, Postman hay chương trình khác, nên `KbRequestValidator` kiểm tra lại với cùng giới hạn và trả `400` trước khi chạm tới dữ liệu. System.Text.Json không báo lỗi khi thiếu field (field thiếu chỉ nhận `null` hoặc `0`), nên validator bắt chung "thiếu field" và "field rỗng".

**Không tin response của server.** Ngược lại, `HttpKbClient` không tin KB API: response `null` hoặc thiếu field bắt buộc bị báo thành lỗi có tên field, thay vì để lọt `null` lên tầng lệnh rồi thành `NullReferenceException` khó hiểu. Khi server từ chối request, lý do trong `{"error": ...}` được đưa vào thông báo, để người dùng biết phải sửa gì.

**Mọi lỗi I/O đều thành thông báo.** Lỗi đọc file của `kb add` (file bị khoá, không có quyền) được bắt ở `AddCommand` và báo `Cannot read file ...` với exit 1, như mọi lỗi người dùng khác. Không có exception nào lọt ra ngoài thành stack trace.

**Refactor trước khi thêm Zendesk.** Trước khi viết test Zendesk, phần gửi request của `HttpKbClient` (token, log, timeout, đổi lỗi) được tách ra `KbHttpSender`, và quy tắc MATCH của mock được tách ra `KbMatcher`. Lúc đó 136 test và golden master bảo đảm hành vi không đổi. Nhờ vậy `ZendeskKbClient` chỉ phải viết phần riêng của Zendesk (URL, ánh xạ field, HTML), và mọi client HTTP xử lý lỗi giống nhau.

**Không mock HTTP ở tầng lệnh.** Unit test của `HttpKbClient` dùng `FakeHttpMessageHandler` để kiểm tra từng trường hợp lỗi (500, JSON hỏng, timeout). Test lệnh và test contract dùng server Kestrel thật ở cổng ngẫu nhiên trên `127.0.0.1`, nên kiểm tra được cả việc ghép URL, header và JSON.

**Công nghệ.** Đề gợi ý TypeScript, Commander.js, fetch/axios và Jest. Project dùng C# để nối tiếp Tuần 2; các phần tương ứng:

| Đề gợi ý | Project dùng |
|---|---|
| TypeScript interface `Document`, `SearchResult`, `KBQuery` | C# record `KbDocument`, `SearchResult`, `KbQuery` |
| Commander.js | System.CommandLine 2.0 |
| fetch / axios | `HttpClient` + `System.Net.Http.Json` |
| Jest | xUnit |
| (server KB API) | ASP.NET Core Minimal API |
