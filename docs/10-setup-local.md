# 10 — Thiết lập và chạy local

> Chủ tài liệu: **Đinh Văn Tài**. Cập nhật: **16/10/2026**.
> Lệnh theo cấu trúc API/Tests chuẩn hóa 16/10/2026. Kết quả build/test hiện hành ở worklog.

## 1. Yêu cầu

- Windows + SQL Server Express/LocalDB cho integrated authentication hiện tại.
- SDK 9.0 theo global.json (baseline 9.0.300, latestFeature); máy kiểm tra dùng 9.0.317. Project vẫn target net8.0, cần runtime ASP.NET Core/.NET 8 khi chạy/test.
- Node 24 như CI hiện tại; máy kiểm tra dùng 24.21.0. Dùng npm ci theo lockfile.
- Git; k6/dotnet-ef khi chạy kiểm tra tương ứng.

Không cần Docker/Supabase local cho baseline SQL Server.

## 2. Restore/build từ root repo

```powershell
Set-Location 'D:\My Project\Badminton'
dotnet --version
node --version
dotnet restore .\Backend\Badminton.slnx
dotnet build .\Backend\Badminton.slnx -c Debug --no-restore
```

Từ root có thể chạy .\build.ps1; thêm -Run để chạy API, -StopApi nếu cần dừng API trước build. Script dùng đường dẫn theo PSScriptRoot, không đổi thư mục terminal và không mặc định dừng process. CI đã dùng Backend/API và Backend/Tests, SDK 9 + runtime 8.

## 3. Cấu hình backend

Trong terminal sẽ chạy API:

```powershell
$env:ConnectionStrings__BadmintonBooking = 'Server=.\SQLEXPRESS;Database=BadmintonBooking;Trusted_Connection=True;TrustServerCertificate=True;'
$env:JwtSettings__Key = '<local-random-key-at-least-32-characters>'
$env:ASPNETCORE_ENVIRONMENT = 'Development'
```

Thay placeholder key; issuer/audience phải nhất quán. LocalDB dùng Server=(localdb)\MSSQLLocalDB. PowerShell dùng một backslash thật; escape đôi trong JSON là quy ước khác.

Startup áp migration/seed trên DB đã chọn; dùng DB riêng dự án. `.env` root không tự được server nạp. Xem [09](09-deploy-detail.md).

## 4. Hai terminal độc lập

Backend từ root repo, sau đặt env:

```powershell
dotnet run --project .\Backend\API\Badminton.API.csproj --launch-profile http
```

API http://localhost:5131; Swagger Development http://localhost:5131/swagger. Profile http đặt Development.

Frontend:

```powershell
Set-Location 'D:\My Project\Badminton\Frontend'
npm ci
npm run dev
```

SPA http://localhost:5173; proxy /api → :5131. Không cần VITE_API_URL khi dùng proxy. Nếu tạo Frontend/.env.local, base phải gồm /api/v1. Restart Vite sau đổi env.

## 5. Tài khoản và smoke test

Tài khoản mẫu xem [SeedData.cs](../Backend/API/SeedData.cs); manager SĐT 0900000002–0900000004, customer bắt đầu 0910000000. DB cũ không tự bảo đảm có seed mới.

1. Đăng nhập khách; đọc availability với ngày yyyy-MM-dd.
2. Chọn sân/slot active, bắt đầu ít nhất 30 phút trong tương lai; tạo Pending.
3. Manager đúng cơ sở xác nhận Cash/QR; đọc lại đơn/payment/ledger.
4. Hủy một đơn Pending test khác để kiểm tra giải phóng slot; hủy đã trả còn PAY-01.
5. Đọc danh sách/chi tiết browser; QR tile chưa là mã quét hợp lệ.

Mock gateway không thu tiền thật. Chạy và ghi kết quả trước khi kết luận local hoạt động.

## 6. Kiểm tra

Root repo:

```powershell
$env:BADMINTON_TEST_SQLSERVER = '.\SQLEXPRESS'
dotnet test .\Backend\Tests\Badminton.Tests.csproj -c Debug
```

Test tạo/xóa DB BadmintonTest_* trên instance chọn; không chạy server live. Suite service/EF chưa bao phủ middleware/JWT/controller.

Từ Frontend:

```powershell
npm run lint
npm run build
```

Ghi lệnh/phiên bản/môi trường/kết quả vào [worklog](worklog.md). Chi tiết [05](05-testing-strategy.md).

## 7. Lỗi thường gặp

| Lỗi | Kiểm tra |
| --- | --- |
| Không thấy project | Backend và tên Badminton.API.csproj |
| SQL connect fail | Instance/quyền Windows/connection string |
| DLL khóa | Dừng đúng process API dự án trước build |
| Engine Vite | Node và engines node_modules/vite/package.json |
| API 5173 404 | Vite dev/proxy; preview không có dev proxy |
| Clock test lệch | HanoiTime.NowUtc reset; không test xung đột global clock |

Không import SQL script rồi áp migrations cùng DB khi chưa đối chiếu lịch sử schema.

## 8. EF tool và kiểm tra model

Từ root repo:

```powershell
dotnet tool restore
dotnet ef migrations has-pending-model-changes --project Backend/Repositories --startup-project Backend/API
```

Tool manifest pin dotnet-ef 8.0.28 theo EF packages. Design-time factory ưu tiên ConnectionStrings__BadmintonBooking, vẫn hỗ trợ biến legacy BadmintonBooking__ConnectionStrings__BadmintonBooking; kiểm tra model drift không áp migration. Khi thực sự update DB, xác nhận connection đúng môi trường trước chạy.
