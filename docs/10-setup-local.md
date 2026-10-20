# 10 — Thiết lập và chạy local

> Chủ tài liệu: **Đinh Văn Tài**. Cập nhật: **20/10/2026**. Đường dẫn theo checkout; kết quả máy kiểm ở worklog.

## 1. Yêu cầu và build

SDK 9 theo global.json, runtime .NET/ASP.NET Core 8, Node 24, Git, SQL Server local (default/Express/LocalDB). Không cần Docker/Supabase local cho baseline.

Từ root repo:

```powershell
dotnet tool restore
dotnet restore Backend/Badminton.slnx
.\build.ps1
```

Script dùng PSScriptRoot; -Run chạy API, -StopApi mới dừng process. Cấu trúc API/Tests hiện hành thay đường dẫn cũ.

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
| Pin SDK 10.0.401 không có | Dùng global.json mới, SDK9/latestFeature |
| Startup fail | Check-config → instance/schema → /ready |
| Port/DLL bận | Dừng đúng process, -StopApi khi cần |
| Playwright thiếu package | npm ci sau pull, dùng --no-install |
| Test tiền lệch cuối tuần | Expected phải theo ngày fixture |
| Import page không tìm thấy | modules/<feature>/pages thay pages cũ |

Không dùng loadtest/start_api.ps1 để start thông thường: helper legacy có reset DB. Dùng dotnet run bên trên; không chạy helper vào dữ liệu cần giữ.
