# WORKLOG — Hệ thống đặt sân cầu lông

> **Chủ tài liệu: Đinh Văn Tài** (github.com/taiDinh303) · **Đồng đội: Phùng Vĩnh Luân** (github.com/thankiemsama).
> File này là **điểm bắt đầu cho bất kỳ AI/dev** làm tiếp. Cập nhật sau mỗi lượt có thay đổi thật.
> Ghi sự thật đã kiểm chứng; **không đánh dấu "xong" khi chưa kiểm tra**. Quy tắc đọc/ghi: xem `00-README.md` §Rules.

## 1. Trạng thái hiện tại — 20/10/2026, UTC+7

- CURRENT .NET 8/SQL Server/JWT BCrypt + React 19/Vite/TypeScript. Có 17 bảng, waitlist/notify/check-in token/membership và signed gateway adapters trong source mới pull.
- Theo yêu cầu chuẩn hóa toàn bộ docs/ASP.NET/React, đã áp dụng patterns Marixa nhưng giữ policy/route/schema/danh tính Badminton; không dùng tài nguyên hoặc thông tin hosting Marixa.
- Layering: API Configuration/Bootstrap/Security, repository DI và mapping lỗi provider; contracts/business không tham chiếu implementation persistence, EF/SqlClient/HttpContext. ICurrentUser là ranh giới identity.
- Production config fail closed, startup không migrate/demo seed; check-config không side effect. Publish bỏ environment JSON, manifest public chưa có host live.
- React app/modules/services/utils/theme; lazy routes, shared nav/sidebar/header/mobile menu, clear cache theo user và 401 reset. Giao diện giữ nội dung đặt sân, xanh/vàng thể thao.
- Local backend 83 test và frontend Node 11/Playwright 8 đạt, bằng chứng §4; không đồng nghĩa gateway/cloud/worker đa instance đã nghiệm thu.

## 2. Lịch sử ghi nhận 14/10/2026 — chưa xác minh lại runtime

> Giữ nội dung lịch sử bên dưới để truy vết. Các khẳng định coverage, clock, scope, restore và k6 ở bản ghi cũ đã được giới hạn/đính chính bởi §1, §8 và tài liệu chuyên trách; không dùng làm bằng chứng kiểm tra hiện tại.

- **Múi giờ (critical fix):** mọi deadline (guard +30p, refund 2h/1h/0%, pending-expiry, availability) chuyển `DateTime.UtcNow` → `HanoiTime` tại `Core/Utils/HanoiTime.cs`. Trước đó khách đặt được slot đã qua + tỷ lệ hoàn lệch ±7h.
- **State machine:** `BookingService.MarkCompletedAsync/MarkNoShowAsync` + `AutoCompleteJob` (Confirmed qua slot end → Completed) + endpoint `POST /bookings/{id}/complete` & `/no-show`.
- **No-show gate:** `User.NoShowCount/NoShowPeriod/NoShowLimited/NoShowLimitedPeriod`; 3 lần/tháng → chặn đặt tháng kế; `NoShowResetJob` tự mở; kiểm tra trong `BookingService.CreateAsync`.
- **Branch-scope:** `PaymentService.AuthorizeStaffScope` + `MaintenanceService` chặn manager thao tác cơ sở khác.
- **Redeem điểm:** `PointService.RedeemAsync/RestoreAsync` (LedgerKey idempotent), `Booking.PointsRedeemed`, hoàn full → restore điểm.
- **Quản lý nhân viên:** `StaffService` + `POST /api/v1/admin/staff/*` (tạo/lock/unlock/đổi role/gán cơ sở), guard "đúng 1 admin active" (`ux_active_admin`).
- **Bảo mật API:** CORS (`Cors:AllowedOrigins`), `AddRateLimiter` (global + bucket `auth` 5/60s), lockout login 5 sai/10p (in-memory), `[Authorize]` cho `GET /vouchers/{code}`.
- **Sửa kỹ thuật:** `GetByUserPagedAsync` (push enum vào SQL), `IsSlotConflict` (SqlException.Number 2601, không parse message), dọn `RefundLocks` (chống leak), audit filter Hanoi→UTC boundary.
- **Database:** migration `20261014063758_AddNoShowTrackingAndPointsRedeemed` (4 cột User + 1 cột Booking). `BadmintonBooking.sql` dev cập nhật.
- **Test:** thêm `TimezoneGuardTests` (3) + `LifecycleAndRedeemTests` (5); `TestHost` seed 2 branch + manager 2 cơ sở + slot 09:40, pin `HanoiTime.NowUtc`. → **23/23 pass**.
- **Load test:** `loadtest/concurrency_booking.js` (k6, 50 VU cùng slot, assert 1 winner + 49 SLOT_CONFLICT; + login storm 1000 VU + dashboard fan-out).

## 3. Ma trận chức năng CURRENT

| Khu vực | Có source / kiểm chứng local | Giới hạn |
| --- | --- | --- |
| Auth | SĐT/BCrypt/JWT, guard role và active user | Chưa OTP/refresh recovery đầy đủ |
| Booking | Active slot unique, window/limit, owner/key binding, price snapshot | Chưa full payload hash/concurrent balance hardening |
| Payment/cancel | Mock/real adapters, on-site, CancelRefund, chữ ký/replay tests | Chưa sandbox/live/đối soát crash |
| Points/voucher | Earn Completed, full refund restore, redemption/ledger tests | Kiểm tiếp nhiều instance/balance race |
| Manager scope | List/summary/detail/payment-read/lifecycle/check-in shared guard | Full endpoint role matrix release chưa hoàn tất |
| Groups/review | Owner booking/review Completed; UI module | Upload ảnh chưa có |
| Waitlist/notify | Queue/promotion/notification ledger và test stub/log | Chưa chứng minh gửi ngoài/đa instance |
| Membership | Tier/config multiplier/admin UI có source | Không tự xác nhận bán gói subscription |
| QR/check-in | Server token/API/branch scope và UI nhập mã | Tile chưa QR scan chuẩn |
| Jobs/health | 5 worker SQL applock/heartbeat, health/ready/metrics | Chưa load/restart/crash rehearsal live |
| React | Module/routes/API/theme/nav/session; browser mock | Không thay HTTP/DB/payment acceptance |
| Cloud/Storage/Postgres | Chưa tích hợp hoặc chưa xác minh | Dự án cutover riêng |

## 4. Bằng chứng kiểm tra

### 20/10/2026 — chuẩn hóa toàn stack theo Marixa

- Rà source Marixa Configuration/Bootstrap/persistence DI/theme/modules và Badminton checkout 2bb3d87; không thay source Marixa.
- SDK 9.0.317, Node 24.21.0, SQL Server local default instance '.'; fixture tạo/xóa BadmintonTest_* riêng.
- Backend Release build, architecture/config/service/SQL/HTTP checks; EF model check không thay migration/schema.
- npm test: 11 pass; lint/build đạt; Playwright: 8 pass, mocked API. Đã xem ảnh mobile manager, desktop screenshot được tạo qua flow login.
- Production publish + scripts/Test-ProductionConfiguration.ps1: đạt, valid dry-run/invalid fail-closed, không Development JSON, không DB/network/worker.
- CONFIGURATION_OK Development qua check-config; CiGuard đạt.
- Test giá cũ phụ thuộc weekday bị fail vào cuối tuần; sửa expected theo phụ thu thật, không đổi chính sách. VNPAY parser dùng System.Web.HttpUtility để Services không cần ASP.NET WebUtilities; signature tests kiểm hồi quy.
- Kiểm cuối: dotnet build Backend/Badminton.slnx -c Release --no-restore -warnaserror đạt 0 warning/error; dotnet test Backend/Tests/Badminton.Tests.csproj -c Release --no-build --no-restore với BADMINTON_TEST_SQLSERVER=.: 83 pass, 0 fail/skip, gồm vòng HTTP đặt → on-site → hủy/refund.
- EF --configuration Release --no-build has-pending-model-changes: No changes have been made to the model since the last migration. 4 migration giữ nguyên; không áp migration vào DB ứng dụng.
- npm test 11 pass, npm run lint không warning, npm run build đạt; bundle entry gzip khoảng 109.5–109.7 KB, các feature lazy-load, không còn warning chunk >500 KB.
- Playwright 8 pass; đã xem ảnh desktop customer và mobile manager. Ảnh dùng dữ liệu test, không commit artifact. HTTP tests real/local và browser mock được ghi riêng.
- python scripts/Validate-Repository.py: 21 Markdown, 128 link, 33 bảng/project reference, 0 lỗi; CiGuard 5/5 đạt. git diff --check trước commit.
- Chưa chạy gateway sandbox/live, tải k6, cloud, upload Storage hoặc worker crash/multi-instance rehearsal.


### Lần chuẩn hóa docs 16/10/2026

- Đọc cấu trúc/mẫu docs Marixa và source/config Badminton; đối chiếu controller/service/mapping/test/CSS/route, không dùng hạ tầng Marixa làm snapshot Badminton.
- Xác nhận tĩnh: 15 bảng mapping, 23 Fact; SDK máy rà soát 9.0.317, Node 24.21.0.
- Chưa chạy dotnet build/test, npm build/lint, k6, API, browser hoặc cloud. Nhiệm vụ chỉ Markdown; không báo kết quả ứng dụng mới.
- Kiểm tra 15 file Markdown: một H1/file, code fence cân bằng, UTF-8/không ký tự lỗi, đường dẫn link nội bộ tồn tại; git diff --check -- docs không báo lỗi. Kiểm tra cuối: 105 liên kết nội bộ (kèm anchor), 45 bảng, 15 file đều đạt; 0 lỗi cấu trúc/liên kết; git diff --check -- docs sạch.

### Kết quả lịch sử trong bản ghi cũ

Ngày 14/10 ghi build 0 warning/error, 23 test pass với SQL Express; chưa có log runtime mới. Lệnh cũ dùng thư mục Badminton/ hiện không còn phù hợp. Lệnh hiện hành ở [10-setup-local.md](10-setup-local.md). Lượt chuẩn hóa tên tiếp theo đã sửa script/CI và xác minh local; xem sự kiện mới nhất.

## 5. Việc tiếp theo

[08-plan.md](08-plan.md) là backlog/tiêu chí chuẩn; [14-release-checklist.md](14-release-checklist.md) là gate. Ưu tiên sandbox payment/đối soát, full idempotency/balance race, báo cáo theo tiền/kỳ, migration/restore, worker đa instance và QR chuẩn. Không dùng cấu trúc sạch/build xanh như xác nhận production.

## 6. Log sự kiện (mới trên cùng)

### 20/10/2026 — Chuẩn hóa docs, ASP.NET và React theo kiến trúc tham khảo

- Tách repository DI/unique error; Core ICurrentUser và API adapter; pure StartupConfiguration, DatabaseInitializer, health endpoints, five-job flag, per-IP limiter và safe publish.
- Sửa pin SDK không có trên máy/CI từ 10.0.401 sang SDK9/latestFeature, giữ target net8.0; Node24/config validator/env examples cùng manifest.
- React chuyển pages sang feature modules, tách transport/types/format/theme/app providers/routes/navigation. Giữ facade lib/api tương thích; desktop/mobile cùng registry, thêm staff menu/skip link, expired session và cache isolation.
- Bổ sung scope server cho manager list/summary/detail/payment read/lifecycle/check-in, active user create guard, retry actor/slot binding, stable historical amount. Tiền snapshot gồm phụ thu, không bịa split chưa lưu.
- Đồng bộ docs 00–14/glossary/runbooks và onboarding/AGENTS/architecture. Gate chưa đạt vẫn mở, không tự deploy hoặc dùng dịch vụ thật.


### 16/10/2026 — Chuẩn hóa tên backend và push toàn bộ theo yêu cầu

- Chuyển cấu trúc Backend đang có vào Git; API/Badminton.API.csproj, namespace Badminton.API; Tests/Badminton.Tests.csproj; Core namespace Badminton.Core; Context/BadmintonBookingDbContext.cs. Solution groups và RootNamespace đồng bộ.
- Giữ tên các tầng và database/migration/API contract; không sửa các lỗi nghiệp vụ SEC/PAY/REP trong nhiệm vụ đổi tên.
- Sửa build.ps1 dùng path theo script, chỉ dừng API khi -StopApi; cập nhật CI SDK 9 + runtime 8, EF tool manifest 8.0.28, thêm lint/build frontend. global.json chọn SDK 9.0.300/latestFeature (máy dùng 9.0.317).
- Cập nhật docs và helper loadtest theo đường dẫn mới; design-time factory hỗ trợ env chuẩn cùng alias legacy. Không chạy helper reset DB/load test.
- dotnet build Backend/Badminton.slnx -c Debug: đạt, 0 error, 1 warning CS1998 có sẵn trong TimezoneGuardTests.
- BADMINTON_TEST_SQLSERVER=. dotnet test Backend/Tests/Badminton.Tests.csproj -c Debug --no-build --no-restore: 23 passed, 0 failed/skipped.
- dotnet tool restore: đạt; EF has-pending-model-changes: No changes have been made to the model since the last migration.
- npm ci, npm run lint, npm run build: đạt. Vite cảnh báo chunk chính >500 KB minified (gzip 170.23 KB), chưa tối ưu bundle trong nhiệm vụ này. build.ps1 đạt (incremental 0 warning/error); full build trước đó có warning CS1998 như ghi trên.
- Kiểm 110 link nội bộ, table/fence/H1, solution/ProjectReference và JSON config: 0 lỗi; PowerShell parser cho build.ps1/start_api.ps1 đạt. Git diff --check kiểm trước commit.
- File cấu hình Visual Studio *.user không được đưa lại vào Git ở cấu trúc mới; giữ local theo .gitignore. Node_modules/dist/bin/obj không được commit.
- Chưa chạy smoke API/browser/cloud hoặc load k6; CI remote chưa xác minh. Các P0 nghiệp vụ vẫn mở.


### 16/10/2026 — Chuẩn hóa bổ sung theo bộ docs Marixa, dành riêng Badminton

- Người dùng cấp phạm vi hoàn thiện toàn bộ Markdown. Thay quy tắc đặc tả đóng băng bằng bảo trì docs trong phạm vi được giao.
- Bổ sung 08-plan, 09-deploy-detail, 10-setup-local, 11-setup-cloud, 12-figma-sync; hoàn thiện các file hiện hữu và glossary, giữ đường dẫn/tên cũ còn dùng.
- Phân biệt CURRENT/TARGET/PENDING/NOT VERIFIED; sửa path Backend, đơn vị điểm, quan hệ voucher, giới hạn unique admin, QR minh họa, rate-limit/correlation, k6 request count/assertion và migration/test coverage.
- Ghi sai khác ứng dụng bằng mã SEC/PAY/DATA/REP/DEV; không sửa code hoặc đổi chính sách chưa chốt để hợp thức lỗi.
- Giữ lịch sử/bản ghi trước và mọi thay đổi local ngoài docs; chưa commit/push. Kiểm tra cấu trúc/liên kết/diff docs; không chạy suite ứng dụng.


### 16/10/2026 — Viết lại & hoàn thiện bộ tài liệu docs/
- **Viết mới `00-README.md`** (entry point, quick facts dev-baseline vs production-target, rules) theo mẫu Marixa.
- **Hoàn thiện `01`–`07` + `glossary.md`** cho khớp **code đang chạy thật** (SQL Server, int PK, JWT/BCrypt, IHostedService, mock gateway, React 19 + Tailwind v4), tách rõ **dev baseline vs production target** ở mọi file. Không còn mơ hồ Supabase/uuid/Hangfire như bản cũ.
- **Đổi tên `06-deployment-vercel-supabase.md` → `06-deployment.md`** (Part A dev local + Part B production). Xóa file `06-deployment-vercel-supabase.md` cũ.
- **Cập nhật `worklog.md`** (file này) làm điểm bắt đầu chính xác cho AI/dev.
- Chỉ chỉnh Markdown trong `docs/`; không sửa code.

### 14/10/2026 — (bản trước, giữ nguyên) Múi giờ HanoiTime, state machine, no-show gate, branch-scope, redeem điểm, quản lý nhân viên, bảo mật API, k6 script; 23/23 test pass.

## 7. Quy tắc cập nhật worklog

- Mỗi lượt: ghi ngày + thay đổi thật + bằng chứng; chuyển việc xong khỏi §5.
- Bug: triệu chứng + cách tái hiện + mức ảnh hưởng + trạng thái; khi hết ghi cách sửa + ngày đóng.
- Bảo trì docs cùng nhiệm vụ được giao theo [00](00-README.md). Chính sách chưa chốt ghi đề xuất/tác động ở [08](08-plan.md), không tự coi là quyết định.

## 8. Giới hạn còn mở

| Khu vực | Còn cần làm |
| --- | --- |
| Payment | Signed adapter/stub test chưa là sandbox certification; xử lý timeout/crash và đối soát ngoài |
| Idempotency | Chưa hash đầy đủ voucher/points/note; unique key toàn hệ thống cần policy retention |
| Điểm | Concurrent balance giữa nhiều đơn; clawback theo tier/điểm đã tiêu cần mở rộng tests |
| Reporting | Thống nhất ngày chơi vs ngày PaidAt/refund, UTC boundary/refund-only day và denominator bảo trì |
| Role/slot | TimeSlot POST/PATCH cho Manager còn khác mục tiêu Admin cấu hình |
| QR/Storage | Token có API, tile chưa QR chuẩn camera; chưa upload/policy ảnh |
| Jobs | App lock/heartbeat có source; chưa chứng minh mọi crash window không mất ledger/notify |
| Operations | RPO/RTO/retention/host/plan/metrics ingress chưa chốt; không xác nhận cloud |

Chi tiết ở [08](08-plan.md), [13](13-configuration.md), [14](14-release-checklist.md). Giữ lịch sử §2/§4/§6 theo ngày, không đọc bản ghi 16/10 như hiện trạng 20/10.
