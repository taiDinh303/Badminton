# 05 — Kiểm thử và bằng chứng nghiệm thu

> Chủ tài liệu: **Đinh Văn Tài**. Cập nhật: **20/10/2026**. Kết quả hiện hành ở [worklog](worklog.md); không dùng lịch sử 23 test để đại diện suite mới.

## 1. Mức bằng chứng

| Mức | Công cụ / phạm vi | Không tự chứng minh |
| --- | --- | --- |
| Config/architecture | xUnit configuration synthetic, reflection | DB/gateway kết nối thật |
| Service/DB | TestHost DI + SQL Server/PostgreSQL loopback BadmintonTest_* | Routing/JWT/browser |
| HTTP/DB | Kestrel loopback + JWT/controllers + DB fixture | Payment sandbox/cloud |
| Frontend pure | Node test env/date/currency | UI hoặc server |
| Browser mock | Playwright built SPA, mock API | DB/gateway thật |
| Browser tích hợp | Built SPA + Kestrel + BCrypt/JWT + DB fixture, không mock API | Gateway/cloud thật |
| Load/cloud | Script hiện hữu, chưa nghiệm thu | Không suy p95/production từ build |

## 2. Backend

[Tests](../Backend/Tests) bao gồm slot/refund concurrency, webhook replay/signature, voucher, lifecycle/points, timezone, notify/waitlist/check-in/membership và cancel-refund. ConfigurationAndArchitectureTests kiểm key/CORS/init/provider/notify, thông báo không lộ secret và dependency không tham chiếu ngược persistence/HTTP.

HttpBookingFlowTests dùng JWT thật: tạo/đọc đơn; chặn anonymous/customer khác/manager khác branch; retry key; snapshot giá; readiness khi jobs tắt. Host không gọi initializer/worker. HTTP fixture dùng migrations thật; các service fixture SQL mặc định EnsureCreated, PostgreSQL luôn migrations. Gateway tests dùng stub, không thu tiền/gửi SMS thật.

Root repo:

```powershell
dotnet tool restore
dotnet build Backend/Badminton.slnx -c Release -warnaserror
$env:BADMINTON_TEST_SQLSERVER = '.'
dotnet test Backend/Tests/Badminton.Tests.csproj -c Release --no-build
dotnet ef migrations has-pending-model-changes --context BadmintonBookingDbContext --project Backend/Repositories --startup-project Backend/API --configuration Release --no-build
dotnet ef migrations has-pending-model-changes --context PostgresBookingDbContext --project Backend/Repositories --startup-project Backend/API --configuration Release --no-build
```

Chỉ chọn instance local/test có quyền tạo/xóa BadmintonTest_*. Máy kiểm dùng default instance '.', CI dùng LocalDB. EF drift check không áp migration hoặc chứng minh dữ liệu đã chuyển.

## 3. Frontend

Từ Frontend: npm ci → npm test → npm run lint → npm run build → npx --no-install playwright install chromium → npm run test:e2e.

Env test chặn URL/credential/query sai; date test kiểm UTC qua ngày Việt Nam. Browser test kiểm login, booking, waitlist, mobile manager menu, admin-only route và expired session. Ảnh/test-results bị ignore. Mock browser và HTTP/DB test là bằng chứng riêng.

## 4. Ma trận cần giữ

| Nhóm | Ca ưu tiên |
| --- | --- |
| Quyền | JWT/owner/branch/null branch, user khóa |
| Giá | Peak/low/cuối tuần; đổi giá không đổi đơn lịch sử |
| Retry | Actor/payload khác; unique slot; webhook lặp/đảo thứ tự |
| Hoàn/điểm | 100/50/0%, fail/crash, full restore, earn Completed/tier/ledger |
| Jobs | Hết hạn/no-show/waitlist qua restart/nhiều instance |
| Config | Check-config không DB/jobs/network, publish không Development JSON |
| UI | Mobile/desktop, keyboard/focus, loading/empty/error/conflict/cache |

Test redeem cũ giả định ngày thường đã sửa expected theo cuối tuần thật của fixture; không đổi chính sách giá để làm test xanh. K6 burst 50 request/giây trong 10 giây không phải đúng 50 request; cần counter/hậu kiểm DB và phân loại 409/429 trước kết luận tải.

## 5. Gate và ghi bằng chứng

Ghi ngày/commit/SDK/Node/provider/lệnh/pass/fail/skip/artifact/giới hạn. Không coi build là acceptance live. Gateway sandbox, migration/restore, tải nhiều instance và các điểm chưa chốt phải đạt [14](14-release-checklist.md) trước dùng thật.

## 6. Bộ kiểm thử tích hợp mới — CURRENT 24/10/2026

ProjectQualityTests kiểm giá không đổi theo người, quote không giữ voucher/điểm, sức chứa, legacy overlap, group concurrent join, registration BCrypt limits, receipt dates/refund-only days, occupancy/maintenance và retry notification. HttpBookingFlowTests kiểm quyền ghi slot, quote contract, migration readiness và quản trị cơ sở. Ngày UTC lưu dạng Unspecified trên SQL được diễn giải là UTC.

Chạy từ Frontend sau npm run build và cài Chromium: `npx tsc -p tsconfig.e2e.json --noEmit`, `npx playwright test`, rồi `npx playwright test --config playwright.integration.config.ts`. Suite tích hợp dùng Backend/BrowserTests riêng, cần env BADMINTON_TEST_SQLSERVER hoặc BADMINTON_TEST_POSTGRES local, tự tạo/xóa BadmintonTest_*. Không dùng VITE_API_URL remote/local overrides cho suite này: built SPA cần same-origin /api/v1. Hạ host bằng test teardown; không chạy chung suite tích hợp trên cùng cổng5099.

CI không có bước deploy. Docker build/runner GitHub là mức kiểm riêng, không suy ra từ local Windows. Command/count cuối cùng ở worklog.
