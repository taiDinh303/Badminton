# 04 — Kiến trúc hệ thống đặt sân cầu lông

> Chủ tài liệu: **Đinh Văn Tài**. Cập nhật: **20/10/2026**. CURRENT theo checkout; bằng chứng ở [worklog](worklog.md). Không xác nhận cloud.

## 1. Kiến trúc và ranh giới

React/Vite SPA → HTTP `/api/v1` → ASP.NET Core 8 → business service → repository contract → EF Core/SQL Server. Jobs chạy trong API. API adapter cung cấp JWT identity, logging, health và metrics. PostgreSQL/Supabase/OTP/Storage chưa là đường chạy hiện tại.

```mermaid
flowchart LR
  UI[React feature modules] --> API[ASP.NET API]
  API --> S[Business services]
  API --> R[Persistence registration]
  S --> C[Contracts and Core]
  R --> C
  R --> DB[(SQL Server)]
  API --> J[Background jobs]
  S --> P[Payment and notification adapters]
```

## 2. Phân tầng CURRENT

| Project / folder | Trách nhiệm | Phụ thuộc cho phép |
| --- | --- | --- |
| API/Configuration | Composition, auth/CORS/rate-limit, validation, health | Services, Repositories, contracts, Core |
| API/Bootstrap | Khởi tạo DB có guard | EF/migration; không chạy khi check-config |
| API/Security | HttpCurrentUser | ASP.NET HTTP → Core ICurrentUser |
| API/Controllers | Request/response, role attribute | Contract.Services và DTO |
| Services | Nghiệp vụ, quyền dữ liệu, adapters payment/notify | Contracts, Core, ModelViews, Extensions; không tham chiếu Repositories/EF/SqlClient/HttpContext |
| Contract.Services | Interface nghiệp vụ | Contract.Repositories, Core, ModelViews |
| Repositories | EF mapping/migration/UnitOfWork, lỗi SQL và DI | Contract.Repositories, Core, EF/provider |
| Contract.Repositories | Entity và repository contract | Core; không EF |
| Core | Enum, settings, domain, lỗi trung lập, ICurrentUser | Không EF/ASP.NET |
| ModelViews | DTO HTTP | Core theo nhu cầu |
| Tests | Config/architecture, service/SQL, HTTP/JWT/Kestrel | Các project cần kiểm chứng |

Repository DI là [AddBookingPersistence](../Backend/Repositories/DependencyInjection.cs); API gọi qua [AddBookingApi](../Backend/API/Configuration/DependencyInjection.cs). Business registration ở [ServiceRegistration](../Backend/Services/DI/ServiceRegistration.cs) không tạo UnitOfWork implementation.

Services vẫn có raw SQL qua IUnitOfWork cho atomic claim hiện hữu; đây là giới hạn portability được giữ để bảo toàn concurrency, không tuyên bố đã provider-independent hoàn toàn. Không thêm CQRS/MediatR hoặc layer rỗng chỉ để giống mẫu.

## 3. OOP và dependency inversion

BaseService/BaseApiController/BaseEntity chia sẻ hành vi thật. Interface tách persistence, payment strategy, notify channel và current user. Repository dịch unique SQL error thành UniqueConstraintException trung lập; business không bắt DbUpdateException/SqlException. ICurrentUser dùng ClaimsPrincipal .NET thuần, adapter HttpCurrentUser nằm tại API; fixture test cung cấp identity giả.

Core/contracts không được tham chiếu implementation repository; test architecture kiểm reference assembly. API là composition root, controller không new service. Tên project/path hiện hành ở [Backend/ARCHITECTURE](../Backend/ARCHITECTURE.md).

## 4. Hợp đồng HTTP

| Nhóm prefix /api/v1 | API chính / quyền |
| --- | --- |
| auth | POST register/login; chỉ Customer tự đăng ký |
| branches, courts, time-slots | GET public; mutation theo controller role, xem nghiệp vụ |
| bookings | POST, mine, detail, cancel; availability public; all/complete/no-show staff |
| payments | init, booking history; webhook provider anonymous có verification adapter; on-site/refund staff |
| groups | mine/create/join/members; owner đặt nhóm |
| vouchers, reviews | Preview cần login; review GET public, POST owner Completed |
| checkin | Staff theo cơ sở; code/QR token và danh sách slot |
| waitlist | Customer join/leave/mine |
| admin/membership | Admin assign/members |
| admin/report, admin/staff, admin/audit | Role + scope theo từng endpoint |

Response controller: `{data,message,statusCode,code}`; domain exception cùng envelope; validation 400 có VALIDATION_ERROR. 401/403/429 framework có thể không cùng body; client dùng HTTP status trước. RequestIdMiddleware cung cấp X-Request-Id; không log token/password. Không tự đổi statusCode enum/body hiện hữu để giống Marixa.

## 5. Quyền và tính nhất quán

- Owner kiểm tại service; JWT role không thay quyền sở hữu. Manager list/summary/detail/payment read/lifecycle/check-in được giới hạn branch phía server, không tin branchId query. Account không active/chưa gán cơ sở fail closed.
- Unique `(CourtId,Date,SlotId)` trên Pending/Confirmed bảo vệ giữ chỗ. Retry key được kiểm owner và các trường slot/date/group/playerCount; chưa lưu hash toàn payload voucher/points/note.
- Tiền đơn có snapshot. Breakdown lịch sử dùng TotalAmount đã gồm phụ thu; trường WeekendSurcharge=0 vì dữ liệu cũ không lưu riêng. Không tính lại tiền theo giá sân hiện tại.
- Cancel đơn đã trả đi qua CancelRefundAsync. Full refund trả điểm đã dùng; earn khi Completed. Xem [03](03-workflow.md).
- Transaction/conditional update không thay đối soát gateway khi timeout/crash. Gateway thật phải nghiệm thu sandbox trước dùng tiền thật.

## 6. React CURRENT

app/routes.tsx quản lý lazy routes/guards; app/Providers và queryClient quản lý cache/session; app/navigation dùng chung sidebar/mobile. modules/<feature>/pages giữ màn theo nghiệp vụ. services/http.ts quản lý fetch/envelope/401; services/api tách endpoint/types; utils/format và theme.css dùng chung. lib/api.ts chỉ là facade tương thích.

Token và bb_user cùng cần có khi hydrate. 401 xóa session, identity đổi hủy/clear query cache. Route guard không thay server authorization. Mobile menu mở cả vận hành; admin-only link ẩn với Manager. Không đưa logic giá/quyền server xuống component.

## 7. Startup và vận hành

[StartupConfiguration](../Backend/API/Configuration/StartupConfiguration.cs) validate trước xây host; --check-config thoát trước DB/jobs/external calls. Production không auto migrate/seed, CORS phải explicit HTTPS, payment real phải đủ cấu hình. Development có initialize flags. BackgroundJobs:Enabled tắt/bật cả năm worker; readiness bỏ heartbeat khi tắt có chủ đích và kiểm đủ năm khi bật.

/health chỉ liveness; /ready kiểm DB/heartbeat; /metrics cần được giới hạn qua mạng/ingress khi triển khai. Logging giữ request ID và file retention; không gọi log file là JSON nếu output là text. Config đầy đủ ở [13](13-configuration.md).
