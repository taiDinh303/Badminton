# 15 — Vận hành cloud Badminton

Ngày 21/10/2026, người dùng chỉ định Supabase tổ chức `tugxiczrpnesubccgbuq`, frontend Vercel và Render; đồng ý chuyển toàn bộ dữ liệu local và chạy thanh toán tại sân trước.

## Kiến trúc CURRENT

- Frontend React/Vite là file tĩnh; Vercel và Render Static không chạy ASP.NET. Build từ `Frontend`, `npm ci && npm run build`, publish `dist`; SPA rewrite `/*` → `/index.html`.
- API .NET dùng Dockerfile ở root, build context root, cổng 8080, user không đặc quyền; health `/health`, readiness `/ready` gồm DB và heartbeat worker.
- Supabase project `otwhjkimfzxabbnaktpl`, PostgreSQL 17; 17 bảng ứng dụng và lịch sử EF, giữ tên bảng/ID và JWT/BCrypt của ứng dụng. RLS bật, anon/authenticated không được truy cập bảng; client chỉ gọi API .NET.
- Kết nối API dùng Session pooler `aws-0-ap-southeast-2.pooler.supabase.com:5432`, username `postgres.otwhjkimfzxabbnaktpl`, database `postgres`, TLS VerifyFull và CA công khai đóng gói tại `/app/certificates/supabase-ca.crt`. Không dùng transaction pooler 6543 vì worker cần session advisory locks.

## Cấu hình triển khai

Backend đặt secret trong Render Environment, không Git: `ConnectionStrings__BadmintonBooking`, `JwtSettings__Key`. Đặt `Database__Provider=PostgreSQL`, `Payment__Provider=onsite`, `Cors__AllowedOrigins` gồm đúng origin frontend, `DatabaseInitialization__MigrateOnStartup=false`, `DatabaseInitialization__SeedDemoData=false`, `BackgroundJobs__Enabled=true`.

Frontend đặt public `VITE_API_URL=https://<api-host>/api/v1` rồi rebuild/redeploy. Không đặt connection string hoặc khóa JWT/DB trong VITE_*.

`onsite` chỉ nhận Cash/QR tại quầy do Admin/Manager xác nhận; không tạo gateway giả. GET `/api/v1/payments/methods` điều khiển lựa chọn UI. Không coi nhấn xem hướng dẫn tại quầy là đã thanh toán.

`render.yaml` là mẫu tạo mới; không áp Blueprint lên service có sẵn mà chưa đối chiếu tên/nguồn để tránh tạo trùng. Gói Free có thể ngủ khi không hoạt động; worker sẽ không chạy trong thời gian instance ngủ. Chưa là cam kết vận hành liên tục hoặc thu tiền online thật.

## Migration và chuyển dữ liệu

Giữ lịch sử SQL Server trong `Backend/Repositories/Migrations`. PostgreSQL dùng `PostgresBookingDbContext` và bộ migration riêng `Migrations/PostgreSql`. Production không tự migrate/seed.

`tools/DatabaseTransfer` là công cụ thao tác riêng, không gọi từ startup. Nhận đường dẫn file connection source/target (đặt ngoài Git) và `--execute`. Chỉ nhập khi 17 bảng đích đã migrate và trống; khóa đích trong transaction, đọc source trong transaction Serializable, COPY phía client, đối chiếu toàn bộ giá trị/ID, đặt sequence rồi mới commit. Không ghi đè dữ liệu cloud có sẵn. Timestamp lịch sử UTC được giữ, chỉ làm tròn độ chính xác microsecond của PostgreSQL.

Trước chuyển live, phải diễn tập trên PostgreSQL local riêng. Không chạy test fixture hoặc load helper trên DB ứng dụng/cloud. Sau khi chuyển, số liệu có thể đổi do hoạt động thực và worker; worklog ghi snapshot lúc cutover.

## Bằng chứng và giới hạn

Xem [worklog](worklog.md) cho commit/deployment, lệnh thật, số test và trạng thái live mới nhất. Build xanh không tự xác nhận gateway, thiết bị QR/camera hoặc worker crash/multi-instance. Secret, file chuyển dữ liệu và ảnh xác minh nằm trong `artifacts/` (Git ignored).
