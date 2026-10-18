# 09 — Cấu hình triển khai, startup và xử lý sự cố

> Chủ tài liệu: **Đinh Văn Tài**. Cập nhật: **16/10/2026**.
> CURRENT: đọc source/config; không xác nhận env hoặc schema live.

## 1. Nguồn kiểm chứng

[Program.cs](../Backend/API/Program.cs), [launchSettings.json](../Backend/API/Properties/launchSettings.json), [API client](../Frontend/src/lib/api.ts), [Vite config](../Frontend/vite.config.ts), [CI](../.github/workflows/ci.yml), [EF mapping](../Backend/Repositories/Context/BadmintonBookingDbContext.cs).

## 2. Hợp đồng biến môi trường CURRENT

| Key | Nơi dùng | Ý nghĩa / điều kiện |
| --- | --- | --- |
| `ConnectionStrings__BadmintonBooking` | Server | SQL Server connection string; fallback `ConnectionStrings:DefaultConnection` |
| `JwtSettings__Key` | Server | Không rỗng; ngoài Development tối thiểu 32 ký tự; dùng secret store |
| `JwtSettings__Issuer`, `JwtSettings__Audience` | Server | Khớp token; lifetime được validate, clock skew bằng 0 |
| `ASPNETCORE_ENVIRONMENT` | Server | Development bật Swagger/seed; migration startup chạy mọi môi trường |
| `ASPNETCORE_URLS` | Server | Binding không dùng launch profile; local `http://localhost:5131` |
| `Cors__AllowedOrigins` | Server | Chuỗi origin phân tách dấu phẩy, không phải mảng `__0`; thiếu/rỗng hiện AllowAnyOrigin cả production |
| `BADMINTON_TEST_SQLSERVER` | Test | Instance SQL Server; TestHost tạo DB `BadmintonTest_*` rồi xóa |
| `VITE_API_URL` | Frontend build | Toàn bộ base gồm `/api/v1`; rỗng dùng `/api/v1` qua dev proxy |

Server không tự nạp file `.env`; copy `.env.example` chưa làm biến có hiệu lực. Dùng process env/user-secrets/cấu hình host. Vite nạp `.env.local` khi dev/build; thay env frontend cần build lại. `VITE_SUPABASE_*` có ở ví dụ nhưng chưa được ứng dụng sử dụng.

## 3. Database và startup có ghi dữ liệu

`Program.cs` dùng `UseSqlServer`, gọi `Database.Migrate()` trước phục vụ request; Development gọi `SeedData.Seed`. Không có toggle tắt migrate. Design-time factory dùng ConnectionStrings__BadmintonBooking (fallback biến legacy có tiền tố BadmintonBooking__). Phải kiểm soát quyền DDL và migration khi nhiều instance startup.

Seed gồm admin, ba manager, 500 customer; manager thứ ba gán cùng cơ sở manager thứ hai. Không mô tả một manager cho mỗi trong ba cơ sở. Tài khoản mẫu chỉ dùng dev; không sao chép lên production.

| Nguồn schema | Ý nghĩa |
| --- | --- |
| [EF migrations](../Backend/Repositories/Migrations) | Lịch sử nâng cấp runtime |
| [BadmintonBooking.sql](../BadmintonBooking.sql) | Script tham khảo; chưa chứng minh đồng bộ migration |
| [TestHost](../Backend/Tests/TestHost.cs) | EnsureDeleted/EnsureCreated; không kiểm tra đường nâng cấp migration |

## 4. Giới hạn còn mở

- Policy `global` khai báo nhưng chưa gắn GlobalLimiter/RequireRateLimiting; không khẳng định mọi endpoint đang bị chặn 100/10s. Policy `auth` gắn attribute, chưa partition từng IP.
- Login lockout tính năm lần thất bại trong cửa sổ 10 phút, in-memory; hằng LockoutDuration chưa chứng minh thời gian khóa thực tế.
- X-Request-Id chỉ expose CORS; middleware chưa tạo header/request_id.
- JWT lưu bb_token ở localStorage; cần kiểm thử token đang tồn tại sau khóa tài khoản/đổi role.
- Chưa có health/readiness riêng, log tập trung, scheduler phân tán.

Backlog ở [08](08-plan.md). Các giới hạn không tự cấp quyền đổi hạ tầng.

## 5. PostgreSQL/Supabase — TARGET

Cutover không chỉ thay UseSqlServer: cần Npgsql tương thích, migration PostgreSQL được rà soát, filtered-index syntax/raw SQL, enum/date/identity/transaction và mapping lỗi xung đột theo provider.

Theo [tài liệu kết nối chính thức](https://supabase.com/docs/guides/database/connecting-to-postgres), direct/session pooler dùng cổng 5432; transaction pooler dùng 6543. Lấy connection đúng project/mode từ dashboard, kiểm tra TLS/mạng; không dùng transaction pooler làm mặc định migration. Xác minh lại hướng dẫn khi triển khai.

RLS phụ thuộc identity/role kết nối thực tế; JWT .NET không tự thành auth.uid() khi EF dùng DB connection đặc quyền. Chọn API-only hoặc Data API có identity mapping và policy tương ứng; không cấp browser ghi trực tiếp booking/payment/ledger.

OTP, Storage, scheduler, Redis và gateway thật đều PENDING; chưa có env contract chạy được. Thêm biến mẫu không đồng nghĩa tích hợp hoàn tất.

## 6. Chẩn đoán

| Triệu chứng | Kiểm tra | Xác nhận sau xử lý |
| --- | --- | --- |
| Không thấy solution/project | Backend/Badminton.slnx; API/Badminton.API.csproj; Tests/Badminton.Tests.csproj | Restore/build tìm đúng project |
| Không đọc .slnx | SDK theo global.json, CI cài SDK 9/runtime 8 | Build solution, target vẫn net8.0 |
| Chết trước Swagger | SQL instance/quyền/JWT/migration | Startup và API đọc DB đúng |
| Browser 404 API | Base thiếu /api/v1; rewrite SPA nuốt /api | Request đến host API đúng |
| CORS | Origin scheme/host/port, env chuỗi | Preflight và mutation browser đúng |
| 401/403/409 quyền | Token/role/owner/scope; có nơi dùng 409 | HTTP test actor thật |
| Login 429 tải cao | Auth bucket dùng chung/load threshold | Phân biệt rejection và server error |
| Có tiền nhưng đơn hết hạn | gateway_ref/callback muộn/trạng thái terminal | Đối soát có audit trước chỉnh dữ liệu |

## 7. Hợp đồng bàn giao

Bàn giao cấu hình không bí mật, nơi quản lý secret, commit, schema/startup, URL, kiểm thử và lỗi mở. Không ghi secret vào screenshot/log. Mẫu ở [11](11-setup-cloud.md).
