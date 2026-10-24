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

## 6. Migration fingerprint — CURRENT 24/10/2026

Hai provider có migration AddBookingRequestFingerprint riêng, chỉ thêm Bookings.RequestFingerprint nullable dài 64; giữ nguyên dữ liệu và migrations lịch sử. Migration được tạo/kiểm model offline, không tự áp lên DB ứng dụng. Production cần review/backup và thao tác phát hành riêng theo quyền owner.

Fingerprint dùng intent ban đầu gồm actor, sân/ngày/slot, số người, nhóm, voucher, điểm yêu cầu và note. Key cũ có fingerprint null không thể tái dựng intent chính xác nên từ chối replay, không tự hash số điểm thực đã cap hoặc dữ liệu voucher có thể đã được giải phóng. Lock request và account là transaction-owned; services tham gia cùng scoped UnitOfWork. Cơ chế này không thay thế đối soát gateway khi timeout/crash.

## 7. Readiness, retry và kiểm thử phát hành — CURRENT 24/10/2026

`/ready` chỉ trả 200 khi kết nối DB, không có migration còn thiếu và heartbeat các job bật còn hợp lệ. Schema lỗi chỉ trả nhãn sanitized; health không áp migration. Đây là kiểm lịch sử migration, không kiểm toàn vẹn mọi bảng nếu schema bị sửa thủ công.

Notification Pending/Failed đã lưu được worker WaitlistPromotionJob thử lại tối đa 5 bản ghi/tick, cách lần thử trước ít nhất 2 phút; HTTP relay timeout 10 giây. Khóa ledger tránh gửi đồng thời khi replay. Đây là retry ít nhất một lần: crash sau relay thành công trước commit vẫn có thể gửi lại; chưa có transactional outbox gắn tất cả business transitions hay provider reconciliation.

CI có guards, build/model/config hai OS, SQL Server LocalDB tests, PostgreSQL16 service tests, frontend unit/lint/build/typecheck E2E, Chromium mock và browser gọi API/DB thật, Docker build không publish. Có timeout, concurrency và upload kết quả lỗi. GitHub run và Docker build phải xác nhận trên runner sau commit; không có job deploy.

Chạy tích hợp browser từ `Frontend/`, sau `npm run build`: `npx playwright test --config playwright.integration.config.ts`. Cần BADMINTON_TEST_SQLSERVER hoặc BADMINTON_TEST_POSTGRES trỏ server loopback riêng. Host nằm ở Backend/BrowserTests, chỉ dùng fixture BadmintonTest_* với password synthetic; không nằm trong image API. Không dùng credential ứng dụng/live. Host shutdown sau suite để dọn fixture database.


## 8. Callback và hoàn tiền — CURRENT 24/10/2026 sau security fixes

Native callback: VNPAY GET `/api/v1/payments/callback/vnpay` (response giữ nguyên `RspCode`/`Message`); MoMo POST `/api/v1/payments/callback/momo`; ZaloPay POST `/api/v1/payments/callback/zalopay`. Wrapper POST `/webhook/{provider}` vẫn còn nhưng reference, merchant, amount và trạng thái phải được chứng minh bởi payload ký; status/amount bên ngoài chữ ký không có thẩm quyền. VNPAY chỉ thành công khi cả response và transaction status là 00. Duplicate fields, legacy MoMo responseKey và legacy Zalo appSignature bị từ chối.

Production real cần thêm `Payment:Momo:AccessKey` (khóa do merchant được cấp, không phải khóa suy ra từ requestId) và `Payment:Zalopay:CallbackKey` (key2/callback key). Không ghi giá trị vào repo. MoMo HMAC-SHA256 ký toàn bộ IPN theo thứ tự tài liệu; ZaloPay HMAC-SHA256 ký nguyên chuỗi data. Tham khảo [VNPAY IPN](https://sandbox.vnpayment.vn/apis/docs/thanh-toan-pay/pay.html), [MoMo notification](https://developers.momo.vn/v3/docs/payment/api/result-handling/notification/), [ZaloPay callback](https://docs.zalopay.vn/docs/specs/callback-api/).

CURRENT: `OnlineRefundService` lưu intent trước IO vào `OnlineRefundOperations`, `HttpOnlineRefundClient` có transport refund/query VNPAY/MoMo/ZaloPay. `Payment:OnlineRefunds:Enabled=false` mặc định; chỉ bật sau nghiệm thu merchant sandbox. API legacy `IPaymentGateway.RefundAsync` vẫn từ chối `ONLINE_REFUND_UNAVAILABLE` vì thiếu metadata/idempotency bền vững; payment service đi qua coordinator mới cho online thật. Chỉ khi provider xác nhận hoàn xong mới đổi booking/ghi refund ledger/trả điểm-voucher trong transaction. Timeout/crash giữ `refund_uncertain`; lần gọi sau chỉ query cùng intent, không gửi lại lệnh refund. Cash/QR tại quầy giữ nghiệp vụ xác nhận của nhân viên. NOT VERIFIED: provider sandbox/tiền thật/callback ingress; người dùng xác nhận chưa có merchant sandbox trong lượt này. Test synthetic không thay chứng nhận gateway.

JWT kiểm lại user active, verified, chưa xóa, role và SecurityStamp khớp DB mỗi request có xác thực. Đổi mật khẩu/thu hồi phiên đổi stamp, token cũ bị từ chối. TARGET còn lại: refresh/reset/OTP và nghiệm thu vận hành.

## 9. Hoàn tiền bền vững và diễn tập vận hành — CURRENT local 24/10/2026

- Migration mới: SQL Server `20261024120154_AddDurableOnlineRefunds`, PostgreSQL `20261024120256_AddDurableOnlineRefunds`; thêm metadata provider trên Payments và bảng thứ18 `OnlineRefundOperations`. Không tự áp vào DB ứng dụng/live. PostgreSQL bật RLS và revoke quyền PUBLIC/anon/authenticated trên bảng/sequence mới; chỉ backend truy cập.
- `Payment__OnlineRefunds__Enabled=true` yêu cầu `Payment:Provider=real`, đủ credential adapter và `Payment__Vnpay__ApiEndpoint` HTTPS đúng môi trường. Không bật flag để bỏ qua sandbox. Refund sử dụng provider transaction ID từ callback đã ký; VNPAY lưu chính xác vnp_CreateDate của checkout. Đơn lịch sử thiếu metadata bị từ chối, không suy diễn từ BookingCode.
- Staff đúng branch gọi `POST /api/v1/payments/reconciliation/{paymentId}/refund`: intent cũ chỉ query; khoản thu late/additional/conflict chưa có intent có thể yêu cầu hoàn toàn phần. Client không gửi amount. Pending initialization chưa rõ kết quả không thể tùy ý đánh dấu đã xử lý. UI Đối soát hiển thị lỗi/chờ xác minh, chỉ báo thành công khi API hoàn tất.
- Rehearsal: [OperationsRehearsal](../tools/OperationsRehearsal/README.md) chỉ nhận BadmintonTest_* loopback, native BACKUP/RESTORE hoặc pg_dump/pg_restore, đối chiếu dữ liệu/migration/index và kiểm unique slot sau restore. Tám process complete, tám process expiry, một process giữ khóa bị kill và process mới phục hồi khóa. CI có bước tương ứng; remote runner vẫn NOT VERIFIED.
- TARGET: merchant sandbox/certification, production restore rehearsal theo quyền riêng, endurance và quyết định RPO/RTO. Diễn tập local không tự đóng các gate này.

Nguồn protocol: [MoMo refund/query](https://developers.momo.vn/v3/docs/payment/api/payment-api/refund/), [ZaloPay refund/query](https://developer.zalopay.vn/v2/general/overview.html), [VNPAY refund/query](https://sandbox.vnpayment.vn/apis/docs/truy-van-hoan-tien/querydr%26refund.html). Credential và dữ liệu mẫu của tài liệu nhà cung cấp không đưa vào cấu hình dự án.
$110. Lưu liên hệ công khai — 23/10/2026

CURRENT source: POST /api/v1/contacts cho khách chưa đăng nhập, 3 lần/phút/IP; server kiểm họ tên, SĐT Việt Nam, email, độ dài lời nhắn và consent. Honeypot loại submission bot; không ghi thông tin liên hệ vào log. GET /api/v1/contacts?page=1 và PATCH /api/v1/contacts/{id}/handled chỉ cho JWT role Admin.

Có migration AddContactInquiries riêng trong hai thư mục provider, không sửa migration lịch sử. Trước khi phát hành API có form liên hệ, cần backup/review/apply migration tương ứng bằng quy trình triển khai hiện hành; PostgreSQL mới nhất là 20261023155315_AddContactInquiries, SQL Server là 20261023155259_AddContactInquiries. PostgreSQL bật RLS và thu hồi quyền PUBLIC/anon/authenticated; .NET tiếp tục là cổng ghi/đọc qua connection server. Production không auto-migrate hoặc seed. Chưa áp dụng migration lên database live trong nhiệm vụ giao diện này; chưa nghiệm thu lưu/đọc thực trên live. Không có email/SMS tự động cho form này; đội ngũ Admin xem và liên hệ từ hộp thư.
