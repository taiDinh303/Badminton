# 05 — Chiến lược kiểm thử và nghiệm thu Badminton

> Chủ tài liệu: **Đinh Văn Tài**. Cập nhật: **16/10/2026**.
> Rà source thấy 23 phương thức Fact; chưa chạy lại test trong nhiệm vụ docs. Worklog cũ ghi 23/23 pass là bằng chứng lịch sử, không phải kết quả lượt này.

## 1. Phạm vi kiểm tra

Ưu tiên không đặt trùng, đúng quyền, không mất tiền/điểm/voucher, đúng giá và ngày UTC+7. Các mức bằng chứng không thay thế nhau:

| Mức | CURRENT | Cần bổ sung |
| --- | --- | --- |
| Static | Source/controller/entity/config | Link/contract và điểm thiếu có truy vết |
| Build/lint | Có dotnet project/npm scripts | Chạy đúng path/SDK, lưu kết quả |
| Unit | Có Fact tính tier/thời gian trong suite | Hàm thuần giá/boundary theo yêu cầu |
| Service/DB integration | TestHost DI + SQL Server thật | Không coi là HTTP integration |
| HTTP integration | Chưa thấy full host suite | JWT/attribute/middleware/validation/401/403/429 |
| Browser/E2E | Chưa có Playwright/Vitest script | Customer/staff/admin, mutation/refresh/lỗi mạng |
| Load | Có k6 script, assertion còn thiếu | Tải phân loại lỗi đúng, DB hậu kiểm |
| Cloud/provider | Chưa xác minh | PostgreSQL/migration/RLS/Storage/gateway sandbox |

## 2. Suite hiện có và giới hạn

Nguồn [Badminton.Tests](../Backend/Badminton.Tests); TestHost tạo service provider, principal test và DB riêng; EnsureCreated thay vì migrate. 23 Fact gồm các file:

| File | Coverage tìm thấy trong source |
| --- | --- |
| DoubleBookingTests | Cạnh tranh slot/hạn mức cá nhân |
| ConcurrentRefundTests | Refund đồng thời/idempotency |
| WebhookReplayTests | Callback lặp/fail/unknown ref/điểm một lần |
| VoucherExhaustionTests | Tổng lượt/concurrency/dùng lại/release |
| LifecycleAndRedeemTests | Complete/no-show/redeem/restore và scope một số thao tác |
| TimezoneGuardTests | Guard +30 phút/refund tier/múi giờ |

Không suy ra đã test API route/authorize, background worker thật hoặc migration startup. Test pin HanoiTime.NowUtc; expiry job còn dùng DateTime.UtcNow, nên cần kiểm thử riêng cho TTL.

## 3. Môi trường và lệnh

Hướng dẫn đầy đủ ở [10](10-setup-local.md). Từ root repo:

```powershell
$env:BADMINTON_TEST_SQLSERVER = '.\SQLEXPRESS'
dotnet test .\Backend\Badminton.Tests\Badminton.Tests.csproj -c Debug
```

Test tạo/xóa BadmintonTest_*; chỉ dùng instance test/local. LocalDB override khi cài sẵn. DB ứng dụng không dùng EnsureDeleted. Build.ps1/CI hiện path cũ và cần DEV-01; đừng báo pipeline xanh từ mô tả cũ.

Frontend từ Frontend: npm ci → npm run lint → npm run build. Chưa có npm test trong package.json. Migration test cần DB sạch và DB từ bản trước; service tests EnsureCreated không thay thế.

## 4. Dataset nghiệm thu

Chuẩn bị khách A/B, hai manager khác branch, manager chưa gán branch, admin; sân low/peak, ngày thường/cuối tuần, ngày bảo trì; voucher đủ/hết lượt/hết hạn; ledger earn/redeem; Pending/Confirmed/Completed/NoShow/Refunded.

Pin giờ trước/sau 30 phút, 1 giờ, 2 giờ, TTL 10 phút; boundary 23:59/00:00 UTC+7, cuối tháng/năm. Test retry cùng key, key khác actor/payload, callback đảo thứ tự và restart worker. Không dùng tài khoản/dữ liệu cá nhân thật.

## 5. Ma trận quyền bắt buộc

| Actor và thao tác | Kết quả TARGET | Rủi ro CURRENT |
| --- | --- | --- |
| Anonymous tạo đơn/đọc payment | 401 | Phải test host JWT thật |
| Khách A đọc/hủy đơn B | Từ chối, không mutation | AuthorizeCancel còn lỗi SEC-01 |
| Khách tự đăng ký Admin/Manager | Từ chối | Service có guard; HTTP test còn thiếu |
| Manager A list/summary/payment cơ sở B | Từ chối hoặc filter server về A | SEC-02 |
| Manager A complete/no-show đơn B | Từ chối | Lifecycle thiếu scope |
| Manager null branch thao tác khách | Từ chối | Staff scope chưa fail closed |
| Manager tạo voucher/slot/staff/audit | TARGET từ chối theo đặc tả | CURRENT POST/PATCH slot cho Manager; SEC-03; SPA chưa Admin guard |
| Admin thứ hai/khóa admin cuối | Từ chối | Index tối đa một; cần service/concurrency test |
| Token user đã khóa/đổi role | Không mutation vượt quyền | JWT claims chưa tự refresh |

Kiểm cả HTTP status, response không lộ dữ liệu và DB không đổi; sidebar ẩn không là bằng chứng.

## 6. Ma trận nghiệp vụ

| ID | Kịch bản | Kết quả cần chứng minh |
| --- | --- | --- |
| BR-02 | Slot trước/sau +30p, ngày +30/+31 | Đúng boundary và ngày Việt Nam |
| BR-03 | Low 100.000, peak 150.000, cuối tuần +20% | Low=120.000, peak=180.000 cuối tuần, không dùng giá peak cho low |
| BR-03 | Đổi giá sân sau tạo đơn | Tổng/breakdown lịch sử nhất quán, không tính lại sai |
| BR-04 | Hai khách cùng slot, retry key | Một active booking; retry không nhân đôi/không lộ actor khác |
| BR-05 | Callback success/fail/duplicate/late | Confirm/earn tối đa một; terminal không hồi sinh; đối soát muộn |
| BR-06 | Refund ≥2h, đúng 1h, <1h | 100%/50%/0%; payment riêng, ledger đúng, retry không trùng |
| BR-06 | Cancel Confirmed rồi refund | Không mất đường hoàn; CURRENT PAY-01 chưa đạt |
| BR-07 | Nhóm owner/member/người ngoài, 10/11 người | Owner đặt; capacity/giới hạn đúng; không vượt quyền |
| BR-08 | Voucher concurrent/hết lượt/cancel Pending | UsedQty không vượt/âm; đúng discount, trả lượt một lần |
| BR-09 | 200.000 VND thu, đổi 1.000 điểm | Earn=200.000 điểm theo thời điểm được chốt; discount=20.000 VND |
| BR-09 | Hai đơn đồng thời tiêu cùng balance | Không âm do race; ledger không trùng |
| BR-10 | No-show trước giờ/đúng kỳ/qua tháng | Đúng mốc DEC-03, đủ ba lần chặn tháng kế, mở đúng kỳ |
| BR-10 | Review owner Completed/lặp/non-owner | Chỉ một review hợp lệ; không upload giả từ string |
| BR-11 | Lifecycle/bảo trì/role/refund | Audit trước/sau đúng và actor truy vết được |
| BR-12 | Pending/Success/Refunded/low/peak cùng kỳ | Doanh thu khớp tiền, occupancy lọc peak và denominator đúng |

DEC-01/02/03 cần chốt trước dùng expected result cho thời điểm earn/restore/no-show. Không sửa expected để test hợp thức lỗi.

## 7. Load k6 — CURRENT và việc cần sửa

[Script](../loadtest/concurrency_booking.js) có burst rate 50 request/giây trong 10 giây (xấp xỉ 500 iterations nếu không drop), 50 VU cấp trước/tối đa 100; **không phải đúng 50 request**. Có login N_USERS mặc định 1000 và read fan-out 1000 VU/30 giây.

Check hiện chỉ status 200/409 và không auth/500; chưa đếm đúng một winner hoặc xác nhận mọi 409 là SLOT_CONFLICT. Threshold http_req_failed<5% còn xung đột với 409/429 hợp lệ. Cùng một customer token cho burst cũng có thể gặp hạn mức; không suy ra 1+49 từ script.

TARGET sửa script trước nghiệm thu: dataset/tokens phù hợp → phân loại expected conflict/rate-limit → counter winner/conflict/unexpected → hậu kiểm DB active count=1 và không thừa payment/ledger/voucher → summary p95 theo endpoint/scenario. Đặt BOOKING_DATE rõ theo UTC+7, không tin tomorrowISO dùng UTC gần nửa đêm.

Root repo, sau chuẩn bị API/test token và slot hợp lệ:

```powershell
$env:API_URL = 'http://localhost:5131/api/v1'
$env:TEST_TOKEN = '<customer-test-token>'
$env:ADMIN_TOKEN = '<admin-test-token>'
$env:BOOKING_DATE = '<yyyy-MM-dd>'
k6 run .\loadtest\concurrency_booking.js
```

Không chạy tải trên live. p95 booking <500ms/read <1s là mục tiêu, chưa có số đo lượt này. Ghi request count/dropped iterations/throughput/expected conflict/429/unexpected 5xx và DB hậu kiểm, không chỉ p95.

## 8. Browser và khả năng tiếp cận

Kiểm 375/768/1024/1440px; bàn phím/focus/dialog/error/contrast; mạng chậm/disconnect; tạo đơn timeout/retry; mutation invalidate cache; refresh detail/deep link. QR CURRENT là tile display-only, phải thay mã thực và test scan/server verify trước tuyên bố check-in.

Kiểm staff mở route admin-only, không lộ dữ liệu qua cache sau logout/đổi user; method payment phải được API chấp nhận, URL gateway phải được sử dụng đúng.

## 9. Ghi kết quả và gate

Mỗi lần ghi ngày UTC+7, commit, OS/SDK/Node/DB/provider, lệnh, số pass/fail/skipped, artifact, lỗi còn mở, phần chưa chạy. Không lưu token/password/raw dữ liệu khách.

Chỉ nghiệm thu dùng thật khi P0 quyền/hủy-hoàn/giá/báo cáo đã đóng, callback sandbox đúng, jobs phục hồi, browser flow đạt, backup restore thành công. P0 còn mở giữ trạng thái chưa đạt. [worklog](worklog.md) giữ bằng chứng; [08](08-plan.md) quản lý backlog.
