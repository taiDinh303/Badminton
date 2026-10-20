# BadmintonBooking

Ứng dụng đặt sân cầu lông: ASP.NET Core 8, EF Core/SQL Server và React 19/Vite/TypeScript.

- [Tài liệu và hiện trạng](docs/00-README.md)
- [Chạy local](docs/10-setup-local.md)
- [Cấu hình](docs/13-configuration.md)
- [Kiến trúc backend](Backend/ARCHITECTURE.md)
- [Cấu trúc frontend](Frontend/README.md)

```powershell
dotnet tool restore
.\build.ps1
# Đặt cấu hình local, sau đó:
dotnet run --project Backend/API/Badminton.API.csproj -- --check-config
```

Lệnh check-config không kết nối DB, migrate, seed hoặc chạy worker. Xem hướng dẫn local trước chạy API thật; Development có thể initialize dữ liệu local theo cờ cấu hình. Không có deployment cloud nào được xác minh từ README này.
