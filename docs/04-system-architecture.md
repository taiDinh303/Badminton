# 04 — Kiến trúc hệ thống đặt sân cầu lông

> **Chủ tài liệu: Đinh Văn Tài.** Cập nhật: 16/10/2026.
> **EN/AI:** This doc describes the **source** .NET solution + React SPA; runtime has not been verified in this documentation task. The dev baseline is a **4-layer modular architecture, feature-by-feature folders**. **CQRS/MediatR is NOT implemented** (it is a planned option, not the baseline). Supabase/Postgres/RLS/Hangfire are **production targets**, not the current dev stack (SQL Server + JWT + IHostedService + mock gateways).

## 1. Kiến trúc tổng quan

```mermaid
flowchart LR
  UI[React 19 + Vite SPA\nTailwind v4 · zustand · React Query] -->|/api/v1 (dev proxy / VITE_API_URL)| API[.NET 8 ASP.NET Core\n/api/v1 · JWT Bearer · Swagger]
  API --> DB[(SQL Server dev\n LocalDB/Express\n → Supabase Postgres prod)]
  API --> AUTH[JWT (dev, BCrypt)\n → Supabase Auth OTP (prod)]
  API --> PAY[IPaymentGateway\n MockOnline dev → VnPay/MoMo/Zalopay prod]
  API --> JOB[IHostedService jobs dev\n → Hangfire prod]
  API --> FILE[Court.ImageUrl path\n → Supabase Storage buckets prod]
```

- **Frontend:** React 19 + Vite **SPA** (không Next.js) — API .NET trả đủ REST, không cần SEO. Route thật; làm mới/Back-Forward/link chi tiết giữ đúng màn hình. Khách ưu tiên **mobile** (đặt sân), manager/admin ưu tiên **desktop** (vẫn dùng được màn hình nhỏ).
- **Backend:** .NET 8, EF Core, JWT Bearer, rate-limiting, CORS, Swagger.
- Mọi thao tác nghiệp vụ qua **API .NET có kiểm tra quyền**; không cho client ghi trực tiếp bảng đặt chỗ/thanh toán/điểm/voucher/audit.

**Không dùng Docker cho frontend.** Dev: chạy Vite bằng Node + SQL Server LocalDB/Express, `npm run dev`. Prod: API .NET cần host chạy lâu dài; container/host cụ thể là TARGET, chưa có Dockerfile. Migration hiện EF SQL Server; chuyển PostgreSQL cần migration và raw SQL riêng được kiểm tra.

## 2. Phân tầng (dev baseline thực tế)

Giải pháp `Backend/Badminton.slnx` gồm các project (module theo chức năng):

| Layer | Project | Trách nhiệm |
| --- | --- | --- |
| API | `Backend/Badminton/BadmintonBE.API.csproj` (API) | Controllers (1 thư mục/feature), Middleware, Jobs, `Program.cs` (auto-migrate + seed), DI. |
| Services | `Services` + `Contract.Services` | Implementation + interface mỗi feature (Booking, Payment, Voucher, Pricing, …). |
| Repository | `Repositories` + `Contract.Repositories` | `BadmintonBookingDbContext` (15 bảng + index), UnitOfWork, Migrations, Entity classes. |
| Core | `Core` | `Base` (BaseEntity, BaseResponse, BaseException, StatusCode), `Domain` (`IPaymentGateway`), `Enums`, `Store` (JwtSettings), `Utils` (`HanoiTime`, `TimeHelper`, `EnumHelper`). |
| DTO | `ModelViews` | Record DTO per feature (request/response, camelCase). |
| Tests | `Badminton.Tests` | xUnit, integration trên SQL Server thật. |

Mỗi module nghiệp vụ tách riêng trong `Services/<Feature>/`, interface định nghĩa ở `Contract.Services/<Feature>/`, DTO ở `ModelViews/<Feature>/`. `Services/DI/ServiceRegistration.cs` là composition root.

> **Quyết định ghi rõ:** CQRS-lite/MediatR **CHƯA** implement. Cấu trúc 4-layer + feature-folder là baseline dev đã đồng ý. Thêm MediatR khi request volume đòi hỏi (Open/Closed).

### Nguyên tắc OOP trong source .NET

1. **Abstraction (lõi):** mọi phía ngoài domain làm việc qua interface — `IPaymentGateway`, `IBookingService`, `IUnitOfWork`, `IPricingService`. Impl (mock/real, EF) thay được mà không sửa caller. Suite hiện tạo DI/service và SQL Server thật; chưa là unit test thuần mock.
2. **Polymorphism + Open/Closed (lõi):** Strategy cho thanh toán — `MockOnlineGateway`/`OnSiteGateway` (dev) và sau này `VnPayGateway`/`MomoGateway`/`ZalopayGateway` (prod) đều implement `IPaymentGateway`; `PaymentGatewayFactory` chọn theo `PaymentMethod`. Thêm gateway cần class, factory/DI/config và kiểm thử contract; không suy ra chỉ thêm một dòng là tích hợp xong.
3. **Encapsulation (hạn chế):** aggregate `Booking`/`Payment` mutation chủ yếu qua service method (`ConfirmOnSiteAsync`, `RefundAsync`); value immutable (int/enum). UI/controller không tự thay `Booking.Status`.
4. **Inheritance (chừng mực):** `IntBaseEntity` (id int + created/updated) là cha trực tiếp các entity; composition + interface (DIP) ưu tiên hơn cây thừa kế sâu.

**Hàm giá duy nhất:** `PricingService` (interface `IPricingService`) là nơi duy nhất tính giá; Booking dùng PricingService để tạo giá; hoàn tiền đọc PayableAmount snapshot. Dashboard hiện tổng Booking, chưa đối soát payment. UI phải hiển thị kết quả server; breakdown lịch sử còn DATA-01.

## 3. Route giao diện (React SPA, khớp `src/App.tsx`)

| Route | Người dùng | Nội dung |
| --- | --- | --- |
| `/auth` | Chưa đăng nhập | Đăng nhập/đăng ký (dev: sđt + mật khẩu; prod: OTP). |
| `/` (home) | Khách | Chọn cơ sở, ngày, grid sân trống (màn hình chính). |
| `/book` | Khách | Grid đặt sân, chọn slot/số người/voucher/điểm, xác nhận. |
| `/bookings`, `/bookings/:id` | Chủ đặt | Đơn của tôi; chi tiết, QR, hủy theo quy tắc. |
| `/account` | Khách | Hồ sơ, lịch sử, điểm, voucher, nhóm. |
| `/admin` | Manager, admin | Dashboard KPI theo quyền. |
| `/admin/bookings` | Manager, admin | Danh sách đặt (filter cơ sở/ngày/trạng thái), hoàn/xử lý. |
| `/admin/courts` | Manager, admin | Quản lý sân/giá, bảo trì. |
| `/admin/vouchers` | Staff guard; API mutation Admin | Tạo/vô hiệu voucher. |
| `/admin/slots` | Staff guard; API create/update Staff, delete Admin | Khung giờ (thêm/bớt slot). |
| `/admin/staff` | Staff guard; API Admin | Tạo/lock/unlock nhân viên, đổi role, gán cơ sở. |
| `/admin/audit` | Staff guard; API Admin | Nhật ký audit. |

- Guard: `Protected` (không token → `/auth`), `Staff` (customer không mở `/admin/*`), admin pages **lazy-load** (mục tiêu bundle khách <200 KB gzip, chưa đo). Phân quyền thật enforce ở .NET; guard phía client chỉ hỗ trợ UI.

## 4. Hợp đồng API V1 (khớp controllers thật)

Controller dùng BaseResponse: data, message, statusCode, code (camelCase). Middleware domain exception trả mã HTTP và envelope; chưa có request_id. Validation tự động ASP.NET, 401/403/429 không bảo đảm cùng envelope. IdempotencyKey nằm body CreateBookingRequest; phân trang BasePaginatedList.

| Endpoint (prefix `/api/v1`) | Chức năng / quyền |
| --- | --- |
| `POST /auth/register`, `POST /auth/login` | Đăng ký/đăng nhập (dev: sđt+mật khẩu). Auth: rate-limit bucket `auth`. |
| `GET /branches`, `GET /courts?branchId=`, `GET /courts/{id}` | Cơ sở, sân + availability (public-ish). |
| `GET /time-slots` | Public danh sách khung giờ. CURRENT POST/PATCH cho Admin/Manager, DELETE chỉ Admin; khác yêu cầu chỉ Admin cấu hình. |
| `GET /bookings/availability/{branchId}/{date}` | Grid sân + slot trống/khóa (public). |
| `POST /bookings` | Tạo đặt chỗ `Pending`; body: `courtId`,`date`,`slotId`,`playerCount`,`groupId?`,`voucherCode?`,`pointsToRedeem?`,`idempotencyKey?`. |
| `GET /bookings/mine`, `GET /bookings/{id}` | Lịch sử của tôi / chi tiết (chi tiết hiện owner/owner nhóm; staff override chưa đầy đủ). |
| `POST /bookings/{id}/cancel` | Hủy trạng thái; còn lỗi quyền và chưa gọi refund (SEC-01/PAY-01). |
| `POST /bookings/{id}/complete`, `/no-show` | Manager/Admin; chưa guard thời điểm/scope đầy đủ. |
| `GET /bookings/all` | Manager/Admin, filter `status/branchId`, phân trang. |
| `POST /payments/init` | Lấy URL gateway theo `bookingId`+`method` (idempotent). |
| `POST /payments/{bookingId}/on-site` | Manager xác nhận trả tại quầy (cash/QR). |
| `POST /payments/{bookingId}/refund` | Hoàn tiền (tạo record mới, theo quy tắc). |
| `GET /payments/booking/{bookingId}` | Lịch sử payment của booking. |
| `POST /payments/webhook/{provider}` | Gateway callback (public, verify chữ ký — prod; mock dev). Idempotent theo `gateway_ref`. |
| `GET /vouchers/{code}/preview?baseAmount=`, `GET /vouchers` | Preview / danh sách voucher. |
| `POST /vouchers`, `POST /vouchers/{id}/deactivate` | Admin tạo/vô hiệu voucher. |
| `POST /groups`, `POST /groups/{id}/join` | Nhóm chơi (customer). |
| `GET /reviews?courtId=`, `POST /reviews` | Xem/tạo đánh giá (chỉ `Completed`). |
| `POST /admin/staff`, `POST …/lock`, `POST …/unlock`, `PATCH …/role`, `PATCH …/branch` | Admin quản lý nhân viên. |
| `POST /admin/courts/{id}/maintenance` | Manager cơ sở/Admin bảo trì/đóng sân theo ngày. |
| `GET /admin/report/summary`, `/revenue`, `/occupancy` | Summary cho staff (scope còn thiếu); revenue/occupancy chỉ Admin. |
| `GET /admin/audit` | Query audit (admin). |

- Hai khách cùng đặt 1 slot → unique filtered index đảm bảo chỉ 1 record giữ; record khác trả `SLOT_CONFLICT` (409).
- Webhook payment idempotent theo `gateway_ref`; webhook lặp không đổi trạng thái.
- **Dev baseline:** gateway online = `MockOnlineGateway` (URL mock, verify cấu trúc); prod thay adapter thật + verify chữ ký thật.

## 5. Thời gian & đồng bộ đặt chỗ

- `HanoiTime` (Core/Utils): fixed UTC+7, `NowUtc` có thể pin trong test. Mọi deadline (guard +30p, refund 2h/1h, pending TTL, auto-complete) tính theo Hanoi clock.
- Server lưu: giờ tạo (UTC), giờ bắt đầu slot (xác định theo Hanoi), giờ webhook/xác nhận. Ngày đặt & khung slot luôn `Asia/Ho_Chi_Minh`.
- Client (prod) có queue LocalStorage/IndexedDB riêng cho **đặt-chờ/retry** (không cho toàn bộ dữ liệu); retry dùng cùng `idempotency_key`. Data local không dùng để kết luận đơn đã xác nhận. (Chưa build — P2, xem `worklog.md`.)

## 6. Bảo mật & quan sát

- JWT Bearer (fail-fast nếu thiếu key; key ≥ 32 ký tự ở prod). CORS đọc chuỗi Cors:AllowedOrigins; rỗng hiện any-origin cả production. Auth policy 5/60s đã gắn; global policy 100/10s khai báo nhưng chưa áp toàn API. Lockout dựa trên năm sai trong cửa sổ 10 phút, in-memory; Redis chưa có.
- TARGET log có correlation ID, mã lỗi, độ trễ và booking ID, lọc dữ liệu nhạy cảm. CURRENT middleware log lỗi chưa có contract correlation/request_id; không có nghiệp vụ GPS trong Badminton. Theo dõi: số đặt trùng, pending hết hạn, webhook lỗi, dung lượng Storage/DB, độ trễ API p95. Target: API p95 < 500 ms (k6), 0 double-booking, uptime/SLA cần xác nhận, chưa có số đo live.

Chi tiết dữ liệu: `02-database-design.md`. Giao diện: `07-ui-design-system.md`. Triển khai: `06-deployment.md`.

## 7. Ranh giới transaction và giới hạn kiến trúc

| Ranh giới | CURRENT | Yêu cầu cần đạt |
| --- | --- | --- |
| Browser → API | JWT localStorage, fetch wrapper, cache React Query | Không tin role/price/owner client; refresh sau mutation |
| API → service | Controller role attribute + service ownership/scope | Role không thay scope dữ liệu; deny by default |
| Service → DB | UnitOfWork, transaction, raw SQL, unique index | Chống race bằng constraint/conditional update; không dùng app lock như bảo đảm đa instance |
| Payment → gateway | Strategy mock/on-site; refund gọi ngoài transaction | Outbox/đối soát hoặc cơ chế phục hồi crash cần thiết kế trước gateway thật |
| Jobs → DB | Scoped service, polling in-process | Retry idempotent; atomic trạng thái/ledger/voucher; quan sát backlog |
| Auth → DB | JWT claims và BCrypt | Kiểm tra user khóa/role đổi khi token chưa hết hạn |

Không có CQRS, queue offline, Redis, Hangfire hay upload Storage chỉ vì tài liệu đề xuất. Thêm một phụ thuộc cần kế hoạch cấu hình, migration, test và vận hành tương ứng.

## 8. Contract HTTP và ví dụ

Ví dụ tạo đơn **TARGET sử dụng API hiện hữu**, từ client đã login:

```json
{
  "courtId": 1,
  "date": "2026-10-20",
  "slotId": 13,
  "playerCount": 2,
  "groupId": null,
  "voucherCode": null,
  "pointsToRedeem": 0,
  "idempotencyKey": "client-generated-unique-key",
  "note": null
}
```

ID/ngày là ví dụ, phải lấy dữ liệu thật hợp lệ. Mutation create hiện trả HTTP 200 envelope, không mặc định 201. Không hardcode `statusCode` JSON thành số cho mọi response: enum converter controller và serialization middleware có thể khác; HTTP status là căn cứ xử lý lỗi.

| HTTP | Cách client xử lý |
| --- | --- |
| 400 | Validate trường/ngày/payload; giữ dữ liệu form |
| 401 | Yêu cầu login lại; không retry vô hạn |
| 403 | Thông báo không có quyền; không lộ dữ liệu |
| 404 | Không tìm thấy hoặc không còn tài nguyên |
| 409 | Phân biệt SLOT_CONFLICT và business conflict; không mặc định mọi 409 là trùng sân |
| 429 | Thông báo chờ; retry có backoff theo contract thực |
| 500/network | Không kết luận create thất bại chắc chắn; xác minh idempotency trước gửi lại |

CURRENT có lỗi quyền trả 409 và validation không đồng nhất; chuẩn hóa error contract là việc code cần kiểm thử HTTP, không chỉ sửa docs. Quyền endpoint cụ thể đọc controller và [01 §9](01-business-analysis.md#9-sai-khác-current-đã-xác-minh-trong-source).
