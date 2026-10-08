# Knowledge Base CLI

Công cụ dòng lệnh `kb` để tìm kiếm, liệt kê, xem và thêm tài liệu trong một kho tri thức (Knowledge Base, KB): mẫu email, tài liệu hướng dẫn, thông tin nhóm... Tài liệu được sắp theo node dạng cây (`/templates/email`, `/team/devops`, `/docs/guides`).

```
$ kb search "response" --top-k 3
ID        MATCH    NODE                TITLE
doc-001   title    /templates/email    Customer Response Template
doc-008   content  /docs/guides        Ticket Handling Guide

$ kb retrieve doc-001
ID:    doc-001
Title: Customer Response Template
Node:  /templates/email
Tags:  template, email

# Customer Response Template
...
```

CLI có ba client, chọn bằng biến môi trường:

- **Mock client** (mặc định): dữ liệu mẫu nằm trong bộ nhớ, không cần server. Dùng khi phát triển và khi test.
- **HTTP client**: gọi KB API theo API contract của đề qua HTTP (`POST /search`, `/list`, `/retrieve`, `/add`).
- **Zendesk client**: đọc một **Zendesk Help Center** thật, công khai, ví dụ `https://support.zendesk.com`. Chỉ đọc: `search`, `list`, `retrieve`.

Dự án được xây dựng theo Test-Driven Development (bài tập Tuần 3, MindX Engineering Onboarding), tiếp nối Ticket Manager CLI của Tuần 2.

> **Về KB API:** bài tập không cung cấp KB API thật, nên repo có kèm một KB API tự dựng (`tools/KnowledgeBase.Api`) theo đúng API contract trong `architecture.md` của Tuần 3. Khi có KB API thật, chỉ cần đổi `KB_API_URL`, không cần sửa code. Để có bằng chứng tích hợp với một KB **bên ngoài thật**, CLI còn đọc được Zendesk Help Center (không phải KB của MindX, và theo contract khác). Xem [docs/kb-architecture.md](docs/kb-architecture.md).

## Mục lục

- [Yêu cầu hệ thống](#yêu-cầu-hệ-thống)
- [Cài đặt](#cài-đặt)
- [Cấu hình](#cấu-hình)
- [Chạy với mock client](#chạy-với-mock-client)
- [Chạy với HTTP client và KB API](#chạy-với-http-client-và-kb-api)
- [Chạy với Zendesk Help Center](#chạy-với-zendesk-help-center)
- [API contract](#api-contract)
- [Các lệnh](#các-lệnh)
- [Exit code](#exit-code)
- [Chạy test](#chạy-test)
- [Kiểm thử với KB API bên ngoài](#kiểm-thử-với-kb-api-bên-ngoài)
- [Cấu trúc dự án](#cấu-trúc-dự-án)
- [Tài liệu khác](#tài-liệu-khác)

## Yêu cầu hệ thống

- [.NET SDK 9.0](https://dotnet.microsoft.com/download/dotnet/9.0) (bản 9.0.101 trở lên; phiên bản được cố định trong `global.json`)
- Git
- Hệ điều hành: Windows, macOS hoặc Linux

Kiểm tra đã cài .NET SDK:

```bash
dotnet --list-sdks
```

Kết quả phải có một dòng bắt đầu bằng `9.0.`.

## Cài đặt

### 1. Lấy mã nguồn và build

```bash
git clone https://github.com/NgoVanCuong178/mindX_week3.git
cd mindX_week3
dotnet build
```

Build thành công sẽ in `Build succeeded` với `0 Error(s)`.

### 2. Chọn cách chạy

**Cách A: cài thành lệnh `kb` (khuyến nghị)**

```bash
dotnet pack src/KnowledgeBase.Cli -o ./nupkg
dotnet tool install --global KnowledgeBase.Cli --add-source ./nupkg
kb --help
```

Nếu báo không tìm thấy lệnh `kb`, thêm thư mục tool của .NET vào `PATH`:
- Windows: `%USERPROFILE%\.dotnet\tools`
- macOS / Linux: `~/.dotnet/tools`

Cập nhật lên bản code mới: chạy `dotnet tool uninstall --global KnowledgeBase.Cli` rồi chạy lại hai lệnh `pack` và `install` ở trên.

**Cách B: chạy trực tiếp từ mã nguồn**

```bash
dotnet run --project src/KnowledgeBase.Cli -- search "response"
```

Mọi ví dụ bên dưới viết theo cách A (`kb ...`). Với cách B, thay `kb` bằng `dotnet run --project src/KnowledgeBase.Cli --`.

## Cấu hình

CLI đọc cấu hình từ biến môi trường:

| Biến | Bắt buộc | Giá trị | Ý nghĩa |
|---|---|---|---|
| `KB_CLIENT` | Không | `mock` (mặc định), `http` hoặc `zendesk` | Chọn client. Không đặt hoặc để rỗng thì dùng mock. Không phân biệt hoa thường |
| `KB_API_URL` | Có, khi `KB_CLIENT=http` hoặc `zendesk` | URL `http://` hoặc `https://` | Địa chỉ gốc của KB API, ví dụ `http://localhost:5080`, hoặc của Help Center, ví dụ `https://support.zendesk.com`. Có hay không có `/` ở cuối đều được |
| `KB_API_TOKEN` | Không | Chuỗi bất kỳ | Nếu đặt, mỗi request gửi kèm header `Authorization: Bearer <token>` |
| `KB_ZENDESK_LOCALE` | Không | Mã ngôn ngữ, mặc định `en-us` | Ngôn ngữ bài viết, chỉ dùng khi `KB_CLIENT=zendesk` |

Mỗi request HTTP chờ tối đa **10 giây**.

**Cách đặt biến môi trường** (chỉ có hiệu lực trong cửa sổ terminal hiện tại):

| Terminal | Lệnh |
|---|---|
| Git Bash / macOS / Linux | `export KB_CLIENT=http`<br>`export KB_API_URL=http://localhost:5080` |
| PowerShell | `$env:KB_CLIENT = "http"`<br>`$env:KB_API_URL = "http://localhost:5080"` |
| Command Prompt (cmd) | `set KB_CLIENT=http`<br>`set KB_API_URL=http://localhost:5080` |

Quay lại mock: Git Bash `unset KB_CLIENT`, PowerShell `Remove-Item Env:KB_CLIENT`.

## Chạy với mock client

Không cần cấu hình gì: mock là mặc định. Mock có sẵn 3 tài liệu:

| Id | Node | Title |
|---|---|---|
| `doc-001` | `/templates/email` | Customer Response Template |
| `doc-002` | `/team/devops` | DevOps Team Members |
| `doc-003` | `/docs/guides` | Getting Started Guide |

```bash
kb search "response"
kb list --node /team/devops
kb retrieve doc-001
```

Dữ liệu mock chỉ nằm trong bộ nhớ: tài liệu thêm bằng `kb add` mất khi lệnh kết thúc.

## Chạy với HTTP client và KB API

### Bước 1: bật KB API

Mở một terminal riêng và để nó chạy:

```bash
dotnet run --project tools/KnowledgeBase.Api --no-launch-profile
```

Server lắng nghe ở `http://localhost:5080`. Kiểm tra:

```bash
curl http://localhost:5080/health
# {"status":"ok"}
```

Cấu hình của KB API (biến môi trường, hoặc tham số `--TÊN=giá-trị` sau lệnh `dotnet run`):

| Thiết lập | Mặc định | Ý nghĩa |
|---|---|---|
| `--urls` | `http://localhost:5080` | Địa chỉ lắng nghe |
| `KB_SERVER_DATA_FILE` | `kb-data.json` trong thư mục đang chạy lệnh | File lưu tài liệu. Chưa có thì tự tạo với 8 tài liệu mẫu |
| `KB_SERVER_TOKEN` | không đặt | Nếu đặt, mọi request (trừ `GET /health`) phải có `Authorization: Bearer <token>`, không thì trả HTTP 401 |

Ví dụ: chạy ở cổng 5090, lưu dữ liệu ngoài repo, bắt buộc token:

```bash
dotnet run --project tools/KnowledgeBase.Api --no-launch-profile -- --urls http://localhost:5090 --KB_SERVER_DATA_FILE=../kb-data-dev.json --KB_SERVER_TOKEN=dev-token
```

Dữ liệu mẫu của KB API (8 tài liệu theo cấu trúc trong `architecture.md`):

| Node | Tài liệu |
|---|---|
| `/templates/email` | `doc-001` Customer Response Template, `doc-002` Incident Acknowledgement Template, `doc-003` Email Templates README |
| `/team/devops` | `doc-004` DevOps Team Members, `doc-005` DevOps On-call Schedule, `doc-006` DevOps Team README |
| `/docs/guides` | `doc-007` Getting Started Guide, `doc-008` Ticket Handling Guide |

Muốn quay về dữ liệu mẫu ban đầu: tắt server, xoá file dữ liệu, bật lại.

### Bước 2: chuyển CLI sang HTTP client

Ở một terminal khác:

```bash
export KB_CLIENT=http
export KB_API_URL=http://localhost:5080
# export KB_API_TOKEN=dev-token   # chỉ khi server có đặt KB_SERVER_TOKEN

kb list --node /team/devops
```

```
ID        NODE                TITLE
doc-004   /team/devops        DevOps Team Members
doc-005   /team/devops        DevOps On-call Schedule
doc-006   /team/devops        DevOps Team README
```

Kết quả có `doc-004`..`doc-006` (dữ liệu của KB API) nghĩa là CLI đang gọi qua HTTP. Nếu chỉ thấy `doc-002`, CLI vẫn đang dùng mock: kiểm tra lại biến môi trường trong **đúng** terminal đang chạy `kb`.

### Kết nối tới một KB API khác

Chỉ cần trỏ `KB_API_URL` (và `KB_API_TOKEN` nếu có) sang server đó. Server phải tuân theo [API contract](#api-contract). Nên thử bằng `curl` trước:

```bash
curl -X POST "$KB_API_URL/search" -H "Content-Type: application/json" \
     -H "Authorization: Bearer $KB_API_TOKEN" -d '{"query":"response","topK":3}'
```

Sau đó chạy script kiểm thử với server đó (xem [Kiểm thử với KB API bên ngoài](#kiểm-thử-với-kb-api-bên-ngoài)).

### Lưu ý khi dùng Git Bash trên Windows

Git Bash tự đổi tham số bắt đầu bằng `/` thành đường dẫn Windows: `--node /team/devops` thành `--node C:/Program Files/Git/team/devops`, và CLI báo lỗi node sai. Tắt việc này trước khi chạy `kb`:

```bash
export MSYS_NO_PATHCONV=1
```

PowerShell, cmd, macOS và Linux không bị.

## Chạy với Zendesk Help Center

[Zendesk Help Center](https://developer.zendesk.com/api-reference/help_center/help-center-api/) là một nền tảng KB thật, được nhiều công ty dùng cho FAQ, hướng dẫn và quy trình hỗ trợ. Help Center công khai đọc được **không cần tài khoản**. `KB_CLIENT=zendesk` cho CLI đọc trực tiếp một Help Center như vậy, ví dụ Help Center của chính Zendesk ở `https://support.zendesk.com`.

```bash
export KB_CLIENT=zendesk
export KB_API_URL=https://support.zendesk.com
# export KB_ZENDESK_LOCALE=en-us   # không bắt buộc

kb search "password reset" --top-k 3
kb list --node /sections/4405298881946 --limit 5
kb retrieve 4408894162714
```

PowerShell: `$env:KB_CLIENT = "zendesk"` và `$env:KB_API_URL = "https://support.zendesk.com"`. Với Git Bash, nhớ đặt `MSYS_NO_PATHCONV=1` (xem mục trên).

```
$ kb retrieve 4408894162714
ID:    4408894162714
Title: How long are account verification emails and password reset emails valid?
Node:  /sections/4405298881946
Tags:  mtpe, ks, ab0, Support, ...

Question

How long are account verification emails and password reset emails valid?

Answer

Both account verification emails and password reset emails expire after 24 hours. ...
```

**Khác biệt so với mock và HTTP client:**

| | Zendesk |
|---|---|
| Id tài liệu | Số, ví dụ `4408894162714`: lấy từ cột `ID` của `kb search` |
| Node | Một section của Help Center: `/sections/<id>`, lấy từ cột `NODE` của `kb search`. Node khác dạng này, hoặc section không tồn tại, cho `No documents found.` |
| Tag | Nhãn (`label_names`) của bài viết |
| Nội dung | Bài viết là HTML; `kb retrieve` đổi thành chữ thường (bỏ thẻ, ảnh, giữ chữ của link) |
| Cột `MATCH` | Zendesk không trả field này, CLI tự tính theo cùng quy tắc với mock. Kết quả giữ đúng thứ tự xếp hạng của Zendesk |
| `kb add` | **Không hỗ trợ**: tạo bài viết cần tài khoản agent của Zendesk. CLI báo `Zendesk Help Center is read-only...` (exit 1) và không gửi request |
| `KB_API_TOKEN` | Không cần với Help Center công khai; nếu đặt, được gửi dạng OAuth `Bearer` token |

Có thể trỏ `KB_API_URL` sang Help Center công khai của bất kỳ công ty nào dùng Zendesk.

Các endpoint được gọi (đều là `GET`, kiểm tra được bằng trình duyệt):

| Lệnh | Endpoint của Zendesk |
|---|---|
| `kb search <q> --top-k <n>` | `/api/v2/help_center/articles/search.json?query=<q>&per_page=<n>&locale=<locale>` |
| `kb list --node /sections/<id> --limit <n>` | `/api/v2/help_center/<locale>/sections/<id>/articles.json?per_page=<n>` |
| `kb retrieve <id>` | `/api/v2/help_center/<locale>/articles/<id>.json` |

Ảnh chạy thử thật và kết quả: [docs/integration-evidence-zendesk.md](docs/integration-evidence-zendesk.md).

## API contract

Contract giữa HTTP client (`KB_CLIENT=http`) và KB API. Zendesk client dùng API của Zendesk, xem [mục trên](#chạy-với-zendesk-help-center).

CLI và KB API trao đổi bằng JSON qua HTTP. Mọi thao tác là `POST` với `Content-Type: application/json`; tên field dạng camelCase. Request lấy đúng theo `architecture.md` của Tuần 3.

| Endpoint | Request | Response thành công (200) |
|---|---|---|
| `POST /search` | `{"query": "response", "topK": 5}` | `{"results": [{"id", "title", "nodePath", "matchType"}]}` |
| `POST /list` | `{"nodePath": "/templates/email", "limit": 10}` | `{"documents": [{"id", "title", "nodePath"}]}` |
| `POST /retrieve` | `{"docId": "doc-001"}` | `{"id", "title", "content", "nodePath", "tags"}` |
| `POST /add` | `{"title", "content", "nodePath", "tags"}` | Tài liệu đã lưu kèm id mới: `{"id", "title", "content", "nodePath", "tags"}` |

`matchType` (`title` / `tag` / `content`) là field project thêm vào và không bắt buộc. Mọi field khác trong response đều bắt buộc.

**Quy tắc request** (KB API kiểm tra; CLI cũng kiểm tra trước khi gửi):

| Field | Quy tắc |
|---|---|
| `query`, `docId`, `title`, `content` | Bắt buộc, không được rỗng hoặc chỉ có khoảng trắng |
| `topK` | 1–50 |
| `limit` | 1–100 |
| `nodePath` | Bắt buộc, bắt đầu bằng `/` |
| `tags` | Không bắt buộc (không gửi = `[]`); nếu có thì không được chứa phần tử rỗng hoặc `null` |

**Lỗi** luôn có body `{"error": "<lý do>"}`:

| Mã | Khi nào | Ví dụ `error` |
|---|---|---|
| `400` | Request sai quy tắc trên, body rỗng, JSON hỏng, sai kiểu dữ liệu | `title is required.` |
| `401` | Server đặt token mà request thiếu hoặc sai `Authorization: Bearer ...` | `Missing or invalid token.` |
| `404` | `/retrieve` với id không tồn tại | `Document 'doc-999' not found` |
| `415` | `Content-Type` không phải JSON | `Content-Type must be application/json.` |

CLI hiển thị cả mã lỗi lẫn lý do của server, ví dụ `KB API returned 400 (Bad Request) for POST /add: title is required.`. Nếu response thành công nhưng là `null` hoặc thiếu field bắt buộc, CLI báo `KB API returned an invalid response for POST /add: field 'tags' is missing.` (exit 3), chứ không chạy tiếp với dữ liệu thiếu.

Ví dụ request/response đầy đủ và lý do thiết kế: [docs/kb-architecture.md](docs/kb-architecture.md#5-api-contract).

## Các lệnh

```bash
kb --help
kb search --help
```

Tuỳ chọn `--verbose` dùng được với mọi lệnh: in một dòng log cho mỗi request HTTP ra **stderr** (method, URL, mã trạng thái, thời gian). Log không bao giờ chứa token.

```
$ kb --verbose search reminder
POST http://localhost:5080/search -> 200 OK (55 ms)
ID        MATCH    NODE                TITLE
doc-009   title    /templates/sms      SMS Reminder Template
```

### `kb search` — Tìm tài liệu

```bash
kb search <query> [--top-k <n>]
```

| Tham số | Bắt buộc | Mô tả |
|---|---|---|
| `query` | Có | Từ khoá; khoảng trắng hai đầu được bỏ |
| `--top-k` | Không | Số kết quả tối đa, 1–50. Mặc định 5 |

Tìm không phân biệt hoa thường trong title, tag và content. Cột `MATCH` cho biết khớp ở đâu; kết quả xếp theo thứ tự `title`, `tag`, `content`, rồi theo id.

```
$ kb search "response" --top-k 3
ID        MATCH    NODE                TITLE
doc-001   title    /templates/email    Customer Response Template
doc-008   content  /docs/guides        Ticket Handling Guide
```

### `kb list` — Liệt kê tài liệu trong một node

```bash
kb list --node <node-path> [--limit <n>]
```

| Tuỳ chọn | Bắt buộc | Mô tả |
|---|---|---|
| `--node` | Có | Node, phải bắt đầu bằng `/`. Chỉ lấy tài liệu nằm đúng node này |
| `--limit` | Không | Số tài liệu tối đa, 1–100. Mặc định 10 |

```
$ kb list --node /templates/email --limit 10
ID        NODE                TITLE
doc-001   /templates/email    Customer Response Template
```

Không có tài liệu nào: in `No documents found.` (exit 0).

### `kb retrieve` — Xem một tài liệu

```bash
kb retrieve <doc-id>
```

```
$ kb retrieve doc-001
ID:    doc-001
Title: Customer Response Template
Node:  /templates/email
Tags:  template, email

# Customer Response Template

Dear {customer},

Thank you for contacting MindX support. We have received your request and will reply as soon as possible.

Best regards,
MindX Support Team
```

### `kb add` — Thêm tài liệu từ file Markdown

```bash
kb add --file <file.md> --path <node-path> [--tags <tag> ...] [--title <title>]
```

| Tuỳ chọn | Bắt buộc | Mô tả |
|---|---|---|
| `--file` | Có | File Markdown, không được rỗng |
| `--path` | Có | Node đích, phải bắt đầu bằng `/`. Node chưa có thì được tạo |
| `--tags` | Không | Một hoặc nhiều tag, cách nhau bởi dấu cách (`--tags sms reminder`). Chuyển về chữ thường, bỏ tag rỗng và trùng |
| `--title` | Không | Mặc định: dòng `# tiêu đề` đầu tiên trong file; nếu file không có thì dùng tên file (bỏ đuôi) |

Id được cấp tự động: số lớn nhất hiện có cộng 1 (`doc-009`).

```
$ kb add --file new-template.md --path /templates/sms --tags sms reminder
Added document doc-009
```

### Thông báo lỗi thường gặp

| Tình huống | Thông báo (stderr) | Exit code |
|---|---|---|
| Thiếu tuỳ chọn bắt buộc | `Option '--node' is required.` | 1 |
| `--top-k` ngoài 1–50 | `--top-k must be between 1 and 50.` | 1 |
| `--limit` ngoài 1–100 | `--limit must be between 1 and 100.` | 1 |
| Node không bắt đầu bằng `/` | `--node must be a node path starting with '/', for example /templates/email.` | 1 |
| File không tồn tại | `File 'nope.md' not found.` | 1 |
| File rỗng | `File 'empty.md' is empty.` | 1 |
| File có nhưng không đọc được (đang bị khoá, không có quyền) | `Cannot read file 'new.md': The process cannot access the file ...` | 1 |
| Không có tài liệu | `Document 'doc-999' not found` | 1 |
| `KB_CLIENT=http` mà thiếu URL | `KB_API_URL is required when KB_CLIENT=http.` | 1 |
| `KB_CLIENT` sai | `KB_CLIENT must be 'mock', 'http' or 'zendesk', not 'abc'.` | 1 |
| `kb add` khi `KB_CLIENT=zendesk` | `Zendesk Help Center is read-only: kb add is not supported with KB_CLIENT=zendesk.` | 1 |
| KB API chưa bật / sai cổng | `Cannot reach KB API at http://localhost:5080/: ...` | 3 |
| Thiếu hoặc sai token | `KB API returned 401 (Unauthorized) for POST /list: Missing or invalid token.` | 3 |
| KB API từ chối request | `KB API returned 400 (Bad Request) for POST /add: title is required.` | 3 |
| KB API lỗi | `KB API returned 500 (Internal Server Error) for POST /search.` | 3 |
| Zendesk giới hạn tốc độ gọi | `KB API returned 429 (Too Many Requests) for GET /api/v2/help_center/articles/search.json...` | 3 |
| KB API trả response `null` hoặc thiếu field | `KB API returned an invalid response for POST /retrieve: field 'content' is missing.` | 3 |
| KB API không trả lời sau 10 giây | `KB API at ... did not respond within 10 seconds.` | 3 |

Thông báo lỗi in ra **stderr**, kết quả in ra **stdout**.

## Exit code

| Exit code | Ý nghĩa |
|---|---|
| `0` | Thành công |
| `1` | Lỗi phía người dùng: sai cú pháp lệnh, dữ liệu không hợp lệ, không tìm thấy tài liệu, cấu hình biến môi trường sai, thao tác mà client đang dùng không hỗ trợ (`kb add` với Zendesk) |
| `3` | Lỗi phía KB API: không kết nối được, hết thời gian chờ, server trả mã lỗi, response không đọc được hoặc thiếu field |

Exit code `2` của Tuần 2 (file dữ liệu hỏng) không còn dùng, vì CLI không đọc file dữ liệu nữa.

## Chạy test

```bash
dotnet test
```

Kết quả mong đợi:

```
Passed!  - Failed: 0, Passed: 109, ... KnowledgeBase.UnitTests.dll
Passed!  - Failed: 0, Passed: 82, ... KnowledgeBase.IntegrationTests.dll
```

Không cần bật KB API trước: test tự khởi động KB API khi cần (trong tiến trình test, hoặc thành tiến trình riêng). Test Zendesk **không gọi Zendesk thật**: chúng dùng response thật đã lưu trong `tests/Fixtures/zendesk/` và một server Zendesk giả, nên chạy được khi không có mạng và không phụ thuộc dữ liệu thay đổi của Zendesk.

| Nhóm | Mã | Nội dung |
|---|---|---|
| Unit: service | U01–U07 | Validate và chuẩn hoá input của 4 lệnh |
| Unit: client | U08–U18 | Chọn client theo biến môi trường (kể cả `zendesk`); `HttpKbClient` với HTTP giả: request đúng contract, đọc response, lỗi 4xx/5xx kèm lý do của server, mất kết nối, timeout, response `null` hoặc thiếu field, token, log |
| Unit: Zendesk | Z01–Z09 | `ZendeskKbClient` với response thật đã lưu: URL `GET` và mã hoá query, ánh xạ section/label/HTML, cột MATCH, node và id không hợp lệ, 404, `add` chỉ đọc, 429/500/JSON hỏng/thiếu field, log và token; đổi HTML sang chữ |
| Contract | K01–K06 | Cùng một bộ test chạy cho **cả** mock và HTTP client: hai client phải có cùng hành vi |
| Lệnh CLI | C01–C15 | Cả 4 lệnh, với mock, HTTP và Zendesk (server Zendesk giả), cùng các trường hợp lỗi (kể cả file không đọc được) |
| KB API | V01–V07 | Lưu file, dữ liệu mẫu, token; từ chối request sai bằng HTTP 400 và không lưu dữ liệu sai |
| Tích hợp | R01a–R01d | CLI client gọi KB API chạy ở **tiến trình riêng**, hoặc ở `KB_REAL_API_URL` |
| End-to-end | E01–E02 | Chạy chương trình `kb` thật |

Mỗi test có comment ghi loại (Normal / Abnormal / Boundary) và kỹ thuật thiết kế (EP, BVA, DT, EG).

Chạy riêng một nhóm hoặc một test:

```bash
dotnet test tests/KnowledgeBase.UnitTests
dotnet test --filter "FullyQualifiedName~K01"
```

**Chạy test tích hợp với một KB API khác** (ví dụ KB API thật khi được cấp):

```bash
export KB_REAL_API_URL=https://kb.example.com
export KB_REAL_API_TOKEN=...      # nếu server yêu cầu
dotnet test --filter "FullyQualifiedName~R01"
```

Mỗi test tự thêm một tài liệu có tên riêng vào node `/tests/smoke`, nên không phụ thuộc vào dữ liệu có sẵn trên server, nhưng sẽ **để lại** các tài liệu đó (API contract không có thao tác xoá).

**Đo code coverage:**

```bash
dotnet test --collect:"XPlat Code Coverage" --settings coverlet.runsettings
dotnet tool install -g dotnet-reportgenerator-globaltool
reportgenerator -reports:"tests/**/TestResults/**/coverage.cobertura.xml" -targetdir:TestResults/coverage -reporttypes:Html
```

Mở `TestResults/coverage/index.html` để xem báo cáo.

## Kiểm thử với KB API bên ngoài

`scripts/verify-kb-api.sh` kiểm tra CLI với **bất kỳ** KB API nào đang chạy ở một URL và ghi bằng chứng ra `docs/integration-evidence.md`. Script không khởi động server nào: mọi request đều đi qua mạng tới đúng URL được truyền vào.

```bash
export KB_API_TOKEN=...                               # nếu server yêu cầu; không bao giờ bị ghi vào báo cáo
export KB_TARGET_LABEL="KB API của MindX (staging)"   # mô tả server, ghi vào báo cáo
bash scripts/verify-kb-api.sh https://kb.example.com
```

Script chạy 10 bước và dừng với exit code khác 0 nếu có bước FAIL:

1. `curl` gửi request thô tới `/search` và `/list`, kiểm tra đúng định dạng contract.
2. CLI `kb` thật với `KB_CLIENT=http`: `search`, `list`, `add` một tài liệu có từ khoá riêng vào `/tests/smoke`, rồi `search` / `retrieve` / `list` lại chính tài liệu đó, và `retrieve` một id không tồn tại (phải exit 1).
3. Bộ test tích hợp R01a–R01d với `KB_REAL_API_URL` là URL đó.

Báo cáo ghi thời điểm, URL, commit mã nguồn, kết quả từng bước và output thật của từng lệnh.

> **Trạng thái hiện tại:** [docs/integration-evidence.md](docs/integration-evidence.md) là kết quả chạy với KB API **tự dựng** của project (bản publish chạy như một dịch vụ riêng, ngoài repo, có token). Chưa có kết quả với KB API của MindX vì chưa được cấp URL. Khi có URL, chạy lại script với URL đó để thay báo cáo.

### Bằng chứng với một KB bên ngoài thật: Zendesk Help Center

[docs/integration-evidence-zendesk.md](docs/integration-evidence-zendesk.md) ghi lại các lệnh `kb` chạy thật với `https://support.zendesk.com` (`KB_CLIENT=zendesk`), kèm ảnh chụp. Đây là KB bên ngoài, đang chạy production, do một bên thứ ba vận hành. Các yêu cầu đi qua Internet tới server của Zendesk, không qua code nào của project ở phía server.

## Cấu trúc dự án

```
├── src/KnowledgeBase.Cli/                CLI `kb`
│   ├── Program.cs                        Điểm vào: đọc biến môi trường, gọi CliApp
│   ├── CliApp.cs                         Ghép 4 lệnh, chuyển lỗi thành exit code
│   ├── Commands/                         Mỗi lệnh một file; DocumentFormatter in kết quả
│   ├── Services/KbService.cs             Validate và chuẩn hoá input
│   ├── Clients/
│   │   ├── IKbClient.cs                  Interface 4 thao tác: search, list, retrieve, add
│   │   ├── MockKbClient.cs               Client dữ liệu trong bộ nhớ
│   │   ├── HttpKbClient.cs               Client gọi KB API theo contract của đề (POST JSON)
│   │   ├── ZendeskKbClient.cs            Client đọc Zendesk Help Center (GET, chỉ đọc)
│   │   ├── KbHttpSender.cs               Gửi request, log, timeout, đổi lỗi: dùng chung cho 2 client HTTP
│   │   ├── KbMatcher.cs                  Quy tắc cột MATCH: dùng chung cho mock và Zendesk
│   │   ├── HtmlText.cs                   Đổi HTML của bài viết Zendesk sang chữ
│   │   ├── KbClientFactory.cs            Chọn client theo KB_CLIENT
│   │   ├── KbApiContract.cs              Định dạng JSON của API, dùng chung với KB API
│   │   ├── ZendeskContract.cs            Định dạng JSON của Zendesk
│   │   └── KbExceptions.cs               Lỗi không tìm thấy, lỗi API, lỗi cấu hình, thao tác không hỗ trợ
│   └── Models/                           KbDocument, SearchResult, KbQuery
├── tools/KnowledgeBase.Api/              KB API tự dựng (ASP.NET Core Minimal API)
│   ├── KbApiServer.cs                    Endpoint, token, trả lỗi dạng {"error": ...}
│   ├── KbRequestValidator.cs             Kiểm tra request (sai → HTTP 400)
│   ├── FileKbStore.cs                    Lưu tài liệu vào file JSON
│   ├── KbSeedData.cs                     8 tài liệu mẫu
│   └── kb-api.http                       Request mẫu để thử API trong Visual Studio / VS Code
├── tests/
│   ├── KnowledgeBase.UnitTests/
│   ├── KnowledgeBase.IntegrationTests/
│   └── Fixtures/zendesk/                 Response thật của Zendesk (rút gọn), dùng chung cho 2 project test
├── scripts/verify-kb-api.sh              Kiểm thử CLI với một KB API bất kỳ, ghi báo cáo
├── docs/
│   ├── kb-architecture.md                Kiến trúc và API contract
│   ├── integration-evidence.md           Báo cáo do scripts/verify-kb-api.sh sinh ra
│   ├── integration-evidence-zendesk.md   Bằng chứng chạy thật với Zendesk Help Center
│   ├── research-knowledge-base.md        Ghi chú tìm hiểu về Knowledge Base
│   └── images/                           Ảnh của các tài liệu trên
├── coverlet.runsettings                  Cấu hình đo coverage
└── global.json                           Cố định phiên bản .NET SDK
```

## Tài liệu khác

- [docs/kb-architecture.md](docs/kb-architecture.md): kiến trúc, luồng dữ liệu, API contract, các quyết định thiết kế.
- [docs/integration-evidence.md](docs/integration-evidence.md): bằng chứng kiểm thử tích hợp với KB API.
- [docs/integration-evidence-zendesk.md](docs/integration-evidence-zendesk.md): bằng chứng chạy thật với Zendesk Help Center.
- [docs/research-knowledge-base.md](docs/research-knowledge-base.md): ghi chú tìm hiểu về Knowledge Base.
