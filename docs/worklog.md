# WORKLOG — Hệ thống đặt sân cầu lông

> **Chủ tài liệu: Đinh Văn Tài** (github.com/taiDinh303) · **Đồng đội: Phùng Vĩnh Luân** (github.com/thankiemsama).
> File này là **điểm bắt đầu cho bất kỳ AI/dev** làm tiếp. Cập nhật sau mỗi lượt có thay đổi thật.
> Ghi sự thật đã kiểm chứng; **không đánh dấu "xong" khi chưa kiểm tra**. Quy tắc đọc/ghi: xem `00-README.md` §Rules.

## 1. Trạng thái hiện tại — rà source 16/10/2026, UTC+7

- CURRENT: .NET 8/EF SQL Server/JWT BCrypt/BackgroundService/mock payment + React 19/Vite/Tailwind 4. Backend nằm trong Backend/, frontend trong Frontend/.
- Tìm thấy 23 phương thức Fact ở 6 file test; chưa chạy build/test/API/browser/live trong nhiệm vụ sửa tài liệu này. Kết quả 23/23 của bản ghi 14/10 là lịch sử, chưa xác minh lại.
- Suite dùng DI/service và SQL Server thật, không phải suite HTTP middleware/controller. TestHost EnsureCreated không kiểm migration nâng cấp.
- Production/PostgreSQL/OTP/Storage/gateway thật/scheduler phân tán chưa tích hợp; chưa xác nhận hosting Badminton live.
- Rà source phát hiện P0 quyền hủy, scope manager, hủy–hoàn tiền, báo cáo; QR chỉ tile minh họa. Chi tiết và backlog chuẩn ở [08-plan.md](08-plan.md).
- Có thay đổi local ngoài docs (cấu trúc Backend thay đường dẫn Badminton cũ); nhiệm vụ này bảo toàn, không chỉnh mã nguồn/build script/CI.

| Quyết định lịch sử | Hiện trạng rà source | Giới hạn |
| --- | --- | --- |
| HanoiTime UTC+7 | Có helper và guard/refund/auto-complete | ReportService còn DateTime.Now; không tuyên bố mọi thời gian đã chuẩn hóa |
| Completed/NoShow | Có endpoint/job | Chưa scope/thời điểm đầy đủ; DEC-03 |
| Manager một cơ sở | Có scope một số thao tác payment/maintenance | Chưa toàn API; SEC-02 |
| Redeem điểm | Có key idempotent, 1.000 điểm=20.000 VND | Earn/restore khác spec; DEC-01/02 |
| CQRS/MediatR | Chưa có | Service/repository là baseline |

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

## 3. Ma trận chức năng (đã có / chưa có)

> ✅ có code, không đồng nghĩa runtime pass · 🟡 một phần/sai khác · ❌ chưa có. Source path tính từ Backend trừ Frontend. P0 ở 08-plan là nguồn ưu tiên hiện hành.

| Module / chức năng | Trạng thái | Code chính | Ghi chú |
| --- | --- | --- | --- |
| Auth (đăng nhập/đăng ký sđt) | ✅ (dev BCrypt) | `Services/Auth/*`, `POST /auth/*` | Prod = Supabase OTP |
| Đặt chỗ (idempotent, anti-overbooking) | 🟡 | `BookingService`, index `ux_court_slot_active` | |
| Thanh toán (init, on-site, webhook, refund) | 🟡 (mock gateway) | `PaymentService`, `PaymentGateways.cs` | Real adapter = việc #4 |
| Voucher (percent cap/fixed, min_spend, 1 lần) | ✅ | `VoucherService`, `VoucherExpiryJob` | |
| Điểm (1 VND=1 điểm, redeem/restore, ledger) | 🟡 | `PointService`, `PointLedger` | |
| Nhóm chơi (owner-only, max 10) | ✅ | `GroupService`, `BookingPlayer` | |
| Đánh giá (completed-only, rolling avg) | ✅ | `ReviewService`, `Reviews.BookingId` unique | |
| Bảo trì sân (lock slot, không tự hủy) | ✅ | `MaintenanceService`, `CourtDateStatus` | |
| No-show gate (3 lần/tháng) | ✅ | `NoShowResetJob`, `User.NoShow*` | |
| Nhân viên (CRUD, lock, role, gán cơ sở) | ✅ | `StaffService`, `/admin/staff/*` | |
| Báo cáo (summary/revenue/occupancy) | 🟡 | `ReportService`, `/admin/report/*` | |
| Audit (append-only, before/after) | 🟡 | `AuditService`, `AuditLogs` | |
| **Dashboard UI no-show + complete/no-show + redeem** | 🟡 | `Frontend/pages/admin/*` | API có, UI bổ sung = việc #5 |
| **Gateway thật (VnPay/MoMo/Zalopay signed)** | ❌ | mock dev | Việc #4 |
| **Cutover Postgres + RLS + Supabase Auth** | ❌ | `UseSqlServer` dev | Việc #3 (P0) |
| **Hangfire (thay IHostedService)** | ❌ | IHostedService dev | Việc P1 |
| **Redis rate-limit/lockout** | ❌ | in-memory dev | P1 |
| **Offline queue (LocalStorage/IndexedDB)** | ❌ | chưa build | P2 |
| **Load test chạy thật (p95)** | 🟡 | k6 script có, chưa chạy | Việc #1 |

## 4. Bằng chứng kiểm tra

### Lần chuẩn hóa docs 16/10/2026

- Đọc cấu trúc/mẫu docs Marixa và source/config Badminton; đối chiếu controller/service/mapping/test/CSS/route, không dùng hạ tầng Marixa làm snapshot Badminton.
- Xác nhận tĩnh: 15 bảng mapping, 23 Fact; SDK máy rà soát 9.0.317, Node 24.21.0.
- Chưa chạy dotnet build/test, npm build/lint, k6, API, browser hoặc cloud. Nhiệm vụ chỉ Markdown; không báo kết quả ứng dụng mới.
- Kiểm tra 15 file Markdown: một H1/file, code fence cân bằng, UTF-8/không ký tự lỗi, đường dẫn link nội bộ tồn tại; git diff --check -- docs không báo lỗi. Kiểm tra cuối: 105 liên kết nội bộ (kèm anchor), 45 bảng, 15 file đều đạt; 0 lỗi cấu trúc/liên kết; git diff --check -- docs sạch.

### Kết quả lịch sử trong bản ghi cũ

Ngày 14/10 ghi build 0 warning/error, 23 test pass với SQL Express; chưa có log runtime mới. Lệnh cũ dùng thư mục Badminton/ hiện không còn phù hợp. Lệnh hiện hành ở [10-setup-local.md](10-setup-local.md). Build script/CI còn path cũ, DEV-01 vẫn mở.

## 5. Việc tiếp theo

Nguồn duy nhất quản lý backlog/ưu tiên/tiêu chí đóng: [08-plan.md §3](08-plan.md#3-backlog-và-phụ-thuộc). P0 đầu tiên là quyền hủy và manager scope, hủy–hoàn, giá lịch sử/báo cáo, sửa path CI và kiểm thử thực. Không bắt đầu load lớn trên ứng dụng chưa đóng lỗi quyền/tiền.

## 6. Log sự kiện (mới trên cùng)

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

## 8. Vấn đề/rủi ro đang mở — bằng chứng source

| ID | Bằng chứng | Ảnh hưởng / trạng thái |
| --- | --- | --- |
| SEC-01 | BookingService.AuthorizeCancel không throw sau guard owner/group | Actor khác có thể đi tiếp; chưa sửa code |
| SEC-02 | GetAllPagedAsync/ReportService dùng branchId caller; lifecycle thiếu scope; payment read chỉ IsStaff | Không chứng minh manager bị giới hạn cơ sở; chưa sửa |
| SEC-03 | TimeSlotController POST/PATCH cho Admin,Manager | Khác đặc tả chỉ Admin cấu hình slot; chưa sửa |
| PAY-01 | CancelAsync đổi Cancelled nhưng không gọi RefundAsync; refund chỉ nhận Confirmed/Completed | Hủy đã trả mất đường refund hiện tại; chưa sửa |
| DATA-01 | Lookup IdempotencyKey không bind actor/payload; ToView tính giá tham chiếu sân hiện tại | Retry có thể nhận kết quả sai; breakdown lịch sử lệch; chưa sửa |
| REP-01 | Summary/Revenue dùng Bookings, refunds=0, peak tử số chưa IsPeak, DateTime.Now | KPI sai nghĩa/giờ; chưa sửa |
| DEV-01 | build.ps1/ci.yml dùng Badminton/ thay Backend/ | Lệnh hiện tại không khớp checkout; chưa sửa |
| UI-01 | Detail QrTile display-only, method mock, init success chưa điều hướng/refresh | Chưa nghiệm thu payment/QR thực; chưa sửa |
| OPS-01 | global limiter chưa áp, CORS rỗng any-origin, request_id chưa tạo | Không coi cấu hình là bảo vệ/quan sát đã đầy đủ |
| DEC-01/02/03 | Điểm/no-show khác đặc tả hoặc thiếu mốc | Cần owner chốt trước sửa chính sách |

Đọc trực tiếp source hiện tại khi triển khai; bảng này là rà soát tĩnh, chưa là kết quả khai thác/test HTTP. Các chi tiết và tiêu chí nằm tại [08](08-plan.md).
