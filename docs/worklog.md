# WORKLOG — Hệ thống đặt sân cầu lông

> File này là điểm bắt đầu cho bất kỳ AI/người làm việc tiếp trên dự án. Cập nhật sau mỗi lượt làm việc có thay đổi thực tế. Ghi sự thật đã kiểm tra; không đánh dấu "xong" khi chưa kiểm chứng. Quy tắc nghiệp vụ chuẩn nằm trong `01-business-analysis.md`; `01`–`07` và worklog ưu tiên khi khác nhau.

## Trạng thái hiện tại

- Ngày 14/10/2026, `Asia/Ho_Chi_Minh`. Backend .NET đã build xanh + 23/23 test integration pass (SQL Server Express). Các điểm "chưa có" ở lượt 13/10 đã được lấp: bug múi giờ, state-machine Completed/NoShow, branch-scope, quản lý nhân viên, redeem điểm, bảo mật API (CORS/rate-limit/lockout), load-test k6 script.
- Quyết định kiến trúc hiện hành: **React + Vite SPA (không Next.js)** triển khai trên Vercel; **.NET 8 ASP.NET Core** (Clean Architecture 4 lớp + CQRS-lite) chạy container Linux trên Azure App Service; **Supabase** (Auth OTP, Postgres + RLS, Storage) lưu dữ liệu khi sang production. Dev/test hiện chạy SQL Server (LocalDB/Express) vì `UseSqlServer` + filtered index — Supabase (Postgres) là migration phase riêng (worklog cũ §5).
- Chống overbooking dùng **unique partial index** `(court_id, date, slot_id) WHERE status IN (Pending, Confirmed)` + transaction CAS; webhook gateway là nguồn xác nhận, không phụ thuộc client.
- **Đã thực thi (14/10):** múi giờ nghiệp vụ chuẩn hoá qua `HanoiTime` (UTC+7 fixed, testable); state-machine `Confirmed→Completed/NoShow` (job tự auto-complete + endpoint manager); cờ no-show 3 lần/tháng chặn đặt; branch-scope 1 manager = 1 cơ sở; admin CRUD nhân viên (tạo/lock/role/gán cơ sở); redeem điểm 1000đ=20.000đ; CORS + rate-limiter + login lockout; audit ngày Hanoi→UTC; k6 script 50 request song song.

## Việc vừa hoàn thành (lượt 14/10/2026)

- **Múi giờ (critical fix):** mọi deadline nghiệp vụ (guard +30p, refund 2h/1h/0%, pending-expiry, availability) chuyển từ `DateTime.UtcNow` sang `HanoiTime` (UTC+7) tại `Core/Utils/HanoiTime.cs`. Trước đó khách có thể đặt slot đã qua và tỷ lệ hoàn tiền lệch ±7h.
- **State machine (Q1=B):** thêm `BookingService.MarkCompletedAsync/MarkNoShowAsync` + `AutoCompleteJob` (sau giờ kết thúc slot, đơn Confirmed tự chuyển Completed để khách được đánh giá) + endpoint `POST /bookings/{id}/complete` & `/no-show` (Admin/Manager).
- **No-show gate (Q2=A):** `User.NoShowCount/NoShowPeriod/NoShowLimited/NoShowLimitedPeriod`; đủ 3 no-show trong tháng → chặn đặt tháng sau; `NoShowResetJob` tự mở lại khi qua tháng. Kiểm tra trong `BookingService.CreateAsync`.
- **Branch-scope (Q3=A):** `PaymentService.AuthorizeStaffScope` + `MaintenanceService` chặn Manager thao tác cơ sở khác (1 manager = 1 `BranchId`).
- **Redeem điểm (Q4=A):** `PointService.RedeemAsync/RestoreAsync` (LedgerKey idempotent), `Booking.PointsRedeemed`, tạo booking nhận `PointsToRedeem`, hoàn tiền full → restore điểm.
- **Quản lý nhân viên:** `StaffService` + `POST /api/v1/admin/staff/*` (tạo/lock/unlock/đổi role/gán cơ sở), guard "đúng 1 admin active" (index `ux_active_admin`).
- **Bảo mật API:** CORS (env `Cors:AllowedOrigins`), `AddRateLimiter` (global + bucket "auth" 5/60s) + `[EnableRateLimiting("auth")]` trên AuthController, lockout login 5 sai/10p→15p (in-memory, dev), `[Authorize]` cho `GET /vouchers/{code}`.
- **Sửa kỹ thuật:** `GetByUserPagedAsync` (push enum vào SQL, không `ToString()`), `IsSlotConflict` (SqlException.Number 2601, không parse message), dọn `RefundLocks` (TryRemove hết leak), audit filter ngày Hanoi→UTC boundary.
- **Database:** migration `20261014063758_AddNoShowTrackingAndPointsRedeemed` (4 cột User + 1 cột Booking); `BadmintonBooking.sql` dev cập nhật DDL + seed + migrations history.
- **Test:** thêm `TimezoneGuardTests` (3) + `LifecycleAndRedeemTests` (5); `TestHost` seed 2 branch + manager 2 cơ sở + slot 09:40, pin `HanoiTime.NowUtc`. **Kết quả: 23/23 pass** (SQL Server Express `.\\SQLEXPRESS`).
- **Load test:** `loadtest/concurrency_booking.js` (k6, 50 VU cùng slot, assert 1 winner + 49 SLOT_CONFLICT).

## Lệnh và kết quả kiểm chứng

- `dotnet build Badminton.slnx` → **Build succeeded, 0 warning 0 error**.
- `dotnet ef migrations add AddNoShowTrackingAndPointsRedeemed --project Repositories --startup-project Badminton` → migration tạo OK.
- `dotnet test Badminton.slnx` → **Passed! Failed: 0, Passed: 23, Total: 23**.
- DB test chạy trên `.\SQLEXPRESS` (LocalDB không có trên máy này; đã đổi connection string TestHost + sqlcmd xác nhận `SELECT 1` OK với `-C`).
- k6 script đã viết **chưa chạy** (cần API chạy thật + token; k6 chưa cài). Ghi rõ chưa có bằng chứng p95 thực tế.

## Việc tiếp theo

1. Chạy k6 `concurrency_booking.js` khi API chạy được → đo p95 (mục tiêu <500ms theo docs 05).
2. Chuyển Dev lockout login in-memory sang **Redis** (docs 04) khi infra Supabase/Redis sẵn sàng.
3. Áp migration lên Supabase Postgres (production), chuyển `UseSqlServer` → `UseNpgsql`, bật RLS, seed 1 admin thật (docs 06).
4. Nối gateway sandbox thật (VNPay/MoMo/Zalopay) thay `MockOnlineGateway`; verify chữ ký webhook thật.
5. Frontend React Vite: bổ sung UI cho `complete/no-show`, redeem điểm, dashboard no-show.

Quy tắc nghiệp vụ chi tiết xem [01-business-analysis.md](01-business-analysis.md); dữ liệu xem [02-database-design.md](02-database-design.md); luồng xem [03-workflow.md](03-workflow.md); kiến trúc và OOP xem [04-system-architecture.md](04-system-architecture.md); kiểm thử xem [05-testing-strategy.md](05-testing-strategy.md); triển khai xem [06-deployment-vercel-supabase.md](06-deployment-vercel-supabase.md); giao diện xem [07-ui-design-system.md](07-ui-design-system.md).
