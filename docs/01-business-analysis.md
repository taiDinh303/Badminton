# 01 — Phân tích nghiệp vụ hệ thống đặt sân cầu lông

> **Chủ tài liệu: Đinh Văn Tài.** Cập nhật: 16/10/2026.
> Tài liệu này là **nguồn duy nhất định nghĩa quy tắc nghiệp vụ**. Các file khác tham chiếu, không tự phát sinh quy tắc mới. Đây là TARGET nghiệp vụ; sai khác CURRENT được ghi tại §9 và kế hoạch. EN note: business rules are the single source of truth; other docs link here.

## 1. Mục tiêu và phạm vi

Hệ thống cho phép khách xem lịch trống và đặt sân cầu lông theo khung giờ, thanh toán trực tuyến hoặc tại quầy, quản lý đặt chỗ và nhóm chơi; quản lý vận hành từng cơ sở; một admin quản lý cấu hình và nhiều cơ sở. Giao diện và thuật ngữ dùng **tiếng Việt**; múi giờ nghiệp vụ là **`Asia/Ho_Chi_Minh` (UTC+7, fixed offset)** — mọi deadline nghiệp vụ tính theo giờ Hanoi, không theo múi giờ server/Vercel/vùng cloud.

**V1 gồm:**
- Hồ sơ khách đăng ký bằng **số điện thoại** (dev = mật khẩu BCrypt; prod = Supabase OTP).
- Tài khoản và phân quyền 3 role; đặt sân **cá nhân và nhóm**.
- Khung giờ cố định 1 giờ; giá peak/low + phụ thu cuối tuần.
- Thanh toán **VNPay/Momo/Zalopay** (trực tuyến) và **tại quầy (cash/QR)**; voucher; điểm tích lũy; đánh giá sau hoàn thành; lịch sử đơn; bảo trì sân; dashboard + báo cáo theo quyền.

**V1 KHÔNG gồm:** gói membership (vàng/bạc), huấn luyện viên, lớp học, bán hàng/phụ kiện, giải đấu, đa ngôn ngữ, tự động trừ phí no-show (V1 chỉ gắn cờ no-show và **giới hạn đặt** sau 3 lần), không tự động tính giá theo từng ngày lễ.

## 2. Người dùng và quyền

Ba role: `customer`, `manager`, `admin`. Khách tự đăng ký bằng số điện thoại; manager và admin do admin tạo. **Yêu cầu đúng một tài khoản admin đang hoạt động trên toàn hệ thống**. Index `ux_active_admin` bảo đảm tối đa một; bootstrap và service phải bảo đảm còn ít nhất một. Có thể có nhiều khách, nhiều manager.

| Khả năng | Customer | Manager | Admin |
| --- | --- | --- | --- |
| Xem lịch trống, đặt sân, thanh toán, nhận QR | Có | Có | Có |
| Hủy đặt chỗ của mình theo quy tắc hoàn tiền | Có | Có | Có |
| Tạo/quản lý nhóm chơi, mời thành viên, đặt cho nhóm | Có | Có | Có |
| Dùng voucher, xem/đổi điểm tích lũy, đánh giá | Có | Có | Có |
| Xem đặt chỗ/báo cáo **của cơ sở mình** | Không | Có | Có |
| Đánh dấu bảo trì, no-show, hủy đặt chỗ của khách | Không | Có | Có |
| Chỉnh giá sân, khung giờ, tạo voucher, quản lý nhiều cơ sở | Không | Không | Có |
| Cấp/khóa tài khoản, đổi role, xem audit toàn hệ thống | Không | Không | Có |
| Quản lý nhân viên (tạo/lock/unlock/đổi role/gán cơ sở) | Không | Không | Có |

- Hồ sơ khách và tài khoản đăng nhập tách biệt (khóa tài khoản vẫn giữ lịch sử).
- **Manager gắn với MỘT cơ sở** (branch-scope): `PaymentService.AuthorizeStaffScope` và `MaintenanceService` có kiểm tra một số thao tác; chưa đủ chứng minh scope trên mọi endpoint (xem §9). (Quyết định Q3=A.)
- **Đúng 1 admin active** được enforce bởi unique filtered index `ux_active_admin` (`[Status]='Active' AND [Role]='Admin'`). Tạo admin thứ hai hoặc khóa admin cuối cùng → bị từ chối.

> **Dev note (code):** TimeSlotController hiện cho Manager tạo/sửa slot, khác quyền chỉ Admin cấu hình; SEC-03 vẫn mở. Role lưu trong enum `Role { Customer, Manager, Admin }` ở `Core/Enums/Enums.cs`; quyền API enforce qua JWT claims `[Authorize(Roles=...)]`. Frontend guard `isStaff`/`isAdmin` ở `src/store/auth.ts`.

## 3. Quy tắc sân và khung giờ chuẩn

- Mỗi cơ sở có dải giờ mở cửa do admin cấu hình: **mặc định khởi tạo 06:00–23:00, bước 1 giờ → 17 slot/ngày** (slot bắt đầu 06:00…22:00, mỗi slot 60 phút). Đây là giá trị seed (`SeedData.cs` sinh 17 slot, peak = bắt đầu ≥ 18:00). Slot là dữ liệu trong `TimeSlots`, có API quản trị. V1 giữ slot 60 phút; slot khác độ dài là mở rộng cần thiết kế giá và chống khoảng giờ chồng lấn, không chỉ thêm bản ghi.
- **Khung peak** = slot bắt đầu từ 18:00–22:00 (5 slot); các slot còn lại là **low-peak**. Mỗi sân có `price_low_peak` và `price_peak` riêng (nguyên, VND/giờ). Giá thực = giá peak nếu slot thuộc peak, ngược lại là low-peak.
- **Cuối tuần** (thứ 7, chủ nhật) áp phụ thu theo tỷ lệ sân (`weekend_surcharge`, mặc định 0.20 = +20% lên giá cơ sở của slot (peak hoặc low-peak)). Admin cấu hình; yêu cầu có audit.
- Một đặt chỗ chiếm **một slot duy nhất** của một sân trong một ngày. Không đặt slot trùng của cùng khách/nhóm vượt hạn mức.
- Slot có `court_date_status` khác `Open` (bảo trì/đóng) → **không cho đặt**; hiển thị là slot khóa.
- Không tạo đặt chỗ vào slot đã bắt đầu hoặc bắt đầu trong vòng 30 phút tới (theo Hanoi clock).

**Hàm giá duy nhất (single source):** mọi thành phần (UI, payment, dashboard, hoàn tiền) đọc từ `PricingService` một chỗ:
```
price(court, slot, date) =
  (slot.IsPeak ? court.PricePeak : court.PriceLowPeak)
  * (isWeekend ? 1 + court.WeekendSurcharge : 1)
  − voucher_discount − points_discount
```
TARGET không tính lại ở UI; refund đọc PayableAmount snapshot. CURRENT dashboard và breakdown lịch sử còn sai khác tại §9; không khẳng định mọi module đã dùng chung hàm. Xem [04](04-system-architecture.md).

## 4. Đặt chỗ, hủy và hoàn tiền

- **Cửa sổ đặt:** sớm nhất = thời điểm hiện tại + 30 phút, xa nhất = +30 ngày.
- **Hạn mức:** 1 khách tối đa **4 slot/ngày**; 1 nhóm tối đa **8 slot/ngày** và tối đa **10 thành viên**; người tạo nhóm là người duy nhất được đặt cho nhóm.
- Chuỗi trạng thái mục tiêu: `Pending → Confirmed → Completed / NoShow / Cancelled / Refunded`; hết TTL Pending → Cancelled. CURRENT ở [03](03-workflow.md). Slot `Pending` chưa thanh toán hiệu lực **10 phút** rồi tự hủy (`PendingBookingExpiryJob`); không cho khách khác chiếm slot đang `Pending/Confirmed` hợp lệ (chống trùng: unique filtered index `ux_court_slot_active`).
- **Xác nhận** qua gateway webhook (trực tuyến) hoặc manager xác nhận tại quầy; **không phụ thuộc việc khách giữ nguyên trình duyệt** (webhook là source of truth).
- **Hoàn tiền khi hủy (theo thời điểm hủy so với giờ bắt đầu slot):**
  | Khoảng cách đến giờ bắt đầu | Tỷ lệ hoàn |
  | --- | --- |
  | ≥ 2 giờ | 100% |
  | 1–2 giờ | 50% |
  | < 1 giờ | 0% (no-show nếu sau giờ bắt đầu) |
- Đơn **chưa thanh toán** hủy chỉ giải phóng slot, không phát sinh hoàn tiền.
- **No-show:** sau giờ bắt đầu mà khách vắng → yêu cầu xử lý no-show theo mốc được chốt, hoàn 0%; mỗi khách **3 lần no-show trong tháng** → gắn cờ `NoShowLimited` chặn đặt **tháng kế tiếp** (`NoShowResetJob` tự mở lại khi qua tháng). Kiểm tra trong `BookingService.CreateAsync`.
- Hủy hợp lệ cập nhật trạng thái Booking, giữ bản ghi và giá trị lịch sử; hoàn tiền tạo Payment riêng, ghi ledger/audit. CURRENT hủy đã trả chưa liên kết hoàn tiền, xem §9.

> **Code (dev baseline):** refund tiers = hàm thuần `RefundPolicy.Tier`; pending TTL 10 phút = `PendingBookingExpiryJob` (poll 1 phút). Hoàn tiền tạo **payment record mới** (`status=Refunded/PartialRefund`), không sửa payment gốc.

## 5. Voucher, điểm tích lũy và đánh giá

**Voucher**
- Loại `percent` (giảm %, có trần `max_value`) hoặc `fixed` (giảm tiền cố định, VND); kèm `min_spend`, `max_per_user`, tổng số `total_qty` và thời hạn `valid_from..valid_until`.
- Một voucher **một lần mỗi khách** (unique `voucher_redemptions(voucher_id, user_id)`); phải thỏa `min_spend` tính trên giá gốc trước giảm. Không phát hành voucher âm; voucher + điểm không cộng dồn vượt giá trị đơn.
- `VoucherExpiryJob` (mỗi giờ) vô hiệu voucher qua hạn.

**Điểm tích lũy**
- **1 đồng thanh toán thành công = 1 điểm**, tính trên đơn `Completed` hợp lệ.
- **1.000 điểm quy thành 20.000đ** giảm một đơn (redeem qua `PointService.RedeemAsync`, idempotent theo `LedgerKey`). Hoàn tiền full → `RestoreAsync` trả điểm.
- Sổ điểm append-only trong `PointLedger`: earn/restore = delta dương, redeem/clawback = delta âm. Không xóa giao dịch đã ghi. Số dư = tổng delta theo user.

**Đánh giá (review)**
- Chỉ tạo cho đơn đã **`Completed`**, tối đa một đánh giá mỗi đặt chỗ (unique `reviews.booking_id`), kèm sao 1–5 + bình luận + ảnh.
- Trung bình sao sân (`rating_avg`) cập nhật chạy (rolling) khi có đánh giá mới. Hủy đơn đã đánh giá giữ đánh giá, gắn cờ nếu đơn bị hoàn.

**Dashboard ưu tiên:** doanh thu kỳ, tỷ lệ lấp đầy slot peak, số đặt mới, số no-show (30 ngày), đơn chờ thanh toán, voucher đang chạy, tình trạng bảo trì, storage/DB. (Xem `ReportService` + endpoint `GET /api/v1/admin/report/*`.)

## 6. Điều kiện nghiệm thu nghiệp vụ (acceptance)

1. Khách đặt một slot trống, thanh toán (mock gateway) thành công → đặt chỗ `Confirmed`, slot khóa, mục tiêu QR quét/xác thực hợp lệ (CURRENT mới có tile minh họa).
2. Hai khách đặt cùng slot cùng lúc → chỉ một được giữ, khách kia nhận `SLOT_CONFLICT` (409); slot không trùng (unique index).
3. `Pending` quá 10 phút không trả → tự hủy, slot mở lại; voucher slot + điểm redeem (nếu có) được trả về.
4. Hủy theo 3 mốc (≥2h/1–2h/<1h) hoàn đúng tỷ lệ; đơn chưa trả hủy chỉ giải phóng slot; no-show hoàn 0% và tích lũy cờ.
5. Voucher đúng điều kiện giảm đúng mức, 1 khách 1 lần, không âm; điểm cộng khi hoàn thành, trừ khi hoàn tiền.
6. Slot bảo trì/đóng không cho đặt; cuối tuần đúng phụ thu; peak dùng giá peak, low dùng low.
7. Manager chỉ thấy/xử lý cơ sở mình; admin thấy mọi cơ sở; mọi thay đổi giá, voucher, bảo trì, hủy, role có audit (append-only, before/after).

## 7. Thuật ngữ (tham chiếu nhanh)

Xem định nghĩa chuẩn tại `glossary.md` (peak/low-peak, pending TTL, no-show limit, overbooking, idempotency_key, gateway_ref, ledger, branch-scope, Hanoi clock…).

## 8. Kế hoạch và truy vết

Backlog/ưu tiên/phụ thuộc/chính sách cần chốt quản lý tại [08-plan.md](08-plan.md); ma trận có code và lịch sử tại [worklog.md](worklog.md). Không dùng checkbox kế hoạch như quyền triển khai toàn bộ tính năng.

## 9. Sai khác CURRENT đã xác minh trong source

| Chủ đề | Yêu cầu cần đạt | CURRENT / ảnh hưởng |
| --- | --- | --- |
| Hủy | Chỉ chủ đơn/staff đúng cơ sở; đã trả phải xử lý refund | AuthorizeCancel không throw actor khác; CancelAsync không gọi refund (SEC-01/PAY-01) |
| Manager | Mọi dữ liệu/thao tác giới hạn cơ sở | List/summary/payment read/lifecycle còn thiếu scope; manager null branch chưa fail closed (SEC-02) |
| Điểm | Mục §5 quy định earn khi Completed, restore khi full | PaymentService earn khi Confirmed và restore mọi tỷ lệ; DEC-01/02 chưa chốt |
| No-show | Mốc rõ, không đánh dấu trước giờ; đủ ba lần chặn tháng kế | Lifecycle chỉ guard Confirmed, chưa thời gian/scope; cạnh tranh auto-complete (DEC-03) |
| Giá lịch sử | Tiền đã xác nhận không đổi theo giá sân mới | Tổng có snapshot, breakdown dựng lại từ sân hiện hành; DATA-01 |
| QR | Mã quét và server xác thực | QrTile là hình minh họa display-only; UI-01 |
| Báo cáo | Tiền thu thành công trừ hoàn, peak đúng slot | ReportService tổng Booking, refunds=0, peak tử số chưa lọc IsPeak; REP-01 |

Sửa docs không sửa các lỗi ứng dụng này. Xem source [BookingService](../Backend/Services/Booking/BookingService.cs), [PaymentService](../Backend/Services/Payment/PaymentService.cs), [ReportService](../Backend/Services/Report/ReportService.cs).

## 10. Danh mục yêu cầu và nghiệm thu V1

| ID | Yêu cầu | Tiêu chí nghiệm thu / liên quan |
| --- | --- | --- |
| BR-01 | Danh tính và quyền | Tự đăng ký chỉ Customer; A không đọc/hủy/hoàn đơn B; staff đúng branch |
| BR-02 | Cửa sổ và lịch | +30 phút/+30 ngày đúng boundary UTC+7; bảo trì không nhận đơn mới |
| BR-03 | Giá và giữ chỗ | Đúng peak/low/cuối tuần; Pending TTL; snapshot không đổi theo cấu hình mới |
| BR-04 | Chống trùng/idempotency | Một active slot; retry không tạo thêm; actor/payload khác không dùng kết quả cũ |
| BR-05 | Thanh toán | Không tin client; callback/confirm chỉ áp một lần; muộn có đối soát |
| BR-06 | Hủy/hoàn | Đúng ba tier, không refund Pending, không mất tiền khi cancel đã trả |
| BR-07 | Nhóm | Owner đặt, đúng member/capacity/hạn mức, không vượt quyền qua groupId |
| BR-08 | Voucher | Đúng điều kiện/số lượng; concurrent không vượt; cancel Pending trả lượt |
| BR-09 | Điểm | Quy đổi đúng đơn vị, ledger không trùng; thời điểm và restore theo DEC-01/02 |
| BR-10 | Lifecycle/review | Chuyển trạng thái hợp lệ, no-show theo kỳ; owner Completed review một lần |
| BR-11 | Quản trị/audit | Tối đa một active admin, không mất admin cuối; trước/sau truy vết đúng |
| BR-12 | Báo cáo | Doanh thu/refund/occupancy đối soát dataset, scope và múi giờ đúng |

### Báo cáo và yêu cầu phi chức năng — TARGET

- Phân biệt **giá trị đặt sân theo ngày chơi** với **tiền thu theo PaidAt**. Doanh thu thuần = khoản thu Success − RefundAmount đã hoàn thành trong kỳ; không tính Pending như tiền thu.
- Peak occupancy = số slot peak được sử dụng theo tập trạng thái đã chốt / số slot peak có thể bán. Slot bảo trì và cơ sở đóng cần được loại nhất quán; định nghĩa trạng thái sử dụng cần chốt trước nghiệm thu REP-01.
- Ngưỡng p95 booking <500ms/read <1s là mục tiêu test theo tải xác định, chưa là số đo thực tế; 0 double-booking là bất biến dữ liệu.
- SĐT/email, thanh toán, audit chỉ hiển thị theo quyền; log không chứa token/password/raw payload nhạy cảm. Retention/RPO/RTO cần owner chốt, chưa là cam kết pháp lý/SLA.
- Nghiệm thu cả lỗi mạng, xung đột, hết TTL và truy cập chéo; chi tiết [05](05-testing-strategy.md). Giao diện tiếng Việt, bàn phím và responsive theo [07](07-ui-design-system.md).
