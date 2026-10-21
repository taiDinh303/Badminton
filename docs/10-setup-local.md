# 10 — Thiết lập và chạy local

> Chủ tài liệu: **Đinh Văn Tài**. Cập nhật: **21/10/2026**. Đường dẫn theo checkout; kết quả máy kiểm ở worklog.

## 1. Yêu cầu và build

SDK từ 9.0.300 theo global.json (`rollForward: latestMajor`, chọn SDK stable mới nhất đã cài, hỗ trợ máy chỉ có SDK 10), runtime .NET/ASP.NET Core 8, Node 24, Git, SQL Server local (default/Express/LocalDB). Target ứng dụng vẫn net8.0. Không cần Docker/Supabase local cho baseline.

Từ root repo:

```powershell
dotnet tool restore
dotnet restore Backend/Badminton.slnx
.\build.ps1
```

Script dùng PSScriptRoot; -Run chạy API, -StopApi mới dừng process API có executable trong Backend/API/bin của checkout này. Khi -Run/-StopApi, script kiểm tra cổng 5131 trước build; nếu process khác còn giữ cổng thì báo PID và dừng, không tự kill process khác. Cấu trúc API/Tests hiện hành thay đường dẫn cũ.

## 2. Cấu hình local

Đặt ConnectionStrings:BadmintonBooking và JwtSettings:Key trong User Secrets của Backend/API/Badminton.API.csproj hoặc process env. Key phải riêng và ≥32 byte UTF-8; không paste vào chat/log. Connection local ví dụ Server=.;Database=BadmintonBooking;Trusted_Connection=True;TrustServerCertificate=True; (đổi instance theo máy).

Backend không tự nạp .env. Development JSON có LocalDB mẫu và cờ init local; base JSON không có connection/key. Env/User Secrets ghi đè theo máy. [13](13-configuration.md) quản lý key.

```powershell
dotnet run --project Backend/API/Badminton.API.csproj --launch-profile http -- --check-config
```

Dry-run không DB/migration/seed/jobs/HTTP ngoài.

## 3. Hai terminal

Backend từ root sau cấu hình:

```powershell
dotnet run --project Backend/API/Badminton.API.csproj --launch-profile http
```

API :5131; Swagger Development /swagger; /health liveness; /ready DB/jobs. Development mặc định migrate/seed local theo DatabaseInitialization flags. Nếu schema đã chuẩn bị, đặt MigrateOnStartup/SeedDemoData=false; tắt worker bằng BackgroundJobs:Enabled=false khi cần test chỉ API.

Frontend từ Frontend:

```powershell
npm ci
npm run dev
```

SPA :5173 proxy /api → :5131. API_PROXY_TARGET đổi dev origin; VITE_API_URL rỗng dùng /api/v1, base riêng phải đúng suffix. Vite preview không có dev proxy: cần reverse proxy/API host riêng hoặc mocked test.

## 4. Flow và tài khoản

SeedData.cs chứa tài khoản dev, không dùng production. Login → availability → chọn slot ≥30 phút tới → Pending → on-site đúng staff/branch hoặc mock → đọc lại → hủy đơn test. Mock không chứng minh thu tiền thật.

Manager mobile vào vận hành bằng Mở menu. Check-in nhận code/server token; tile chưa là QR chuẩn camera. Nhóm/review/waitlist/membership nằm trong modules tương ứng.

## 5. Kiểm tra

Root:

```powershell
$env:BADMINTON_TEST_SQLSERVER = '.'
dotnet test Backend/Tests/Badminton.Tests.csproj -c Release
dotnet ef migrations has-pending-model-changes --project Backend/Repositories --startup-project Backend/API --configuration Release --no-build
```

Fixture tạo/xóa BadmintonTest_*, chỉ local/test. Frontend: npm test, npm run lint, npm run build, npx --no-install playwright install chromium, npm run test:e2e. [05](05-testing-strategy.md) giải thích phạm vi.

## 6. Lỗi thường gặp

| Lỗi | Cách kiểm tra |
| --- | --- |
| Unable to resolve SDK theo global.json | Chạy dotnet --list-sdks và dotnet --version trong Backend; cần SDK stable từ 9.0.300, global.json dùng latestMajor để nhận SDK 10 khi máy không có SDK 9 |
| Startup fail | Check-config → instance/schema → /ready |
| Failed to bind / address already in use / Port 5131 bận | Có API khác đang chạy. Ctrl+C ở terminal cũ hoặc Shift+F5 trong Visual Studio. Từ root chạy `.\build.ps1 -StopApi` để dừng API của checkout này và build, sau đó F5 trong Visual Studio; hoặc `.\build.ps1 -StopApi -Run` nếu chỉ dùng terminal. Không chạy đồng thời cả hai. |
| Playwright thiếu package | npm ci sau pull, dùng --no-install |
| Test tiền lệch cuối tuần | Expected phải theo ngày fixture |
| Import page không tìm thấy | modules/<feature>/pages thay pages cũ |

Không dùng loadtest/start_api.ps1 để start thông thường: helper legacy có reset DB. Dùng dotnet run bên trên; không chạy helper vào dữ liệu cần giữ.

Để xem process đang giữ cổng trước khi xử lý:

```powershell
Get-NetTCPConnection -LocalPort 5131 -State Listen |
    Select-Object LocalAddress, LocalPort, OwningProcess
Get-Process -Id <OwningProcess> | Select-Object Id, ProcessName, Path
```

Thay `<OwningProcess>` bằng số PID vừa thấy. Kiểm tra tên/đường dẫn trước khi dừng; không dùng lệnh tắt toàn bộ dotnet. Giữ cổng 5131 vì frontend proxy mặc định gọi API tại đây. Lỗi xuất hiện ở `app.RunAsync()` vì đó là lúc API mở cổng HTTP, không phải lỗi đặt sân hay database.
