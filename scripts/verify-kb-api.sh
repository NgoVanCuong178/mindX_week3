#!/usr/bin/env bash
# Kiểm tra CLI `kb` với một KB API bất kỳ đang chạy ở <KB_API_URL>, và ghi bằng chứng ra file Markdown.
#
#   scripts/verify-kb-api.sh <KB_API_URL> [file-báo-cáo.md]
#
# Biến môi trường (không bắt buộc):
#   KB_API_TOKEN      token của KB API; được gửi đi nhưng KHÔNG BAO GIỜ được ghi vào báo cáo
#   KB_TARGET_LABEL   mô tả server để ghi vào báo cáo, ví dụ "KB API của MindX (staging)"
#
# Script chạy 3 phần, tất cả đi qua mạng tới đúng URL đã cho:
#   1. curl gửi request thô theo API contract (kiểm tra server trước khi dùng CLI)
#   2. CLI `kb` thật với KB_CLIENT=http: search, list, add, rồi search/retrieve/list lại tài liệu vừa thêm
#   3. bộ test tích hợp R01a–R01d (dotnet test) với KB_REAL_API_URL = URL đã cho
# Tài liệu thêm vào nằm ở node /tests/smoke và không bị xoá (contract không có thao tác xoá).
# Exit code 0 khi mọi bước đều PASS.

set -u
export MSYS_NO_PATHCONV=1   # Git Bash: không đổi "/tests/smoke" thành đường dẫn Windows

URL="${1:?Usage: scripts/verify-kb-api.sh <KB_API_URL> [report.md]}"
URL="${URL%/}"
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
command -v cygpath > /dev/null && ROOT="$(cygpath -m "$ROOT")"   # Git Bash: C:/... cho dotnet
REPORT="${2:-$ROOT/docs/integration-evidence.md}"
TOKEN="${KB_API_TOKEN:-}"
LABEL="${KB_TARGET_LABEL:-(không ghi)}"
CLI="$ROOT/src/KnowledgeBase.Cli/bin/Debug/net9.0/KnowledgeBase.Cli.dll"
WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT
WORK_NATIVE="$WORK"
command -v cygpath > /dev/null && WORK_NATIVE="$(cygpath -m "$WORK")"

echo "Building..."
dotnet build "$ROOT" --nologo -v quiet > "$WORK/build.log" 2>&1 || { cat "$WORK/build.log"; exit 1; }

PASSED=0
FAILED=0
SUMMARY=""
DETAILS=""

# Ghi kết quả một bước. $1 tên bước, $2 lệnh, $3 mong đợi, $4 PASS/FAIL, $5 file chứa output.
record() {
  if [ "$4" = PASS ]; then PASSED=$((PASSED + 1)); else FAILED=$((FAILED + 1)); fi
  SUMMARY+="| $((PASSED + FAILED)) | $1 | $3 | **$4** |"$'\n'
  local output
  output="$(head -n 20 "$5")"
  [ -n "$TOKEN" ] && output="${output//"$TOKEN"/***}"
  DETAILS+=$'\n'"### $((PASSED + FAILED)). $1 — $4"$'\n\n'"\`$2\`"$'\n\n'"\`\`\`"$'\n'"$output"$'\n'"\`\`\`"$'\n'
  echo "[$4] $1"
}

# Gửi POST JSON thô bằng curl. In "HTTP <mã>" rồi body.
post() {
  local auth=()
  [ -n "$TOKEN" ] && auth=(-H "Authorization: Bearer $TOKEN")
  curl -s -m 10 -w '\nHTTP %{http_code}' -X POST "$URL/$1" -H "Content-Type: application/json" \
       ${auth[@]+"${auth[@]}"} -d "$2" 2>&1
}

# Chạy CLI với HTTP client. Ghi output vào $WORK/out, trả về exit code của CLI.
kb() {
  KB_CLIENT=http KB_API_URL="$URL" KB_API_TOKEN="$TOKEN" dotnet "$CLI" "$@" > "$WORK/out" 2>&1
}

# ---- 1. Request thô theo API contract
post search '{"query":"response","topK":3}' > "$WORK/out"
grep -q '^HTTP 200' "$WORK/out" && grep -q '"results"' "$WORK/out" && r=PASS || r=FAIL
record "curl POST /search" "POST /search {\"query\":\"response\",\"topK\":3}" 'HTTP 200, có "results"' $r "$WORK/out"

post list '{"nodePath":"/templates/email","limit":5}' > "$WORK/out"
grep -q '^HTTP 200' "$WORK/out" && grep -q '"documents"' "$WORK/out" && r=PASS || r=FAIL
record "curl POST /list" "POST /list {\"nodePath\":\"/templates/email\",\"limit\":5}" 'HTTP 200, có "documents"' $r "$WORK/out"

# ---- 2. CLI thật, KB_CLIENT=http
kb search "response" --top-k 3 && r=PASS || r=FAIL
record "kb search" 'kb search "response" --top-k 3' "exit 0" $r "$WORK/out"

kb list --node /templates/email --limit 5 && r=PASS || r=FAIL
record "kb list" "kb list --node /templates/email --limit 5" "exit 0" $r "$WORK/out"

UNIQUE="evidence$(date +%s)$RANDOM"
printf '# Integration evidence %s\n\nCreated by scripts/verify-kb-api.sh (%s).\n' "$UNIQUE" "$UNIQUE" > "$WORK/evidence.md"
kb add --file "$WORK_NATIVE/evidence.md" --path /tests/smoke --tags smoke-test && r=PASS || r=FAIL
NEW_ID="$(sed -n 's/^Added document //p' "$WORK/out" | tr -d '\r')"
[ -n "$NEW_ID" ] || r=FAIL
record "kb add" "kb add --file evidence.md --path /tests/smoke --tags smoke-test" "exit 0, in id mới" $r "$WORK/out"

kb search "$UNIQUE" && grep -q "$NEW_ID" "$WORK/out" && r=PASS || r=FAIL
record "kb search tài liệu vừa thêm" "kb search $UNIQUE" "exit 0, có $NEW_ID" $r "$WORK/out"

kb retrieve "$NEW_ID" && grep -q "$UNIQUE" "$WORK/out" && r=PASS || r=FAIL
record "kb retrieve tài liệu vừa thêm" "kb retrieve $NEW_ID" "exit 0, đúng nội dung" $r "$WORK/out"

kb list --node /tests/smoke --limit 100 && grep -q "$NEW_ID" "$WORK/out" && r=PASS || r=FAIL
record "kb list node /tests/smoke" "kb list --node /tests/smoke --limit 100" "exit 0, có $NEW_ID" $r "$WORK/out"

kb retrieve "doc-does-not-exist-$UNIQUE"; code=$?
[ $code -eq 1 ] && grep -q "not found" "$WORK/out" && r=PASS || r=FAIL
record "kb retrieve id không tồn tại" "kb retrieve doc-does-not-exist-…" "exit 1, \"not found\"" $r "$WORK/out"

# ---- 3. Bộ test tích hợp R01 với chính URL này
KB_REAL_API_URL="$URL" KB_REAL_API_TOKEN="$TOKEN" \
  dotnet test "$ROOT/tests/KnowledgeBase.IntegrationTests" --no-build --filter "FullyQualifiedName~R01" \
  --logger "console;verbosity=normal" > "$WORK/test.log" 2>&1 && r=PASS || r=FAIL
grep -E "(Passed|Failed) .*R01|Total tests|Passed:|Failed:" "$WORK/test.log" | sed -E 's/^ +//' > "$WORK/out"
record "dotnet test R01a–R01d" 'KB_REAL_API_URL=<url> dotnet test --filter "FullyQualifiedName~R01"' "4 test pass" $r "$WORK/out"

# ---- Báo cáo
COMMIT="$(git -C "$ROOT" rev-parse --short HEAD 2>/dev/null || echo unknown)"
DIRTY="$(git -C "$ROOT" status --porcelain 2>/dev/null | grep -q . && echo ' (có thay đổi chưa commit)')"
mkdir -p "$(dirname "$REPORT")"
{
  echo "# Bằng chứng kiểm thử tích hợp với KB API"
  echo
  echo "Sinh tự động bởi \`scripts/verify-kb-api.sh\`. Chạy lại để cập nhật."
  echo
  echo "| | |"
  echo "|---|---|"
  echo "| Thời điểm | $(date '+%Y-%m-%d %H:%M:%S %z') |"
  echo "| KB API | \`$URL\` |"
  echo "| Mô tả server | $LABEL |"
  echo "| Token | $([ -n "$TOKEN" ] && echo 'có gửi (không ghi vào báo cáo)' || echo 'không dùng') |"
  echo "| Mã nguồn | commit \`$COMMIT\`$DIRTY |"
  echo "| Kết quả | **$PASSED/$((PASSED + FAILED)) bước PASS** |"
  echo
  echo "## Tóm tắt"
  echo
  echo "| # | Bước | Mong đợi | Kết quả |"
  echo "|---|---|---|---|"
  printf '%s' "$SUMMARY"
  echo
  echo "## Chi tiết (tối đa 20 dòng output mỗi bước)"
  printf '%s' "$DETAILS"
} > "$REPORT"

echo
echo "$PASSED/$((PASSED + FAILED)) steps passed. Report: $REPORT"
[ "$FAILED" -eq 0 ]
