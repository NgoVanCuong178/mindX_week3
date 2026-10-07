# Test Cases — Knowledge Base CLI (Tuần 3)

Danh sách test case của bài Tuần 3, chia theo tầng test. Mỗi test ghi rõ nhóm (Normal / Abnormal / Boundary), kỹ thuật thiết kế và yêu cầu mà nó đáp ứng.

Trạng thái: **Red** — toàn bộ 97 lượt test đã viết và đều FAIL (code mới là stub), không có test nào bị skip.

---

## Tổng quan

| Tầng | Mã | Số hàm test | Số lượt chạy |
|---|---|---|---|
| Unit — `KbService` | U01–U07 | 9 | 22 |
| Unit — `KbClientFactory`, `HttpKbClient` | U08–U14 | 8 | 25 |
| Integration — contract test (mock và HTTP) | K01–K06 | 6 × 2 client | 18 |
| Integration — lệnh CLI | C01–C12 | 12 | 21 |
| Integration — KB API (`KnowledgeBase.Api`) | V01–V03 | 4 | 5 |
| E2E — chạy chương trình thật | E01–E02 | 2 | 2 |
| Tích hợp với KB API chạy ở tiến trình riêng | R01a–R01d | 4 | 4 |
| **Tổng** | | **45** | **97** |

**Kỹ thuật:** EP phân vùng tương đương · BVA giá trị biên · DT bảng quyết định · EG đoán lỗi

**Mã yêu cầu** (xem `docs/plans/week-3`): AC1–AC4 là tiêu chí nghiệm thu; T1–T9 là yêu cầu chi tiết trong `tasks.md` và `architecture.md`.

Chạy một test: `dotnet test --filter "FullyQualifiedName~K01"`

---

## 1. Unit — `KbService` (validate và chuẩn hoá input)

File: [KbServiceTests.cs](../tests/KnowledgeBase.UnitTests/Services/KbServiceTests.cs). Dùng `RecordingKbClient` (bản giả) để kiểm tra service truyền gì xuống client và không gọi client khi input sai.

| Mã | Nhóm | Input | Kết quả mong đợi | Kỹ thuật | Yêu cầu |
|---|---|---|---|---|---|
| U01 | Abnormal, Boundary | `query` = null / `""` / `"   "` | `ValidationException`, không gọi client | EP, EG | T6, T9 |
| U02 | Boundary | `--top-k` 0, 51 | `ValidationException` | BVA | T6 |
| U02 | Boundary | `--top-k` 1, 50; query `"  response  "` | Client nhận `KbQuery("response", topK)` | BVA | T4 |
| U03 | Boundary | `--limit` 0, 101 | `ValidationException` | BVA | T6 |
| U03 | Boundary | `--limit` 1, 100 | Truyền nguyên vẹn xuống client | BVA | T4 |
| U04 | Abnormal | `--node` null / `""` / `templates/email` (thiếu `/`) | `ValidationException` | EP | T6 |
| U05 | Abnormal | `docId` null / `"   "` | `ValidationException` | EP, EG | T6 |
| U06 | Normal | Title từ `--title` / dòng `# heading` / tên file | Đúng title; nội dung và node giữ nguyên; tags `[" SMS ", "sms", "   "]` → `["sms"]` | DT | T4 |
| U07 | Abnormal, Boundary | Nội dung `""` / `"   "`; `--path` thiếu `/` | `ValidationException` | EG, BVA | T6 |

## 2. Unit — `KbClientFactory` và `HttpKbClient`

Files: [KbClientFactoryTests.cs](../tests/KnowledgeBase.UnitTests/Clients/KbClientFactoryTests.cs), [HttpKbClientTests.cs](../tests/KnowledgeBase.UnitTests/Clients/HttpKbClientTests.cs). `HttpKbClient` dùng `FakeHttpMessageHandler` thay cho mạng.

| Mã | Nhóm | Trường hợp | Kết quả mong đợi | Kỹ thuật | Yêu cầu |
|---|---|---|---|---|---|
| U08 | Normal, Boundary | `KB_CLIENT` không đặt / `""` / `mock` / `MOCK`; `http` + URL | Mock; Http với `BaseAddress` có `/` cuối | DT, EG | AC2 |
| U08 | Abnormal | `http` thiếu URL; URL sai định dạng; `ftp` | `KbConfigurationException` | DT | AC2, T9 |
| U09 | Normal | Mỗi thao tác | `POST` đúng đường dẫn (`/search`, `/list`, `/retrieve`, `/add`), đúng body JSON camelCase | EP | T5 |
| U10 | Normal | Response JSON của mỗi thao tác | Đọc đúng thành đối tượng C#; `"matchType":"title"` → `MatchKind.Title` | EP, EG | T5 |
| U11 | Abnormal | `retrieve` nhận HTTP 404 | `KbDocumentNotFoundException` mang đúng id | EP | T5 |
| U12 | Abnormal | HTTP 500 / 400 / response không phải JSON / mất kết nối / hết thời gian chờ | `KbApiException`, thông báo dễ hiểu, không có chữ "Exception" | EG | T5, T9 |
| U13 | Normal | Có / không có token | Có / không có header `Authorization: Bearer ...` | DT | T5 |
| U14 | Normal | `--verbose` | Log có method, URL, status; không chứa token | EG | T5 |

## 3. Integration — contract test (mock và HTTP cùng hành vi)

Files: [KbClientContractTests.cs](../tests/KnowledgeBase.IntegrationTests/Contracts/KbClientContractTests.cs), [ClientContractRuns.cs](../tests/KnowledgeBase.IntegrationTests/Contracts/ClientContractRuns.cs).

Một bộ test chạy cho 2 client: `MockKbClient` và `HttpKbClient` (gọi qua mạng thật tới `KnowledgeBase.Api` chạy trong tiến trình test, dữ liệu mẫu cố định). Pass cả hai = HTTP client có cùng hành vi với mock (**T5**, **AC2**).

| Mã | Nhóm | Trường hợp | Kết quả mong đợi | Kỹ thuật |
|---|---|---|---|---|
| K01 | Normal | `search "RESPONSE"` | Khớp title (doc-001), tag (doc-002), content (doc-003), xếp đúng thứ tự đó; không phân biệt hoa thường | EP, EG |
| K02 | Boundary | `topK` = 2 khi có 3 kết quả; từ khoá không khớp | Cắt còn 2; danh sách rỗng | BVA |
| K03 | Normal, Boundary | `list` node có 2 tài liệu; `limit` = 1; node không tồn tại | Đúng tài liệu, theo id; cắt còn 1; rỗng | EP, BVA |
| K04 | Normal | `retrieve doc-002` | Đủ mọi field | EP |
| K05 | Abnormal | `retrieve doc-999` | `KbDocumentNotFoundException` | EP |
| K06 | Normal, Boundary | `add` tài liệu mới | Id `doc-005` (max + 1); `retrieve` và `list` thấy tài liệu vừa thêm | EG |

## 4. Integration — lệnh CLI

File: [KbCommandTests.cs](../tests/KnowledgeBase.IntegrationTests/Commands/KbCommandTests.cs). Gọi `CliApp.RunAsync` trong cùng tiến trình; biến môi trường truyền vào dưới dạng Dictionary.

| Mã | Nhóm | Lệnh | Kết quả mong đợi | Yêu cầu |
|---|---|---|---|---|
| C01 | Normal | `kb search "response" --top-k 3` (mock) | Exit 0; có doc-001, cột MATCH | T4, AC1 |
| C02 | Normal | `kb list --node /team/devops --limit 10` | Chỉ tài liệu của nhóm DevOps | T4, AC1 |
| C03 | Normal | `kb retrieve doc-001` | Title, node, tags, toàn bộ nội dung mẫu email | T4, AC1 |
| C04 | Normal | `kb add --file new-template.md --path /templates/sms --tags sms` | `Added document doc-004` | T4 |
| C05 | Abnormal | `list` thiếu `--node`; `--top-k 0`; `--file` không tồn tại | Exit 1; lỗi chỉ ở stderr; stdout rỗng; không lộ stack trace | T6, T9 |
| C06 | Abnormal | `kb retrieve doc-999` | Exit 1; `Document 'doc-999' not found` | T9 |
| C07 | Abnormal | `KB_CLIENT=http` thiếu `KB_API_URL` | Exit 1; thông báo có `KB_API_URL` | AC2, T9 |
| C08 | Normal | Cả 4 lệnh với `KB_CLIENT=http` → `KnowledgeBase.Api` | Exit 0; kết quả giống mock | AC2, AC3 |
| C09 | Abnormal | `KB_API_URL` trỏ tới cổng không có server | Exit 3; `Cannot reach KB API` | T5, T9 |
| C10 | Abnormal | Server trả HTTP 500 | Exit 3; thông báo có `500` | T5, T9 |
| C11 | Normal | `kb --help`, `kb <lệnh> --help` | Exit 0; có mô tả lệnh | T9 |
| C12 | Normal | `--verbose` với HTTP client | Log `POST .../search` ở stderr | T5 |

## 5. E2E — chạy chương trình thật

File: [KbProcessTests.cs](../tests/KnowledgeBase.IntegrationTests/EndToEnd/KbProcessTests.cs).

| Mã | Nhóm | Luồng | Kết quả mong đợi | Yêu cầu |
|---|---|---|---|---|
| E01 | Normal | `dotnet KnowledgeBase.Cli.dll` với `KB_CLIENT=http`: `add` → `search` → `retrieve` (3 tiến trình riêng) | Mọi lệnh exit 0; tài liệu thêm ở lần chạy đầu thấy được ở các lần sau | AC3 |
| E02 | Boundary | Không đặt biến môi trường nào, `search "response"` | Dùng mock mặc định; có doc-001 | AC2 |

## 6. KB API của project — `KnowledgeBase.Api`

Không được cung cấp KB API, nên project tự dựng `tools/KnowledgeBase.Api` theo đúng API contract trong `architecture.md`. Server chạy độc lập (`dotnet run --project tools/KnowledgeBase.Api`, mặc định `http://localhost:5080`), lưu dữ liệu vào file JSON, có dữ liệu KB theo cấu trúc trong `architecture.md`, và hỗ trợ token.

### 6.1 Hành vi riêng của server

File: [KbApiServerTests.cs](../tests/KnowledgeBase.IntegrationTests/Api/KbApiServerTests.cs). Server chạy với file dữ liệu thật trong thư mục tạm.

| Mã | Nhóm | Trường hợp | Kết quả mong đợi | Kỹ thuật |
|---|---|---|---|---|
| V01 | Normal | `add` → tắt server → bật lại với cùng file → `retrieve` | Tài liệu vẫn còn | EG |
| V02 | Boundary | Lần chạy đầu, chưa có file dữ liệu | Tự tạo file; `/templates/email` có doc-001..003, `/team/devops` doc-004..006, `/docs/guides` doc-007..008 | EP |
| V03 | Normal | Server đặt `KB_SERVER_TOKEN`, client gửi đúng token | Thành công | DT |
| V03 | Abnormal | Không gửi token / sai token | HTTP 401 → `KbApiException` có `401` | DT, EG |

### 6.2 Tích hợp với KB API chạy như dịch vụ bên ngoài (AC4)

File: [KbApiIntegrationTests.cs](../tests/KnowledgeBase.IntegrationTests/Integration/KbApiIntegrationTests.cs), [KbApiTarget.cs](../tests/KnowledgeBase.IntegrationTests/Integration/KbApiTarget.cs).

- Mặc định: test tự khởi động `KnowledgeBase.Api` thành **tiến trình riêng** (`dotnet KnowledgeBase.Api.dll`), lưu dữ liệu vào file — request đi qua mạng tới một server nằm ngoài tiến trình test.
- Có biến `KB_REAL_API_URL` (và `KB_REAL_API_TOKEN` nếu cần): cùng bộ test chạy với KB API ở URL đó, không cần sửa code.
- Mỗi test tự thêm một tài liệu có tên duy nhất vào node `/tests/smoke`, nên không phụ thuộc dữ liệu có sẵn trên server.

| Mã | Nhóm | Trường hợp | Yêu cầu |
|---|---|---|---|
| R01a | Normal | `add` rồi `retrieve` cùng tài liệu | AC4 |
| R01b | Normal | `search` tìm được tài liệu vừa thêm | AC4 |
| R01c | Normal | `list` node có tài liệu vừa thêm | AC4 |
| R01d | Abnormal | `retrieve` id không tồn tại → `KbDocumentNotFoundException` | AC4 |

**Lưu ý khi trình bày:** đây là KB API do project tự dựng theo contract, không phải KB API của MindX.

---

## 7. Đối chiếu yêu cầu

| Yêu cầu | Test |
|---|---|
| AC1 — truy vấn KB cho trường hợp thực tế (mẫu email, thông tin nhóm) | C01, C02, C03 |
| AC2 — mock và HTTP, chuyển bằng biến môi trường | U08, K01–K06 (× 2 client), C07, C08, E02 |
| AC3 — 4 lệnh end-to-end | C01–C04, C08, E01 |
| AC4 — tích hợp với KB API được test và có tài liệu | R01a–R01d (KB API ở tiến trình riêng, hoặc `KB_REAL_API_URL`); V01–V03; K01–K06 |
| T5 — HTTP client cùng hành vi, JSON, xử lý lỗi, log | U09–U14, K01–K06, C09, C10, C12 |
| T6 — test lỗi (input sai, thiếu file) | U01–U07, C05 |
| T9 — help text, thông báo lỗi rõ | C05, C06, C07, C11 |

## 8. Giả định về API contract

`architecture.md` chỉ ghi định dạng response của `/search`. Các response còn lại là giả định; `KnowledgeBase.Api` và `HttpKbClient` cùng tuân theo bảng này. Nếu sau này có KB API khác, kiểm tra lại bằng `curl`:

| Thao tác | Response giả định |
|---|---|
| `/search` | `{"results":[{"id","title","nodePath","matchType"}]}` — thêm `matchType` |
| `/list` | `{"documents":[{"id","title","nodePath"}]}` |
| `/retrieve` | `{"id","title","content","nodePath","tags"}`; không có → HTTP 404 |
| `/add` | `{"id","title","content","nodePath","tags"}` |
