# 13 — Chuẩn cấu hình và khởi tạo an toàn

> Chủ tài liệu: **Đinh Văn Tài**. Cập nhật: **21/10/2026**. CURRENT cho SQL Server/PostgreSQL/.NET và React; không chứa secret live.

## 1. Nguồn cấu hình

Backend: appsettings.json → appsettings theo environment → User Secrets chỉ Development → env → command line. Không tự đọc .env. Production publish loại mọi appsettings theo environment (appsettings.*.json); base JSON không chứa connection/key local. Đặt secret qua User Secrets hoặc host env, không commit.

Frontend: Vite load env ở dev/build; VITE_API_URL là public, không chứa DB/JWT/gateway key. API_PROXY_TARGET chỉ dev server, không dùng tiền tố VITE_. [deploy/project.json](../deploy/project.json) chỉ mô tả đường dẫn, chưa chọn host/domain.

## 2. Key CURRENT

| Key env | Nguồn/default | Kiểm tra |
| --- | --- | --- |
| ConnectionStrings__BadmintonBooking | Secret bắt buộc, Development có LocalDB mẫu | Đúng cú pháp server/database; ngoài Dev/Testing không LocalDB |
| Database__Provider | SqlServer | Nhận SqlServer hoặc PostgreSQL; PostgreSQL Production yêu cầu TLS VerifyFull/VerifyCA và direct/session pooler, không port 6543 |
| JwtSettings__Key | User Secrets/env | ≥32 byte UTF-8, không key placeholder |
| JwtSettings__Issuer / Audience | Base JSON riêng Badminton | Không rỗng; giữ contract token hiện hành |
| JwtSettings__ExpireMinutes | 60 | 1–1440 |
| Cors__AllowedOrigins__0 | Origin HTTPS frontend | Production bắt buộc, không wildcard/path/query; vẫn nhận chuỗi comma legacy |
| DatabaseInitialization__MigrateOnStartup | false base, true Development | Chỉ Development/Testing; production không tự DDL |
| DatabaseInitialization__SeedDemoData | false base, true Development | Chỉ Development/Testing; bỏ escape hatch seed prod |
| BackgroundJobs__Enabled | true | Tắt có chủ đích cho test; heartbeat bị bỏ qua khi tắt |
| Payment__Provider | mock base | Ngoài Dev/Testing nhận real hoặc onsite; onsite chỉ Cash/QR tại quầy, không đăng ký mock/online gateway; real cần đủ key/endpoint cả ba adapter |
| Notify__Channels | log | email/sms/zalo cần flat keys và endpoint HTTPS |
| Swagger__Enabled | false | Development bật; môi trường khác theo flag |
| Logging__FileDirectory | logs dưới thư mục app | Chỉ nơi ghi log, không là secret |
| Membership__MultiplierSilver/Gold | Theo Settings | Số dương; ngày theo SilverDays/GoldDays hiện hữu |
| VITE_API_URL | /api/v1 nếu trống | /api/v1 hoặc HTTPS base kết thúc /api/v1; HTTP chỉ loopback |
| API_PROXY_TARGET | http://localhost:5131 | Origin, không credentials/path/query |

Notify key đúng source: Notify__EmailEndpoint/EmailKey, SmsEndpoint/SmsKey, ZaloEndpoint/ZaloToken. Tên nested Email__Endpoint trong mẫu cũ không bind NotifySettings; đã sửa ví dụ. Seed__AllowInProduction=true legacy bị chặn, không dùng để chữa DB production.

## 3. Dry-run không side effect

```powershell
# Root repo, đã đặt config đúng môi trường:
dotnet run --project Backend/API/Badminton.API.csproj --no-launch-profile -- --check-config
```

Trả CONFIGURATION_OK/exit 0 hoặc CONFIGURATION_INVALID/exit 2 và tên key cần sửa; không in giá trị. Thoát trước host/DI/DB/migration/seed/worker/HTTP external. Không chứng minh password/schema/provider/gateway thật kết nối được. Production real cần đủ cấu hình adapter ngay cả dry-run; onsite chỉ cho thanh toán tại sân. Dry-run không gọi gateway.

Development profile có thể dùng để kiểm bằng --launch-profile http. User Secrets của đúng API giữ cấu hình máy cá nhân; không copy credential từ Marixa. Production onsite không cần khóa online gateway và từ chối yêu cầu thanh toán online; Production real vẫn yêu cầu đủ cấu hình cả ba adapter.

## 4. Khởi tạo và migration

DatabaseInitializer là nơi duy nhất startup migrate/seed. Ngoài Development/Testing, validator và initializer cùng chặn. Production áp migration đã review bằng thao tác riêng lên đúng đích sau backup/restore rehearsal; không chạy command production trong nhiệm vụ này.

TestHost tạo DB BadmintonTest_* riêng: SQL Server dùng EnsureCreated; PostgreSQL dùng migration trên loopback qua BADMINTON_TEST_POSTGRES. Bộ migration SQL Server lịch sử giữ nguyên; PostgresBookingDbContext có bộ migration/snapshot riêng trong Repositories/Migrations/PostgreSql. Repository ánh xạ unique violation và khóa transaction/session theo provider; Services dùng hợp đồng khóa lịch trung lập. Production vẫn không tự migrate/seed.

PostgreSQL dùng connection string Npgsql dạng `Host=<session-host>;Port=5432;Database=postgres;Username=postgres.<project-ref>;Password=<secret>;SSL Mode=VerifyFull;Root Certificate=/app/certificates/supabase-ca.crt;Maximum Pool Size=10`. Docker đóng gói CA công khai. Frontend chỉ gọi API .NET qua VITE_API_URL; không có quyền đọc bảng bằng anon/authenticated Supabase. GET `/api/v1/payments/methods` trả phương thức đang bật để UI không mời thanh toán online khi chỉ cấu hình onsite.

## 5. Kiểm thử và publish

ConfigurationAndArchitectureTests dùng dữ liệu synthetic và kiểm không lộ secret. HTTP tests dùng DB local riêng và jobs=false. scripts/Test-ProductionConfiguration.ps1 publish và kiểm synthetic Production hợp lệ/không hợp lệ, không DB/external request. Publish phải xác minh không có Development JSON; config check với server synthetic chỉ xác nhận validation, không DB.

CI dùng SDK theo global.json + runtime net8.0; Node 24/lockfile; unit/frontend/HTTP/provider tests có phạm vi riêng. Xem [14 release](14-release-checklist.md), [05 test](05-testing-strategy.md).
