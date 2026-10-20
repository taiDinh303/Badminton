# 09 — Triển khai và chẩn đoán cấu hình

> Chủ tài liệu: **Đinh Văn Tài**. Cập nhật: **20/10/2026**. CURRENT source; chưa kiểm env/schema live.

## 1. Nguồn chuẩn

[13-configuration.md](13-configuration.md) quản lý key/default/validation; [deploy/project.json](../deploy/project.json) quản lý đường dẫn public. Không lặp secret hoặc quota nhà cung cấp.

## 2. Startup

Program: builder → StartupConfiguration → check-config thoát hoặc đăng ký API/persistence/business → Build → DatabaseInitializer → middleware/routes/health. Check-config không xây host hoặc kết nối ngoài. Production không migrate/seed; Development/Testing dùng flags.

EF migrations là lịch sử runtime; SQL root là tham khảo; TestHost EnsureCreated chỉ model sạch. Không đổi migration body/schema trong lượt chuẩn hóa này.

## 3. HTTP/jobs

RequestIdMiddleware và log hỗ trợ truy vết. CORS nhận array hoặc comma legacy. Public/auth rate-limit partition theo IP kết nối; sau reverse proxy cần cấu hình trust đúng mạng trước dùng forwarded IP, không tin header client tùy ý.

Năm worker dùng SQL applock/heartbeat; BackgroundJobs:Enabled=false không đăng ký worker và readiness chỉ DB. Khi bật, /ready kiểm đủ năm worker, gồm WaitlistPromotionJob. /metrics cần giới hạn ingress trước release.

## 4. Chẩn đoán

| Triệu chứng | Kiểm tra | Xác nhận |
| --- | --- | --- |
| SDK không tìm thấy | global.json và SDK cài trên máy/CI | Build đúng net8.0 |
| CONFIGURATION_INVALID | Tên key/env/override | Dry-run đạt, chưa là DB đạt |
| DB không ready | Instance/quyền/schema/provider SQL Server | /ready và request thực |
| Production không migrate | Đúng thiết kế, không bật seed để chữa | Migration riêng sau review/backup |
| Browser 404 API | Base có /api/v1; proxy chỉ dev | Network đúng API |
| CORS | Origin/scheme/port/array | Preflight và mutation |
| 401 | JWT key/issuer/audience/expiry | Xóa session/login lại |
| Manager conflict | Owner/active/branch scope | Không truy cập chéo dữ liệu |
| Callback/job lỗi | GatewayRef/signature/lock/heartbeat | Đối soát, không ghi trùng |

## 5. Provider/cloud

PostgreSQL/Supabase/OTP/Storage chưa tích hợp. Chuyển provider cần migration/raw SQL/filtered index/date/identity/applock/error mapping; không lấy tài nguyên Marixa. Xem [11](11-setup-cloud.md).

Bàn giao commit/schema/URL/nơi giữ secret/người phụ trách/kiểm thử/rollback ở [worklog](worklog.md); release theo [14](14-release-checklist.md).
