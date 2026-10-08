# Bằng chứng kiểm thử tích hợp với KB API

Sinh tự động bởi `scripts/verify-kb-api.sh`. Chạy lại để cập nhật.

| | |
|---|---|
| Thời điểm | 2026-10-08 07:24:10 +0700 |
| KB API | `http://127.0.0.1:5096` |
| Mô tả server | KnowledgeBase.Api tự dựng của project: bản publish chạy như dịch vụ riêng ngoài repo, bật token (KHÔNG phải KB API của MindX) |
| Token | có gửi (không ghi vào báo cáo) |
| Mã nguồn | commit `678f5fc` (có thay đổi chưa commit) |
| Kết quả | **10/10 bước PASS** |

## Tóm tắt

| # | Bước | Mong đợi | Kết quả |
|---|---|---|---|
| 1 | curl POST /search | HTTP 200, có "results" | **PASS** |
| 2 | curl POST /list | HTTP 200, có "documents" | **PASS** |
| 3 | kb search | exit 0 | **PASS** |
| 4 | kb list | exit 0 | **PASS** |
| 5 | kb add | exit 0, in id mới | **PASS** |
| 6 | kb search tài liệu vừa thêm | exit 0, có doc-013 | **PASS** |
| 7 | kb retrieve tài liệu vừa thêm | exit 0, đúng nội dung | **PASS** |
| 8 | kb list node /tests/smoke | exit 0, có doc-013 | **PASS** |
| 9 | kb retrieve id không tồn tại | exit 1, "not found" | **PASS** |
| 10 | dotnet test R01a–R01d | 4 test pass | **PASS** |

## Chi tiết (tối đa 20 dòng output mỗi bước)

### 1. curl POST /search — PASS

`POST /search {"query":"response","topK":3}`

```
{"results":[{"id":"doc-001","title":"Customer Response Template","nodePath":"/templates/email","matchType":"title"},{"id":"doc-008","title":"Ticket Handling Guide","nodePath":"/docs/guides","matchType":"content"}]}
HTTP 200
```

### 2. curl POST /list — PASS

`POST /list {"nodePath":"/templates/email","limit":5}`

```
{"documents":[{"id":"doc-001","title":"Customer Response Template","nodePath":"/templates/email"},{"id":"doc-002","title":"Incident Acknowledgement Template","nodePath":"/templates/email"},{"id":"doc-003","title":"Email Templates README","nodePath":"/templates/email"}]}
HTTP 200
```

### 3. kb search — PASS

`kb search "response" --top-k 3`

```
ID        MATCH    NODE                TITLE
doc-001   title    /templates/email    Customer Response Template
doc-008   content  /docs/guides        Ticket Handling Guide
```

### 4. kb list — PASS

`kb list --node /templates/email --limit 5`

```
ID        NODE                TITLE
doc-001   /templates/email    Customer Response Template
doc-002   /templates/email    Incident Acknowledgement Template
doc-003   /templates/email    Email Templates README
```

### 5. kb add — PASS

`kb add --file evidence.md --path /tests/smoke --tags smoke-test`

```
Added document doc-013
```

### 6. kb search tài liệu vừa thêm — PASS

`kb search evidence179141904722099`

```
ID        MATCH    NODE                TITLE
doc-013   title    /tests/smoke        Integration evidence evidence179141904722099
```

### 7. kb retrieve tài liệu vừa thêm — PASS

`kb retrieve doc-013`

```
ID:    doc-013
Title: Integration evidence evidence179141904722099
Node:  /tests/smoke
Tags:  smoke-test

# Integration evidence evidence179141904722099

Created by scripts/verify-kb-api.sh (evidence179141904722099).
```

### 8. kb list node /tests/smoke — PASS

`kb list --node /tests/smoke --limit 100`

```
ID        NODE                TITLE
doc-009   /tests/smoke        Integration evidence evidence179141902111735
doc-010   /tests/smoke        Week 3 smoke test smoke8e951a080caf
doc-011   /tests/smoke        Week 3 smoke test smokeb5a451c51264
doc-012   /tests/smoke        Week 3 smoke test smokee3bc1266c63a
doc-013   /tests/smoke        Integration evidence evidence179141904722099
```

### 9. kb retrieve id không tồn tại — PASS

`kb retrieve doc-does-not-exist-…`

```
Document 'doc-does-not-exist-evidence179141904722099' not found
```

### 10. dotnet test R01a–R01d — PASS

`KB_REAL_API_URL=<url> dotnet test --filter "FullyQualifiedName~R01"`

```
Passed KnowledgeBase.IntegrationTests.Integration.KbApiIntegrationTests.R01a_AddThenRetrieve_RoundTrips [63 ms]
Passed KnowledgeBase.IntegrationTests.Integration.KbApiIntegrationTests.R01d_Retrieve_UnknownId_ThrowsNotFound [5 ms]
Passed KnowledgeBase.IntegrationTests.Integration.KbApiIntegrationTests.R01c_List_ContainsAddedDocument [8 ms]
Passed KnowledgeBase.IntegrationTests.Integration.KbApiIntegrationTests.R01b_Search_FindsAddedDocument [14 ms]
Total tests: 4
Passed: 4
```
