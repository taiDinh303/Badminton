# WORKLOG — Hệ thống đặt sân cầu lông

> **Chủ tài liệu: Đinh Văn Tài** (github.com/taiDinh303) · **Đồng đội: Phùng Vĩnh Luân** (github.com/thankiemsama).
> File này là **điểm bắt đầu cho bất kỳ AI/dev** làm tiếp. Cập nhật sau mỗi lượt có thay đổi thật.
> Ghi sự thật đã kiểm chứng; **không đánh dấu "xong" khi chưa kiểm tra**. Quy tắc đọc/ghi: xem `00-README.md` §Rules.

## 1. Trạng thái hiện tại — 20/10/2026, UTC+7

- CURRENT .NET 8/SQL Server/JWT BCrypt + React 19/Vite/TypeScript. Có 17 bảng, waitlist/notify/check-in token/membership và signed gateway adapters trong source mới pull.
- Theo yêu cầu chuẩn hóa toàn bộ docs/ASP.NET/React, đã áp dụng patterns Marixa nhưng giữ policy/route/schema/danh tính Badminton; không dùng tài nguyên hoặc thông tin hosting Marixa.
- Layering: API Configuration/Bootstrap/Security, repository DI và mapping lỗi provider; contracts/business không tham chiếu implementation persistence, EF/SqlClient/HttpContext. ICurrentUser là ranh giới identity.
- Production config fail closed, startup không migrate/demo seed; check-config không side effect. Publish bỏ environment JSON, manifest public chưa có host live.
- React app/modules/services/utils/theme; lazy routes, shared nav/sidebar/header/mobile menu, clear cache theo user và 401 reset. Giao diện giữ nội dung đặt sân, xanh/vàng thể thao.
- Local backend 83 test và frontend Node 11/Playwright 8 đạt, bằng chứng §4; không đồng nghĩa gateway/cloud/worker đa instance đã nghiệm thu.

## 2. Lịch sử ghi nhận 14/10/2026 — chưa xác minh lại runtime

> Giữ nội dung lịch sử bên dưới để truy vết. Các khẳng định coverage, clock, scope, restore và k6 ở bản ghi cũ đã được giới hạn/đính chính bởi §1, §8 và tài liệu chuyên trách; không dùng làm bằng chứng kiểm tra hiện tại.

- **Múi giờ (critical fix):** mọi deadline (guard +30p, refund 2h/1h/0%, pending-expiry, availability) chuyển `DateTime.UtcNow` → `HanoiTime` tại `Core/Utils/HanoiTime.cs`. Trước đó khách đặt được slot đã qua + tỷ lệ hoàn lệch ±7h.
- **State machine:** `BookingService.MarkCompletedAsync/MarkNoShowAsync` + `AutoCompleteJob` (Confirmed qua slot end → Completed) + endpoint `POST /bookings/{id}/complete` & `/no-show`.
- **No-show gate:** `User.NoShowCount/NoShowPeriod/NoShowLimited/NoShowLimitedPeriod`; 3 lần/tháng → chặn đặt tháng kế; `NoShowResetJob` tự mở; kiểm tra trong `BookingService.CreateAsync`.
- **Branch-scope:** `PaymentService.AuthorizeStaffScope` + `MaintenanceService` chặn manager thao tác cơ sở khác.
- **Redeem điểm:** `PointService.RedeemAsync/RestoreAsync` (LedgerKey idempotent), `Booking.PointsRedeemed`, hoàn full → restore điểm.
- **Quản lý nhân viên:** `StaffService` + `POST /api/v1/admin/staff/*` (tạo/lock/unlock/đổi role/gán cơ sở), guard "đúng 1 admin active" (`ux_active_admin`).
- **Bảo mật API:** CORS (`Cors:AllowedOrigins`), `AddRateLimiter` (global + bucket `auth` 5/60s), lockout login 5 sai/10p (in-memory), `[Authorize]` cho `GET /vouchers/{code}`.
- **Sửa kỹ thuật:** `GetByUserPagedAsync` (push enum vào SQL), `IsSlotConflict` (SqlException.Number 2601, không parse message), dọn `RefundLocks` (chống leak), audit filter Hanoi→UTC boundary.
- **Database:** migration `20261014063758_AddNoShowTrackingAndPointsRedeemed` (4 cột User + 1 cột Booking). `BadmintonBooking.sql` dev cập nhật.
- **Test:** thêm `TimezoneGuardTests` (3) + `LifecycleAndRedeemTests` (5); `TestHost` seed 2 branch + manager 2 cơ sở + slot 09:40, pin `HanoiTime.NowUtc`. → **23/23 pass**.
- **Load test:** `loadtest/concurrency_booking.js` (k6, 50 VU cùng slot, assert 1 winner + 49 SLOT_CONFLICT; + login storm 1000 VU + dashboard fan-out).

## 3. Ma trận chức năng CURRENT

| Khu vực | Có source / kiểm chứng local | Giới hạn |
| --- | --- | --- |
| Auth | SĐT/BCrypt/JWT, guard role và active user | Chưa OTP/refresh recovery đầy đủ |
| Booking | Active slot unique, window/limit, owner/key binding, price snapshot | Chưa full payload hash/concurrent balance hardening |
| Payment/cancel | Mock/real adapters, on-site, CancelRefund, chữ ký/replay tests | Chưa sandbox/live/đối soát crash |
| Points/voucher | Earn Completed, full refund restore, redemption/ledger tests + account summary (points/voucher của tôi) + mỗi mã voucher dùng 1 lần/tài khoản (GlobalVoucherCapTests) | Kiểm tiếp nhiều instance/balance race; chốt định giá theo người vs theo slot (08-plan) |
| Manager scope | List/summary/detail/payment-read/lifecycle/check-in shared guard | Full endpoint role matrix release chưa hoàn tất |
| Groups/review | Owner booking/review Completed; UI module | Upload ảnh chưa có |
| Waitlist/notify | Queue/promotion/notification ledger và test stub/log | Chưa chứng minh gửi ngoài/đa instance |
| Membership | Tier/config multiplier/admin UI có source | Không tự xác nhận bán gói subscription |
| QR/check-in | QR mã hóa token thực; POST token body, nhập/dán/USB/camera BarcodeDetector, branch scope | Chưa nghiệm thu thiết bị thật |
| Jobs/health | 5 worker SQL applock/heartbeat, health/ready/metrics | Chưa load/restart/crash rehearsal live |
| React | Module/routes/API/theme/nav/session; browser mock | Không thay HTTP/DB/payment acceptance |
| Cloud/Storage/Postgres | Chưa tích hợp hoặc chưa xác minh | Dự án cutover riêng |

## 4. Bằng chứng kiểm tra

### 20/10/2026 — chuẩn hóa toàn stack theo Marixa

- Rà source Marixa Configuration/Bootstrap/persistence DI/theme/modules và Badminton checkout 2bb3d87; không thay source Marixa.
- SDK 9.0.317, Node 24.21.0, SQL Server local default instance '.'; fixture tạo/xóa BadmintonTest_* riêng.
- Backend Release build, architecture/config/service/SQL/HTTP checks; EF model check không thay migration/schema.
- npm test: 11 pass; lint/build đạt; Playwright: 8 pass, mocked API. Đã xem ảnh mobile manager, desktop screenshot được tạo qua flow login.
- Production publish + scripts/Test-ProductionConfiguration.ps1: đạt, valid dry-run/invalid fail-closed, không Development JSON, không DB/network/worker.
- CONFIGURATION_OK Development qua check-config; CiGuard đạt.
- Test giá cũ phụ thuộc weekday bị fail vào cuối tuần; sửa expected theo phụ thu thật, không đổi chính sách. VNPAY parser dùng System.Web.HttpUtility để Services không cần ASP.NET WebUtilities; signature tests kiểm hồi quy.
- Kiểm cuối: dotnet build Backend/Badminton.slnx -c Release --no-restore -warnaserror đạt 0 warning/error; dotnet test Backend/Tests/Badminton.Tests.csproj -c Release --no-build --no-restore với BADMINTON_TEST_SQLSERVER=.: 83 pass, 0 fail/skip, gồm vòng HTTP đặt → on-site → hủy/refund.
- EF --configuration Release --no-build has-pending-model-changes: No changes have been made to the model since the last migration. 4 migration giữ nguyên; không áp migration vào DB ứng dụng.
- npm test 11 pass, npm run lint không warning, npm run build đạt; bundle entry gzip khoảng 109.5–109.7 KB, các feature lazy-load, không còn warning chunk >500 KB.
- Playwright 8 pass; đã xem ảnh desktop customer và mobile manager. Ảnh dùng dữ liệu test, không commit artifact. HTTP tests real/local và browser mock được ghi riêng.
- python scripts/Validate-Repository.py: 21 Markdown, 128 link, 33 bảng/project reference, 0 lỗi; CiGuard 5/5 đạt. git diff --check trước commit.
- Chưa chạy gateway sandbox/live, tải k6, cloud, upload Storage hoặc worker crash/multi-instance rehearsal.


### Lần chuẩn hóa docs 16/10/2026

- Đọc cấu trúc/mẫu docs Marixa và source/config Badminton; đối chiếu controller/service/mapping/test/CSS/route, không dùng hạ tầng Marixa làm snapshot Badminton.
- Xác nhận tĩnh: 15 bảng mapping, 23 Fact; SDK máy rà soát 9.0.317, Node 24.21.0.
- Chưa chạy dotnet build/test, npm build/lint, k6, API, browser hoặc cloud. Nhiệm vụ chỉ Markdown; không báo kết quả ứng dụng mới.
- Kiểm tra 15 file Markdown: một H1/file, code fence cân bằng, UTF-8/không ký tự lỗi, đường dẫn link nội bộ tồn tại; git diff --check -- docs không báo lỗi. Kiểm tra cuối: 105 liên kết nội bộ (kèm anchor), 45 bảng, 15 file đều đạt; 0 lỗi cấu trúc/liên kết; git diff --check -- docs sạch.

### Kết quả lịch sử trong bản ghi cũ

Ngày 14/10 ghi build 0 warning/error, 23 test pass với SQL Express; chưa có log runtime mới. Lệnh cũ dùng thư mục Badminton/ hiện không còn phù hợp. Lệnh hiện hành ở [10-setup-local.md](10-setup-local.md). Lượt chuẩn hóa tên tiếp theo đã sửa script/CI và xác minh local; xem sự kiện mới nhất.

## 5. Việc tiếp theo

[08-plan.md](08-plan.md) là backlog/tiêu chí chuẩn; [14-release-checklist.md](14-release-checklist.md) là gate. Ưu tiên sandbox payment/đối soát, full idempotency/balance race, báo cáo theo tiền/kỳ, migration/restore, worker đa instance và QR chuẩn. Không dùng cấu trúc sạch/build xanh như xác nhận production.

## 6. Log sự kiện (mới trên cùng)
### 22/10/2026 — Sửa EF model check trong CI Ubuntu/Windows

- Nguyên nhân CURRENT: cùng bước `has-pending-model-changes` của matrix Ubuntu/Windows không chỉ định context, trong khi repo đã có `BadmintonBookingDbContext` (SQL Server) và `PostgresBookingDbContext` (PostgreSQL), gây lỗi “More than one DbContext was found”.
- Sửa `.github/workflows/ci.yml`: hai bước EF riêng, chỉ định `--context` tương ứng, dùng `--configuration Release --no-build` sau build có sẵn. Mỗi bước đặt provider đúng, môi trường Testing/JWT synthetic/connection loopback; migrate/seed/jobs=false. Giữ lịch sử migration/model/schema/API/UI; cập nhật hai lệnh hướng dẫn trong docs05/10. Giữ thay đổi người dùng `TaiKhoan.txt`/`build.ps1` ngoài commit.
- Xác minh root: `dotnet tool restore`8.0.28 đạt; `dotnet build Backend/Badminton.slnx -c Release --no-restore`0warning/0error. Tại `Backend`, chạy đúng hai lệnh EF mới với env giống workflow: cả hai exit0, “No changes have been made to the model since the last migration.” Không áp migration/kết nối DB live. Lượt PostgreSQL đầu thiếu env provider làm host validation báo sai cú pháp rồi factory vẫn kiểm đạt; bổ sung provider và cấu hình synthetic cho cả hai bước, lượt cuối sạch.
- `dotnet test Tests/Badminton.Tests.csproj -c Release --no-build --no-restore --filter FullyQualifiedName~ConfigurationAndArchitectureTests`23/23pass. `./scripts/CiGuard.ps1`5checks đạt; bundled Python `scripts/Validate-Repository.py`22Markdown/131links/33tables/0lỗi; `git diff --check` đạt.
- NOT VERIFIED: CI GitHub Ubuntu/Windows của commit mới sau push; local kiểm trên Windows/SDK10.0.401 theo global.json, CI giữ SDK9/runtime8. Không chạy SQL/HTTP/browser vì không đổi các boundary runtime; không deploy hoặc sửa dữ liệu.

### 22/10/2026 — Bổ sung giao diện điện thoại, bảo toàn giao diện PC

- Phạm vi người dùng: tối ưu toàn bộ giao diện điện thoại, không thay đổi giao diện PC. Giữ nguyên thay đổi local `TaiKhoan.txt` và `build.ps1`; không deploy/push, không chạm DB hay API contract.
- CURRENT: thêm `Frontend/src/mobile.css`, chỉ tác động viewport <768px hoặc điện thoại cảm ứng xoay ngang (hover none/pointer coarse, cao <512px/rộng <1024px). Các component chỉ thêm class hook; style PC hiện hữu giữ nguyên. Header/menu/bottom nav gọn và safe-area; bỏ padding lặp của feature pages; KPI/form/filter/phân trang co hàng, input 16px và nút tối thiểu44px. Bảng quản trị giữ đủ cột trong vùng cuộn ngang, có hướng dẫn và focus bàn phím. Drawer đặt sân/modal cuộn theo chiều cao thực, nút đóng và CTA có thể tiếp cận trên màn hình ngang thấp.
- TARGET nghiệm thu: 17 route khách/vận hành ở320/360/390/430/767px không làm trang tràn ngang; login nhỏ/ngang, touch390px, drawer/modal667×375, bảng/form membership320px, điện thoại ngang932×430 và đổi lại desktop. Baseline51ảnh trước sửa (17route ×768/1024/1440px) lưu ở `artifacts/responsive-desktop-baseline` ngoài Git. Browser mock chặn toàn bộ API và font remote, fixed time; không gọi dữ liệu live.
- Lệnh tại `Frontend`: `npm run build` đạt (TypeScript + Vite), `npm run lint` đạt, `npm test`11/11 đạt. Root: bundled Python chạy `scripts/Validate-Repository.py`22Markdown/131links/33tables/0lỗi; `git diff --check` đạt. Chỉ đổi UI nên không chạy SQL/HTTP/backend/configuration suites.
- Kiểm tra trung gian: full browser30/30 đạt trước hai case bảng/form và landscape rộng. So PNG byte-exact phát hiện69pixel viền bo lệch1mứcRGB ở trang admin/bookings, không phải thay đổi bố cục; chuyển sang comparator ảnh Playwright threshold0.01/maxDiffPixels0. Một lượt chạy đồng thời hai suite dùng chung preview4173 gặp4connection-refused khi suite trước tắt server; chạy lại tuần tự, không sửa ứng dụng để che lỗi hạ tầng test.
- Khi chuyển comparator, Playwright chuẩn hóa dấu gạch dưới trong tên snapshot thành gạch ngang, nên lượt kế báo thiếu baseline; đổi tên file test và sao chép đúng51ảnh trước sửa sang tên chuẩn, không dùng ảnh sau sửa làm baseline. Lệnh `python` hệ thống đầu tiên kích hoạt Python Install Manager tự cài3.15.1; validator sau đó dùng Python bundled của Codex.
- Kết quả cuối tại `Frontend`: `$env:VERIFY_DESKTOP_BASELINE='1'; npx playwright test --workers=1` **32/32 pass**,0fail/skip,2phút; gồm19test cũ và13test responsive mới,85tổ hợp route/mobile-width và51ảnh PC đối chiếu đạt (threshold0.01,maxDiffPixels0). Baseline được chụp trước sửa bằng `$env:CAPTURE_DESKTOP_BASELINE='1'; npx playwright test e2e/responsive.spec.ts --grep 'desktop appearance' --workers=1`3/3. Default CI không cần ảnh ignored: vẫn kiểm geometry/mobile và sidebar PC; bật VERIFY chỉ khi có baseline trước sửa. Đã xem ảnh mobile320px trang chủ/dashboard/membership và drawer390px; artifacts/test-results không đưa vào Git.
- NOT VERIFIED: thiết bị iOS/Android và Safari/Firefox thật, bàn phím hệ điều hành/camera, font Google và dữ liệu/luồng ghi live. Không coi browser Chromium mock hoặc build là nghiệm thu mọi điện thoại/production. Website đang deploy chưa được cập nhật trong nhiệm vụ này.

### 21/10/2026 — Tải trước dữ liệu, review chi tiết, voucher theo mã và dữ liệu mẫu

- Phạm vi: các URL Render người dùng chỉ định và ba ảnh; cho phép bổ sung dữ liệu mẫu reviews/membership/vouchers. Giữ thay đổi local TaiKhoan.txt/build.ps1; không đưa secrets/artifacts vào Git.
- CURRENT: warmSession nạp module + dữ liệu màn đầu theo quyền, ba worker; account/profile/points, groups/waitlist, reviews theo sân, booking-list + ba detail đầu, availability cơ sở đầu, các màn quản trị khi đủ quyền. Dừng hàng đợi, hủy query và xóa cache khi đổi danh tính/quyền hoặc logout; cập nhật tên hồ sơ không xóa cache. Không tải toàn lịch sử vô hạn; cache memory120s/gc600s, availability stale10s/poll15s và Pending poll5s. Dữ liệu cache không bị thay bằng spinner khi refetch nền. Lần đầu/ngày lọc mới vẫn cần tải thật.
- Booking detail thêm userId (additive), owner Completed mọi role gồm Admin có thể đánh giá sao + nhận xét2000 ký tự; tải lại hiện review đã gửi, không gửi trùng. Người khác chỉ xem review, server vẫn guard owner/Completed/unique booking. Review làm mới rating sân và review list.
- Bỏ nhãn ngày lặp trên date input. Slot02:00 là bản ghi Id22 sai StartTime (Label14:00) có sẵn; chỉ deactivate giữ lịch sử. Booking availability sort giờ và lọc theo giờ mở cửa branch; Create từ chối ngoài giờ cả khi gọi API trực tiếp.
- Voucher bỏ HasUsedOtherVoucherAsync; mỗi khách mỗi mã một lần, mã khác vẫn dùng được. Preview báo đúng mã đã dùng, summary vẫn có mã chưa dùng; input không khóa cả tài khoản. Repository transaction lock theo user ngăn race khác court; giữ unique redemption và total cap. Lịch sử MaxPerUser>1 vẫn bị giới hạn1 theo yêu cầu mới. Hủy Pending giữ chính sách trả reservation.
- scripts/data/sample-reviews-membership-vouchers.sql là thao tác riêng có transaction/advisory lock và guard collision/idempotency. Diễn tập BadmintonTransfer_20261021 loopback55432 hai lần:6 user mẫu/6 review/6 voucher, không trùng. Áp Supabase otwhjkimfzxabbnaktpl bằng MCP execute_sql theo yêu cầu, SELECT xác minh6/6/6,0 slot02:00 active. Tài khoản và nhận xét có nhãn MẪU; mã DEMO*, 6 Completed mẫu0đ quá45ngày, không thêm Payment/PointLedger; không sửa tier của khách cũ.
- Kiểm thử hiện tại: build/lint frontend và Node11/11 đạt; focused browser2/2 (Admin owner review + reload/không gửi trùng; prefetch cache/không gọi admin bằng Customer/voucher vẫn nhập được). Full SQLServer107/107 đạt, gồm HTTP regressions, outside-hours và voucher race. Full PostgreSQL106/106 đạt (loại SQLServer-only upgrade), browser full19/19 đạt; docs validator22Markdown/131links/33tables/0lỗi; git diff --check đạt.
- Lượt kiểm đầu báo assertion cap-global/MaxPerUser cũ không còn đúng yêu cầu; cập nhật test phân biệt mã. Test total-cap dùng6 khách khác nhau (trước chỉ3 khách lặp, nay bị per-code cap chặn trước total-cap). Browser test mới ban đầu nhầm role button thay vì tab của Pills, sửa locator và focused2/2 đạt. Không chạy fixture/load test trên cloud/app DB.
- LIVE source ef3e7ac: Render API dep-dbce75tg1s2s739ubc1g Live (1m36s), Render Static dep-dbce75on74is7388rljg Live (31.9s, clean cache), Vercel8hzzid8WxyidT4ttiy1otMq7rGXc Ready/Production/Current (10s). Đã push codex/cloud-supabase lên cả hai repo. UI Vercel báo GitHub Outage/delayed automatic deploy, nên chạy manual deploy từ nhánh đúng; không thay môi trường/domain/secrets/plan.
- Browser live Render sau deploy: đăng nhập Admin hiện có; đơn301 Completed hiện form sao + nhận xét cho owner; không gửi review thử vào đơn thật. Reviews hiển thị6 nhãn MẪU, membership6 user MẪU có Gold/Silver/Expired, voucher đủ6 DEMO codes; /book ngày22/10 không có02:00 và không còn dòng weekday/date lặp, drawer voucher enabled + hướng dẫn theo từng mã. Console error0. Ảnh trong artifacts/{reviews-sample-live,membership-sample-live,vouchers-sample-live,booking-grid-updated-live,voucher-rule-updated-live}.png. HTTPS /ready=ready, DBok, cả5 workerok; GET reviews/court1 trả review mẫu. Đăng xuất phiên kiểm tra, giữ lịch sử dữ liệu cũ và thay đổi user TaiKhoan.txt/build.ps1 ngoài commit. PG test server đã dừng.
- NOT VERIFIED: các filter/ngày chưa mở vẫn cần fetch, lỗi mạng/RenderFree cold start có thể loading; chưa chạy gateway thật/thiết bị camera/worker crash/multi-instance. Luồng ghi được kiểm bằng fixture local/HTTP và browser mock; không tạo đơn hoặc thanh toán thử trên live. Dữ liệu mẫu được áp riêng theo yêu cầu, không bật startup seed/migrate.

### 21/10/2026 — Supabase cutover và sửa cấu hình cloud

- Phạm vi trực tiếp từ người dùng: chuyển database source lên Supabase tổ chức tugxiczrpnesubccgbuq; sửa Vercel deployment 3jrvtCdQZCj8A3DLiQwJvBSEuYpB và Render Static srv-dbc8lv8ae00c738vmc80. Người dùng chọn chuyển toàn bộ dữ liệu local và Production thanh toán tại sân trước. Không mua dịch vụ.
- CURRENT: thêm Npgsql EF8, PostgresBookingDbContext/migration snapshot riêng, giữ nguyên lịch sử SQL Server. SQL điều kiện dùng ANSI quoted identifiers/boolean parameter; repository ánh xạ unique violation và transaction advisory lock. Session job lock implementation nằm trong repository; API giữ metrics/orchestration. Sửa DateTime UTC boundary audit/report cho PostgreSQL.
- Production onsite chỉ đăng ký Cash/QR; không đăng ký mock/online gateways. GET payments/methods và UI chỉ hiện phương thức đang bật; xem hướng dẫn tại quầy không xác nhận đã trả tiền. Giữ guard server về role/branch.
- Docker API multi-stage SDK9/runtime8, user app, CA Supabase chính thức, TLS VerifyFull, cổng 8080. render.yaml là template; Production vẫn không migrate/seed. Connection/JWT đặt trong Environment, không VITE_* hoặc Git.
- Render lỗi gốc đã đọc log: Static Site đặt Root Directory=Backend, build trống, publish dist không tồn tại. Đã đổi Frontend + npm ci && npm run build + dist; deployment dep-dbcajuifngtc739j65kg Live. Thêm SPA rewrite /* → /index.html. Vercel cũ Ready nhưng không có VITE_API_URL, gọi API vào host static; fork thankiemsama/Badminton cũ hơn checkout taiDinh303/Badminton.
- Supabase otwhjkimfzxabbnaktpl ban đầu public trống. Áp InitialPostgres và RestrictInternalRlsHelper (2 EF migrations/2 Supabase migration entries): 17 bảng ứng dụng + EF history, 18 bảng RLS, revoke anon/authenticated table access và quyền thực thi helper event-trigger rls_auto_enable. Advisor sau sửa: 0 WARN, 18 INFO RLS không có policy là chủ đích API .NET sở hữu truy cập.
- Chuyển dữ liệu: tools/DatabaseTransfer đọc SQL Server local trong Serializable transaction, chỉ nhập target đã migrate và trống, khóa target, COPY phía client, kiểm mọi giá trị/ID, reset sequences, commit cuối; lỗi log không lộ connection/row data. Đã diễn tập local BadmintonTransfer_20261021, rồi chuyển Supabase qua Session pooler chính xác aws-0-ap-southeast-2.pooler.supabase.com:5432; source không bị ghi. Lần row-by-row đầu bị NpgsqlException và rollback; đổi COPY, diễn tập lại thành công trước lượt cloud cuối.
- Snapshot cloud đã kiểm SELECT sau commit: Users=505, Courts=6, Bookings=302; toàn bộ 17 bảng được đối chiếu giá trị/ID: Branches3, TimeSlots18, Vouchers3, PlayGroups2, AuditLogs33, BookingPlayers2, CourtDateStatus6, GroupMembers4, Notifications2, Payments2, PointLedger1, Reviews0, VoucherRedemptions1, WaitlistEntries0. UTC lịch sử giữ nguyên, độ chính xác làm tròn microsecond theo PostgreSQL. Không chạy test/load helper trên DB local ứng dụng hoặc cloud.
- Kiểm chứng: dotnet test Backend/Tests/Badminton.Tests.csproj -c Release --no-restore với BADMINTON_TEST_POSTGRES loopback: 97/97 pass (loại đúng SQL Server-only migration upgrade test); cùng suite SQL Server BADMINTON_TEST_SQLSERVER=.: 98/98 pass gồm upgrade. Sau thêm 5 case provider/TLS/onsite: filter ConfigurationAndArchitectureTests 23/23 pass. Production publish + scripts/Test-ProductionConfiguration.ps1 đạt; dry-run cấu hình cloud thật CONFIGURATION_OK/exit0, không DB/worker/HTTP.
- Frontend npm run build, npm test 11/11, npm run lint đạt; npm run test:e2e cuối 17/17 pass (API mock). Test onsite kiểm Cash/QR, online bị ẩn, khách xem hướng dẫn vẫn Pending. Lượt đầu assertion dùng nhãn chưa đúng và một test waitlist bị timing fail; sửa nhãn đúng Chờ thanh toán, chạy focused2/2 và full17/17 đạt. Không đổi test waitlist để che lỗi.
- CURRENT cloud: commit 62c59a2 được đẩy nhánh codex/cloud-supabase lên taiDinh303/Badminton và fork thankiemsama/Badminton để dùng integration Vercel hiện có. Không ghi đè main. API Render srv-dbccc661egvs73a76ju0, deployment dep-dbccc6m1egvs73a76kug Live, Docker build thành công trên Render Free/Singapore. URL https://badminton-api-w86i.onrender.com.
- HTTPS smoke chỉ đọc bằng Invoke-WebRequest: /health=200, /ready=200 db=ok và cả 5 worker heartbeat=ok, /api/v1/payments/methods=200 với Cash/QR. CORS trả đúng hai origin Vercel/Render đã cấu hình; origin untrusted.example không có Allow-Origin. Trước deploy, publish API chạy tạm port15131, jobs=false, không migrate/seed, đọc Supabase thành công (3 branches, 5 active courts), đã dừng process.
- Frontend Vercel deployment 9mDVRD92cevQFC2ArX9sPW5KoSYu (c842a2f) Ready/Production/Current tại https://badminton-ten-smoky.vercel.app. Render Static dep-dbccpogm7kps73a8fnb0 Live, clean build 20.8s tại https://badminton-bvqs.onrender.com; lần build cache trước kẹt npm ci 14m50s đã cancel, clear build cache và build lại đạt. Cả hai dùng VITE_API_URL tới API mới; kiểm bundle shared ui xác nhận endpoint đúng, deep link /auth trả200.
- Browser live: đăng nhập bằng tài khoản Admin đã có trên cả Vercel và Render thành công, đọc home 3 cơ sở/5 sân active; Vercel dashboard/report, audit và availability ngày sau tải được; không lỗi console. Không tạo đơn/chuyển tiền/xác nhận thanh toán thử trên live. Đăng xuất sau kiểm tra. Khi kiểm tra phát hiện TimeOnly dùng hh (12 giờ), sửa HH (24 giờ) trong Branch/TimeSlot/Booking (cả label/error/availability). Regression mới xác minh 18:00–19:00 và close23:00 trên các service + booking. PG targeted7/7, SQLServer8/8 pass. Lượt PG đầu thiếu server local (8 fail kết nối), khởi động cluster cô lập; lượt tiếp 7 pass/1 fail do SQLServer-only upgrade, chạy filter đúng7/7 đạt. Không đổi historical test để che lỗi.
- Xác minh cuối bản sửa giờ: commit70f011c, API deployment dep-dbcd433ncjis7394ksug Live (1m26s); HTTPS /ready db và cả5 worker ok; branches trả close23:00/23:00/22:00, methods Cash/QR. Một request /ready gặp502 trong lúc đổi instance, kiểm lại đã200. PG test cluster loopback đã dừng. Source người dùng TaiKhoan.txt và build.ps1 vẫn giữ nguyên thay đổi chưa commit.
- NOT VERIFIED: gateway thật, camera/USB và worker multi-instance/crash chưa nghiệm thu. Render Free có thể ngủ, worker không chạy khi instance ngủ; không cam kết vận hành liên tục. Luồng ghi live không chạy thử vì giữ dữ liệu đã chuyển.


### 21/10/2026 — Giải phóng cổng 5131 khi Visual Studio báo address already in use

- CURRENT / nguyên nhân: screenshot dừng tại app.RunAsync vì một instance API khác đã bind 5131. Xác minh cả IPv4/IPv6 thuộc PID 3672, executable `D:/BadmintonBooking/Backend/API/bin/Release/net8.0/Badminton.API.exe`, là API đã chạy ở lượt trước. Dừng đúng exec session bằng Ctrl+C; không đổi port/API contract, không kill toàn bộ dotnet và không chạy helper reset DB.
- `build.ps1`: `-StopApi` chỉ dừng Badminton.API.exe có đường dẫn trong Backend/API/bin của checkout hiện tại và đợi process kết thúc. `-Run`/`-StopApi` kiểm listener 5131 trước build; nếu còn process khác thì báo PID/hướng dẫn Ctrl+C, Shift+F5 hoặc lệnh của repo, không tự tắt process lạ.
- Cập nhật docs/10-setup-local: chỉ chạy API bằng Visual Studio hoặc terminal tại một thời điểm; `./build.ps1 -StopApi` rồi F5, hoặc `./build.ps1 -StopApi -Run` nếu dùng terminal. Không dùng loadtest/start_api.ps1 vì có reset DB. Sau sửa để API dừng, nhường cổng cho Visual Studio của người dùng.
- Xác minh: `./build.ps1 -StopApi` Debug build đạt 0 warning/error. Dùng TcpListener loopback tạm trên 5131, gọi `./build.ps1 -Run`: từ chối trước build/start, báo đúng PID; listener gốc vẫn bound, được dispose trong finally. Parser PowerShell build.ps1 đạt. `python scripts/Validate-Repository.py`: 21 Markdown, 128 links, 33 tables/project references, 0 lỗi. `git diff --check -- build.ps1 docs/10-setup-local.md` đạt.
- NOT VERIFIED: thao tác F5 trên Visual Studio của người dùng sau khi giải phóng cổng. Không chạy SQL/HTTP/browser suite vì chỉ đổi script chạy local và hướng dẫn; không sửa code nghiệp vụ, DB/config/seed hoặc khởi chạy lại API ở cuối lượt này.


### 21/10/2026 — Hoàn thiện check-in token, membership, account, reviews/waitlist và lịch bảo trì

Phạm vi theo 8 URL người dùng giao; giữ các thay đổi local có sẵn (audit/voucher/account, config, SDK, TaiKhoan.txt), không commit/push/deploy.

CURRENT / thay đổi source:
- Check-in: chi tiết đơn trả QrToken/CheckedInAt; thay tile ngẫu nhiên bằng QR mã hóa token thực (`qrcode`). Thêm POST `/checkin/token` nhận token trong body, giữ các route cũ. Sửa FE đọc `bookingId` camelCase, làm mới roster, nhập/dán/USB Enter và camera BarcodeDetector (có fallback, dừng media tracks khi đóng).
- Membership: admin chọn/tìm khách theo tên/SĐT thay nhập ID; giữ endpoint members, thêm customers, đổi/gia hạn/hủy, phân biệt còn hiệu lực/hết hạn; loại khách đã khóa/xóa và từ chối enum không hợp lệ.
- Account: GET/PATCH `/account/profile` theo CurrentUserId, chỉ cho sửa họ tên/email/URL ảnh HTTP(S); cập nhật tên trong session, hiển thị tier/hạn/hệ số thực. Không cho client đổi ID/role/phone. Ảnh hiện hỗ trợ URL, chưa upload Storage.
- Reviews: “Tất cả” tổng hợp dữ liệu API từng sân, lọc từng sân, nêu lỗi tải và liên kết đánh giá từ đơn hoàn thành. Đọc local Development xác nhận hiện có 5 sân nhưng 0 review thật; không seed review giả.
- Waitlist/book: phân biệt “Đã đặt”, “Bảo trì”, “Đóng”, giờ đã qua; vẫn hiện sân có tất cả slot đã đặt. Khách chọn ô đã đặt để join waitlist ngay; Pending cũng được coi là đang chiếm chỗ. Hiện trạng thái hàng chờ thực, lỗi rời danh sách và link đơn được nhường.
- Voucher: API/view loại mã hết hạn khỏi khả dụng, preview/reserve báo “Voucher đã hết hạn sử dụng” kể cả khi mã đã khóa. Job dùng `ValidUntil < HanoiToday` để voucher còn dùng hết ngày cuối. Tài khoản không gợi ý voucher hết hạn/chưa hiệu lực/hết lượt; admin hiện “Hết hạn sử dụng”.
- Maintenance: thêm `StartTime/EndTime` nullable vào bảng CourtDateStatus qua migration **20261021090146_AddCourtMaintenanceHours**, giữ PK và migrations cũ; một khoảng cùng ngày/sân hoặc cả ngày khi bỏ giờ. Khóa khung giờ giao nhau; Open xanh, Maintenance vàng, Closed đỏ. Sửa theme bảo trì, đọc trạng thái lịch đã lưu, thêm giờ vào modal, refresh cache sau lưu.
- Lưu lịch và tạo booking dùng cùng SQL transaction-owned applock. Khóa lịch trước khi xử lý đơn; Pending hủy/trả điểm/voucher bằng conditional update; Confirmed hoàn toàn phần (khác khách tự hủy theo tỷ lệ). Dùng luồng payment/ledger/branch guard có sẵn; lỗi giữ khóa và hướng dẫn retry, audit giữ before/after. Không hiển thị lỗi provider thô.
- Khi chạy local phát hiện JobLock gọi stored procedure như SQL function khiến cả 5 worker thất bại. Sửa Acquire/Release dùng EXEC + return code, kiểm độc quyền bằng 2 SQL sessions. Worker expiry/waitlist đã chạy query được sau khởi động.

Bằng chứng:
- Root: `dotnet test Backend/Tests/Badminton.Tests.csproj -c Release --no-restore`, `BADMINTON_TEST_SQLSERVER=.`: **98/98 pass**, 0 fail/skip, khoảng 84s; build trong lệnh test không warning/error. Gồm SQL service + Kestrel/JWT/HTTP + configuration/architecture, không DB ứng dụng/live. Các lỗi hồi quy metadata validation trên record và fixture Reason đã được sửa trước lượt đạt cuối.
- Tests mới: khóa theo giờ/ngoài khoảng, full refund trong 40 phút trước chơi + retry không nhân refund, Pending trả điểm, sai branch/range, profile/URL validation + identity allowlist, voucher hết hạn, Pending waitlist/nhãn booked, token HTTP/casing/branch, 2-session job lock và worker expiry giữ ngày cuối.
- Migration regression dùng DB BadmintonTest_*_Upgrade riêng: migrate đến bản 18/10, tạo lịch khóa cũ, nâng cấp bản mới; giữ note/trạng thái, hai giờ null và vẫn khóa cả ngày. EF `migrations has-pending-model-changes --project Backend/Repositories --startup-project Backend/API --configuration Release --no-build`: không model drift.
- Frontend: `npm test`: **11/11 pass**; `npm run build` và `npm run lint` đạt. `npm run test:e2e`: **16/16 pass** trên built SPA, API mock; gồm 8 luồng mới. Có kiểm CSS màu xanh/vàng/đỏ thật; đã xem screenshot tài khoản và modal giờ bảo trì. Sửa mock helper ghép dư dấu ? để query URL khớp request thật.
- `dotnet run --project Backend/API/Badminton.API.csproj -c Release --launch-profile http -- --check-config`: CONFIGURATION_OK, không DB/seed/worker/network.
- `python scripts/Validate-Repository.py`: 21 Markdown, 128 links, 33 tables/project references, 0 lỗi. `git diff --check` đạt (chỉ cảnh báo chuyển LF/CRLF theo Git Windows).

Development local (được tách khỏi tests):
- Xác nhận cấu hình trỏ `(localdb)\MSSQLLocalDB`, database BadmintonBooking; API 5131 chưa chạy ở đầu kiểm tra runtime.
- COPY_ONLY/CHECKSUM backup thành công tại `artifacts/local-db-backup/BadmintonBooking-before-maintenance-hours-20261021.bak` (Git ignored), rồi `dotnet tool run dotnet-ef database update 20261021090146_AddCourtMaintenanceHours --project Backend/Repositories --startup-project Backend/API --configuration Release --no-build`: thành công, từ 4 lên 5 migrations.
- Khởi chạy API Release Development trên localhost:5131 với `DatabaseInitialization__MigrateOnStartup=false` và `DatabaseInitialization__SeedDemoData=false`; giữ frontend dev sẵn có tại localhost:5173. HTTP read-only: health 200, ready 200, frontend reviews 200, courts trả 5 sân. Không chạy test/load helper trên DB này.

NOT VERIFIED / giới hạn: camera và máy quét USB thiết bị thật; upload ảnh (hiện dùng URL); gateway/ngân hàng thật, crash window hoặc nhiều instance giữa gateway/ledger; không gọi các kết quả mock/local là nghiệm thu production. Mỗi sân/ngày hiện một khoảng bảo trì cùng ngày; không có lịch lặp hoặc nhiều khoảng độc lập.


### 21/10/2026 — Sửa 3 lỗi UI/nghiệp vụ: audit "đối tượng", voucher đặt sân, điểm & voucher tài khoản

Triệu chứng (từ user, 3 trang):
- `/admin/audit`: cột ĐỐI TƯỢNG hiển thị "vượt dòng" chồng lên hàng liền kề dù đã có cột thời gian riêng. Nguyên nhân: ô cell thực thể của `VirtualizedTable` render 1 dòng dài ("Bảo trì sân · Sân #2 · 18/10/2026") → wrap thành 3 dòng trong cột 200px, cao hơn `rowHeight=48` → tràn sang hàng dưới.
- `/book`: nhập voucher rồi "Xác nhận đặt sân" → lỗi "Số tiền đơn thấp hơn mức tối thiểu của voucher". Nguyên nhân: **frontend** định giá đơn theo `giá × số người` (group booking, 10 người = 1.500.000đ) nhưng **backend** `BookingService` tính `total = giá/1 slot` (150.000đ) để kiểm `min_spend` → preview (1.500.000đ) và submit (150.000đ) lệch nhau.
- `/account`: không hiện điểm tích lũy (đã đặt sân mà không thấy điểm) và không hiện trạng voucher đã dùng (mỗi tài khoản chỉ dùng 1 voucher). Nguyên nhân: chưa có endpoint cho dữ liệu "của tôi" (points balance, voucher used).

Cách sửa (source):
- `Frontend/src/components/VirtualizedTable.tsx`: cell thêm `min-w-0` + `overflow-hidden` để nội dung không tràn khỏi cột.
- `Frontend/src/modules/admin/pages/AuditPage.tsx`: tách `renderEntityId` thành object `{id, detail}`; ô "Đối tượng" render khối 2 dòng có giới hạn (label dòng 1, id+ngày dòng 2, `truncate`), không còn wrap 3 dòng.
- `Backend/Services/Booking/BookingService.cs`: `total = perPerson × playerCount` (giá 1 người × số người) khớp preview của frontend; giữ `perPerson` = công thức peak/low + cuối tuần của `PricingService`.
- `Backend/Services/Voucher/VoucherService.cs`: thêm `HasUsedOtherVoucherAsync` — quy tắc "mỗi tài khoản chỉ dùng 1 voucher" (khối `HasUsedOtherVoucherAsync` trong cả preview và reserve), giữ nguyên check `MaxPerUser` theo từng voucher.
- Mới: `ModelViews/Account/AccountDtos.cs`, `Contract.Services/Account/IAccountService.cs`, `Services/Account/AccountService.cs` (GetPointsAsync, GetMyVouchersAsync, GetAccountSummaryAsync), `API/Controllers/Account/AccountController.cs` (`GET /api/v1/account/points|vouchers|summary`, `[Authorize]`). Đăng ký DI trong `Services/DI/ServiceRegistration.cs`; TestHost thêm register.
- `Frontend`: `types.ts` (PointSummary/MyVoucher/AccountSummary + khôi phục RevenueByDay/OccupancyByDay), `api/index.ts` (`api.account.*`), `AccountPage.tsx` (hiện điểm thực + trạng thái voucher đã dùng/khả dụng), `BookPage.tsx` (khóa input voucher + ghi chú khi đã dùng voucher).
- Mới: `Backend/Tests/GlobalVoucherCapTests.cs` (4 test: cap global voucher khác voucher, re-use cùng voucher, account summary, scope user của points/voucher).

Bằng chứng kiểm tra:
- `dotnet build Backend/Badminton.slnx -c Release --no-restore`: 0 warning, 0 error.
- `dotnet test Tests/Badminton.Tests.csproj -c Release --no-build`: **87/87 pass** (83 có sẵn + 4 mới). VoucherExhaustion/LifecycleAndRedeem/Feature/CancelRefund không đổi hành vi (tất cả playerCount=1, total giữ per-slot).
- `Frontend`: `tsc --noEmit` (tsconfig.app.json) sạch; `npm run lint` 0 warning/0 error (44 files); `npm run build` đạt, entry gzip ~109.7 KB, feature lazy-load.

Giới hạn / cần chốt:
- Định giá **theo người** (× số người) khác mô tả "giá là VND/giờ" trong `01-business-analysis` §3. Đây là chính sách được áp dụng để khớp preview UI; **ghi đề xuất chốt ở 08-plan** (chưa tự coi là quyết định chủ dự án). Nếu chốt giá theo slot (1 giờ = 1 đơn giá, số người chỉ cap), cần đảo lại `total` và bỏ `× playerCount`.
- Chưa nghiệm thu browser live (cần chạy API + DB thật). Chưa đổi chính sách điểm/voucher ngoài phạm vi "1 voucher/account".

### 21/10/2026 — Sửa lỗi resolve .NET SDK khi mở/build Backend

- CURRENT: máy chỉ có SDK 10.0.400 và 10.0.401; `global.json` yêu cầu 9.0.300 với `latestFeature`, khiến `dotnet --version` thất bại vì không có SDK 9 phù hợp.
- Đổi riêng `sdk.rollForward` sang `latestMajor`, giữ minimum 9.0.300 và `allowPrerelease=false`; cập nhật hướng dẫn local. Target của các project vẫn net8.0; CI đang cài SDK 9 vẫn đáp ứng minimum. Giữ các thay đổi local có sẵn trong StartupConfiguration.cs, appsettings.json và ICurrentUser.cs.
- Xác minh tại root và Backend: `dotnet --version` trả 10.0.401. Tại Backend: `dotnet build Badminton.slnx -c Release` đạt 0 warning, 0 error; `dotnet test Tests/Badminton.Tests.csproj -c Release --no-build --no-restore --filter FullyQualifiedName~ConfigurationAndArchitectureTests` đạt 18/18, 0 fail/skip. `git diff --check` đạt.
- NOT VERIFIED: reload IDE, CI remote và runtime nghiệp vụ. Không chạy SQL/HTTP/browser suite vì không thay các boundary đó; không khởi chạy API, migration, seed hoặc truy cập dữ liệu live.

### 20/10/2026 — Chuẩn hóa docs, ASP.NET và React theo kiến trúc tham khảo

- Tách repository DI/unique error; Core ICurrentUser và API adapter; pure StartupConfiguration, DatabaseInitializer, health endpoints, five-job flag, per-IP limiter và safe publish.
- Sửa pin SDK không có trên máy/CI từ 10.0.401 sang SDK9/latestFeature, giữ target net8.0; Node24/config validator/env examples cùng manifest.
- React chuyển pages sang feature modules, tách transport/types/format/theme/app providers/routes/navigation. Giữ facade lib/api tương thích; desktop/mobile cùng registry, thêm staff menu/skip link, expired session và cache isolation.
- Bổ sung scope server cho manager list/summary/detail/payment read/lifecycle/check-in, active user create guard, retry actor/slot binding, stable historical amount. Tiền snapshot gồm phụ thu, không bịa split chưa lưu.
- Đồng bộ docs 00–14/glossary/runbooks và onboarding/AGENTS/architecture. Gate chưa đạt vẫn mở, không tự deploy hoặc dùng dịch vụ thật.


### 16/10/2026 — Chuẩn hóa tên backend và push toàn bộ theo yêu cầu

- Chuyển cấu trúc Backend đang có vào Git; API/Badminton.API.csproj, namespace Badminton.API; Tests/Badminton.Tests.csproj; Core namespace Badminton.Core; Context/BadmintonBookingDbContext.cs. Solution groups và RootNamespace đồng bộ.
- Giữ tên các tầng và database/migration/API contract; không sửa các lỗi nghiệp vụ SEC/PAY/REP trong nhiệm vụ đổi tên.
- Sửa build.ps1 dùng path theo script, chỉ dừng API khi -StopApi; cập nhật CI SDK 9 + runtime 8, EF tool manifest 8.0.28, thêm lint/build frontend. global.json chọn SDK 9.0.300/latestFeature (máy dùng 9.0.317).
- Cập nhật docs và helper loadtest theo đường dẫn mới; design-time factory hỗ trợ env chuẩn cùng alias legacy. Không chạy helper reset DB/load test.
- dotnet build Backend/Badminton.slnx -c Debug: đạt, 0 error, 1 warning CS1998 có sẵn trong TimezoneGuardTests.
- BADMINTON_TEST_SQLSERVER=. dotnet test Backend/Tests/Badminton.Tests.csproj -c Debug --no-build --no-restore: 23 passed, 0 failed/skipped.
- dotnet tool restore: đạt; EF has-pending-model-changes: No changes have been made to the model since the last migration.
- npm ci, npm run lint, npm run build: đạt. Vite cảnh báo chunk chính >500 KB minified (gzip 170.23 KB), chưa tối ưu bundle trong nhiệm vụ này. build.ps1 đạt (incremental 0 warning/error); full build trước đó có warning CS1998 như ghi trên.
- Kiểm 110 link nội bộ, table/fence/H1, solution/ProjectReference và JSON config: 0 lỗi; PowerShell parser cho build.ps1/start_api.ps1 đạt. Git diff --check kiểm trước commit.
- File cấu hình Visual Studio *.user không được đưa lại vào Git ở cấu trúc mới; giữ local theo .gitignore. Node_modules/dist/bin/obj không được commit.
- Chưa chạy smoke API/browser/cloud hoặc load k6; CI remote chưa xác minh. Các P0 nghiệp vụ vẫn mở.


### 16/10/2026 — Chuẩn hóa bổ sung theo bộ docs Marixa, dành riêng Badminton

- Người dùng cấp phạm vi hoàn thiện toàn bộ Markdown. Thay quy tắc đặc tả đóng băng bằng bảo trì docs trong phạm vi được giao.
- Bổ sung 08-plan, 09-deploy-detail, 10-setup-local, 11-setup-cloud, 12-figma-sync; hoàn thiện các file hiện hữu và glossary, giữ đường dẫn/tên cũ còn dùng.
- Phân biệt CURRENT/TARGET/PENDING/NOT VERIFIED; sửa path Backend, đơn vị điểm, quan hệ voucher, giới hạn unique admin, QR minh họa, rate-limit/correlation, k6 request count/assertion và migration/test coverage.
- Ghi sai khác ứng dụng bằng mã SEC/PAY/DATA/REP/DEV; không sửa code hoặc đổi chính sách chưa chốt để hợp thức lỗi.
- Giữ lịch sử/bản ghi trước và mọi thay đổi local ngoài docs; chưa commit/push. Kiểm tra cấu trúc/liên kết/diff docs; không chạy suite ứng dụng.


### 16/10/2026 — Viết lại & hoàn thiện bộ tài liệu docs/
- **Viết mới `00-README.md`** (entry point, quick facts dev-baseline vs production-target, rules) theo mẫu Marixa.
- **Hoàn thiện `01`–`07` + `glossary.md`** cho khớp **code đang chạy thật** (SQL Server, int PK, JWT/BCrypt, IHostedService, mock gateway, React 19 + Tailwind v4), tách rõ **dev baseline vs production target** ở mọi file. Không còn mơ hồ Supabase/uuid/Hangfire như bản cũ.
- **Đổi tên `06-deployment-vercel-supabase.md` → `06-deployment.md`** (Part A dev local + Part B production). Xóa file `06-deployment-vercel-supabase.md` cũ.
- **Cập nhật `worklog.md`** (file này) làm điểm bắt đầu chính xác cho AI/dev.
- Chỉ chỉnh Markdown trong `docs/`; không sửa code.

### 14/10/2026 — (bản trước, giữ nguyên) Múi giờ HanoiTime, state machine, no-show gate, branch-scope, redeem điểm, quản lý nhân viên, bảo mật API, k6 script; 23/23 test pass.

## 7. Quy tắc cập nhật worklog

- Mỗi lượt: ghi ngày + thay đổi thật + bằng chứng; chuyển việc xong khỏi §5.
- Bug: triệu chứng + cách tái hiện + mức ảnh hưởng + trạng thái; khi hết ghi cách sửa + ngày đóng.
- Bảo trì docs cùng nhiệm vụ được giao theo [00](00-README.md). Chính sách chưa chốt ghi đề xuất/tác động ở [08](08-plan.md), không tự coi là quyết định.

## 8. Giới hạn còn mở

| Khu vực | Còn cần làm |
| --- | --- |
| Payment | Signed adapter/stub test chưa là sandbox certification; xử lý timeout/crash và đối soát ngoài |
| Idempotency | Chưa hash đầy đủ voucher/points/note; unique key toàn hệ thống cần policy retention |
| Điểm | Concurrent balance giữa nhiều đơn; clawback theo tier/điểm đã tiêu cần mở rộng tests |
| Reporting | Thống nhất ngày chơi vs ngày PaidAt/refund, UTC boundary/refund-only day và denominator bảo trì |
| Role/slot | TimeSlot POST/PATCH cho Manager còn khác mục tiêu Admin cấu hình |
| QR/Storage | QR token chuẩn đã có; camera/USB thiết bị thật chưa nghiệm thu; chưa upload/policy ảnh |
| Jobs | App lock/heartbeat có source; chưa chứng minh mọi crash window không mất ledger/notify |
| Operations | RPO/RTO/retention/host/plan/metrics ingress chưa chốt; không xác nhận cloud |

Chi tiết ở [08](08-plan.md), [13](13-configuration.md), [14](14-release-checklist.md). Giữ lịch sử §2/§4/§6 theo ngày, không đọc bản ghi 16/10 như hiện trạng 20/10.

## 9. 23/10/2026 — Trang chủ /home

- Pull fast-forward main từ b49d39e lên 554184b; working tree sạch trước pull.
- CURRENT: HomePage có route riêng /home trong layout hiện tại, vẫn yêu cầu đăng nhập. / chuyển hướng /home; menu desktop/mobile, đăng nhập/đăng ký, fallback và redirect thiếu quyền cùng trỏ về /home.
- Kiểm tra tại Frontend: npm ci thành công; npm run build và npm run lint đạt; npm test 11 pass; npx playwright test e2e/auth.spec.ts e2e/navigation.spec.ts 8 pass. Browser dùng API mock, gồm mở trực tiếp/reload /home, chuyển hướng từ /, login, hết phiên và quyền Manager.
- python scripts/Validate-Repository.py: 22 Markdown, 131 links, 33 tables/project references, 0 lỗi. git diff --check đạt.
- Phạm vi frontend/docs; chưa deploy hoặc kiểm nghiệm API live. Người dùng yêu cầu commit và push các thay đổi /home lên origin/main.

## 10. 23/10/2026 — Trang giới thiệu công khai và Trang chính khách đặt

- Theo yêu cầu mới, thay thế thiết kế /home cần đăng nhập ở mục 9: /home là landing công khai ngoài Gate/Layout, không sidebar, không fetch API riêng tư; / chuyển về /home. Có header đăng nhập/đăng ký, minh họa sân, tiện ích, ba bước đặt sân và CTA. Minh họa không thể hiện tình trạng sân trống thực.
- /dashboard là Trang chính cần đăng nhập: lời chào, đồng hồ Việt Nam cập nhật mỗi giây, ô icon thao tác nhanh, danh sách cơ sở thực. Link cơ sở truyền branchId và màn đặt sân chọn đúng cơ sở đang hoạt động.
- /auth?mode=register mở đăng ký; auth có link về /home. Gate giữ đường dẫn nội bộ (kèm query), sau đăng nhập quay lại màn đang cần; auth trực tiếp về /dashboard. Không đổi HTTP contract, dữ liệu, chính sách giá/thanh toán hay quyền server.
- Header có avatar cursor pointer và dropdown; Escape trả focus, Tab/click ngoài đóng menu. Đăng xuất điều hướng công khai đồng bộ trước khi xóa identity để tránh race với Gate; query cache/session vẫn xóa theo provider hiện hành. Browser test đã phát hiện race và kiểm lại bản sửa.
- Bằng chứng cuối tại Frontend: npm run build, npm run lint đạt; npm test 11 pass; npx playwright test --workers=2: 40 pass, 0 fail (1.5 phút). Có public home 320/390/768/1440px, dashboard/mobile 320–767px, desktop 768/1024/1440px, login/booking/payment/waitlist/role/expired-session, branch shortcut, avatar và đồng hồ UTC+7.
- Đã xem ảnh public desktop/320px và dashboard/menu avatar mobile trong test-results; ảnh dữ liệu mock, không đưa artifact vào Git. Checklist React Best Practices: lazy pages, timer cleanup riêng, keyboard/focus, shared components/styles và guard được giữ.
- python scripts/Validate-Repository.py: 22 Markdown, 131 links, 33 bảng/project references, 0 lỗi; git diff --check đạt.
- Giới hạn: browser dùng API mock; không chạy backend/SQL vì không sửa boundary server. Công cụ web không đọc được hai URL Render trong lượt này; chưa xác minh phiên bản giao diện mới trên live hoặc thiết bị thật. Push source không là bằng chứng deploy thành công.

## 11. 23/10/2026 — Giao diện rộng, liên hệ khách và hộp thư Admin

- CURRENT: sửa trực tiếp checkout D:/My Projects/Badminton. Trang /home công khai và /Home hoạt động theo Router hiện hành; / chuyển tới /home. Mở Home không gọi API riêng tư hoặc ép đăng nhập. CTA đặt sân giữ intent sau auth. Source trước nhiệm vụ đã có route công khai; live Render đọc được HTTP 200 nhưng bundle khác source local, chưa xác minh nguyên nhân redirect live.
- Thiết kế chung xanh đậm/lime: hero rộng với sân CSS, form tư vấn, sidebar tối/icon/nhãn ngắn, header, dashboard, card/bảng/input, trang auth và bottom navigation. Logo sidebar về Home. Không thêm package. Tham khảo Courtsite (https://www.courtsite.my/) và Singapore Badminton Association booking (https://booking.singaporebadminton.org.sg/).
- Khách gửi họ tên/SĐT/email/lời nhắn không cần tài khoản; consent, kiểm dữ liệu, loading, lỗi giữ form, thành công sau HTTP response. API mới /api/v1/contacts, service + repository contract; bảng ContactInquiries, migration/snapshot độc lập SQL Server và PostgreSQL. POST có limiter 3/phút/IP và honeypot; không ghi PII vào log hay browser storage. GET phân trang 20 và PATCH xử lý chỉ Admin. UI /admin/contacts có gọi điện/email/đánh dấu xử lý.
- PostgreSQL migration bật RLS và REVOKE PUBLIC/anon/authenticated để không lộ PII qua PostgREST; chưa chạy DDL/data mutation live. Không đổi booking/payment/điểm/branch contracts. Production tiếp tục không auto-migrate.
- Bằng chứng Frontend: npm ci (0 vulnerabilities); npm run build, npm run lint đạt; npm test 11/11; node node_modules/@playwright/test/cli.js test --workers=2: 44/44 (56.7 giây). Bao gồm /Home, public Home 320/390/768/1440, reload, login intent, responsive các trang trong, đặt sân/payment/waitlist/role/expired session; contact thành công/lỗi/SĐT sai/Admin xử lý/Manager bị chặn. Đổi assertion màu mở sân theo palette mới sau khi regression bắt hardcode màu cũ.
- Backend: cài SDK9 và ASP.NET runtime8 ở thư mục work ngoài repo vì máy thiếu dotnet. dotnet test Backend/Tests/Badminton.Tests.csproj --filter FullyQualifiedName~ContactHttpTests|FullyQualifiedName~ConfigurationAndArchitectureTests --verbosity quiet: 29/29. Contact HTTP dùng Kestrel/JWT/validation/rate limiter thật với IContactService double, không DB; kiểm anonymous submit, không consent, SĐT/email sai, customer/manager/Admin và 429.
- dotnet ef migrations has-pending-model-changes cho cả BadmintonBookingDbContext và PostgresBookingDbContext: không thay đổi model chưa có migration. Sinh và review PostgreSQL migration SQL offline, chỉ tạo bảng/index/RLS/revoke/history mới. Build backend thành công trong các lệnh EF/test. Xem ảnh Home desktop/320px và dashboard desktop; dữ liệu ảnh nội bộ là mock. Checklist React: lazy feature pages, React Query invalidation, submit lock, labeled inputs, alert/status, không package UI mới.
- NOT VERIFIED: persistence DB thực/migration apply, email/SMS tự động (không triển khai), thiết bị thật và phiên bản mới trên Render. Chưa commit/push/deploy. Cần apply migration đúng provider trước phát hành API; build/browser mock không phải nghiệm thu production.
- Kiểm cuối: Validate-Repository.py qua Python runtime bundled đạt 22 Markdown/131 links/33 tables/project references, 0 lỗi; git diff --check đạt.

## 12. 23/10/2026 — Hiện/ẩn mật khẩu

- Thêm icon Eye/EyeOff trong trường mật khẩu /auth cho đăng nhập và đăng ký; type=button, nhãn tiếng Việt, aria-pressed/controls, padding tránh đè nội dung. Mặc định che mật khẩu và tự che lại khi đổi form, giữ nguyên giá trị. autocomplete theo current-password/new-password.
- npm run build đạt. Kiểm browser Playwright trực tiếp trên preview: đăng nhập hiện/ẩn, đăng ký hiện, đổi tab che lại, không đổi giá trị: PASS. Không thêm test file cho thay đổi UI nhỏ. Không đổi backend hoặc deploy live.
