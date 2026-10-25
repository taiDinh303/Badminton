# 16 — Hoàn thiện nghiệp vụ ngoài thanh toán

Ngày: 25/10/2026. Phạm vi người dùng: triển khai các đề xuất trong review 24/10, giữ phần gateway/thanh toán ngoài phạm vi. CURRENT bên dưới là source; kết quả kiểm thử thực tế ở [worklog](worklog.md). Không đồng nghĩa đã triển khai live.

## 1. Quyết định nghiệp vụ được chủ dự án xác nhận

- Cơ bản **100.000 VND = 100 điểm**. Membership tiếp tục nhân hệ số hiện có; số điểm nguyên làm tròn theo `AwayFromZero`. Giữ quy đổi **1.000 điểm = 20.000 VND**, tương đương quyền lợi cơ bản khoảng 2%.
- **Số dư cũ quy về 1/1.000**, bỏ phần lẻ điểm. Ledger cũ giữ nguyên; migration thêm một bút toán điều chỉnh `unit-v2:{userId}` cho từng tài khoản hiện hữu, kể cả số dư 0. Không chia trực tiếp từng dòng lịch sử.
- Check-in từ **30 phút trước giờ bắt đầu đến hết slot**; Pending chỉ được nhận khi TTL còn hiệu lực. Đã check-in không được no-show.
- Chỉ Completed khi **đã check-in và hết giờ**. Job giữ Confirmed chưa check-in để nhân viên xác minh/no-show; không tự kết luận khách đã đến. Dashboard và bộ lọc đơn có danh sách cần xác minh hiện diện.

## 2. CURRENT — Bất biến và xử lý đồng thời

| Khu vực | Thay đổi source |
| --- | --- |
| Profile | Chỉ ghi FullName/Email/AvatarUrl/UpdatedAt, không ghi lại hash mật khẩu, role, status, stamp, tier hay counters cũ |
| Các writer User | Auth/staff/membership/no-show/reset job cập nhật các cột thuộc thao tác; thay mật khẩu/khóa/đổi quyền vô hiệu mã khôi phục khi phù hợp. Reset hạn chế no-show khóa cùng writer bộ đếm và đọc lại hạn chế trước khi gỡ |
| Completion | Service và conditional DB command cùng kiểm hiện diện/giờ kết thúc; không mở lại sân hay cộng điểm trước khi chơi xong |
| Check-in/no-show | Conditional DB commands loại trừ nhau; kiểm TTL, deleted row và cửa sổ thời gian tại server |
| Review | Tạo review, tổng hợp sao và audit cùng transaction; khóa theo sân, tính lại aggregate từ review gốc, chỉ ghi các cột rating |
| Waitlist | Booking và promotion cùng transaction; promotion/leave/expiry dùng cùng khóa entry. Rollback toàn bộ attempt trước khi xử lý lỗi hoặc ứng viên kế tiếp |
| Người chờ mất điều kiện | Giữ vị trí để có thể phục hồi nhưng xét tiếp người đủ điều kiện, không chặn cả hàng. Không xóa entry vì timeout/xung đột tạm thời |
| Hết cơ hội nhận chỗ | Worker chuyển Waiting quá thời hạn nhận slot sang Expired, lưu lý do/ResolvedAt |
| Số học điểm | Giá trị quy đổi account dùng long; hoàn/trừ lại điểm của đơn trước cutover quy sang đơn vị mới, không chia lượt earn mới thêm lần nữa |

## 3. CURRENT — Chức năng và HTTP bổ sung

Các route cũ vẫn tồn tại. Các trường DTO mới là bổ sung; số JSON vẫn là số.

- `GET /bookings/all`: thêm `search`, `date`, `needsAttendance`; lọc server trước phân trang, vẫn áp branch scope. Tìm mã đơn, tên/SĐT khách, tên sân.
- `BookingView`: thêm `pendingExpiresAt`, `serverTimeUtc`, `slotStartAtUtc`, `slotEndAtUtc`. UI đếm thời gian theo deadline server và hiệu chỉnh lệch đồng hồ thiết bị.
- `GET /courts/{id}/impact`, `GET /branches/{id}/impact`: staff hợp lệ xem số đơn đang hoạt động và tối đa 10 mã đơn. Vô hiệu/xóa sân/cơ sở hoặc thu hẹp giờ ảnh hưởng đơn sẽ bị chặn. Dùng quy trình bảo trì/xử lý đơn trước; không tự hủy hàng loạt.
- `GET /reviews/paged`: lọc sân tùy chọn, phân trang và aggregate toàn bộ kết quả. `GET /reviews/booking/{bookingId}` kiểm owner/staff scope cho lookup một đơn. Trang review và preload không còn tải lịch sử mọi sân.
- `GET /waitlist/mine/paged`: lịch sử chờ phân trang. Route `mine` cũ giữ contract.
- Modal dùng bàn phím: đưa focus vào dialog, giữ Tab trong dialog, Escape đóng và trả focus về trigger. Ngày check-in và timestamp hiển thị theo UTC+7.

## 4. CURRENT — Nhóm chơi

- `GET /groups/mine` vẫn owner-only; thêm `GET /groups/joined` cho các nhóm đang tham gia.
- Thêm đổi tên, rời nhóm, loại thành viên, chuyển chủ và giải tán. Chỉ owner được quản trị; owner cần chuyển chủ hoặc giải tán trước khi rời; nhóm còn booking hoạt động không được giải tán.
- Chuyển chủ chỉ cho thành viên đang hoạt động và thu hồi mã mời cũ. Quota nhóm đếm theo GroupId qua các owner, không được reset bằng chuyển chủ.
- Nhóm **tạo mới** cần mã mời bí mật khi join. Owner được xoay mã; server chỉ lưu hash, không trả lại mã cũ. UI hiển thị mã khi tạo/xoay và chỉ giữ trong bộ nhớ màn hình.
- Nhóm cũ chưa có hash vẫn nhận join theo ID để giữ tương thích; owner có thể xoay mã để chuyển sang lời mời có kiểm soát. Đây là giới hạn tương thích có chủ đích, không tuyên bố mọi nhóm cũ đã có invite protection.

## 5. CURRENT — Phục hồi tài khoản có người xác minh

- Admin xác minh chủ tài khoản tại sân, nhập lại mật khẩu Admin và ghi lý do để cấp mã tại `POST /auth/recovery-code/{userId}`. UI nằm trong quản lý khách/membership.
- Mã ngẫu nhiên 256 bit, chỉ lưu SHA-256, hết hạn 15 phút; cấp lại thu hồi mã trước. Chỉ trả mã cho Admin trong response cấp mã, không đưa token/hash vào audit.
- Khách dùng màn Quên mật khẩu, gọi `POST /auth/reset-password` với SĐT, mã, mật khẩu mới. Mã dùng một lần, tối đa 5 lần nhập sai, kiểm và consume trong account lock/transaction. Đặt lại mật khẩu đổi stamp để thu hồi JWT cũ.
- Không hỗ trợ tự reset Admin duy nhất bằng mã này. Không dùng email chưa xác minh để chiếm quyền tài khoản; không giả lập đã gửi SMS/OTP. TARGET riêng: kênh xác minh tự động đã được owner/provider chấp thuận, refresh sessions và quy trình phục hồi Admin có kiểm soát.

## 6. CURRENT — Lịch sử tỷ lệ lấp đầy

- Bảng mới `InventoryDaySnapshots` lưu tập court/slot/peak theo branch/ngày, không chứa PII. Worker chụp ngày hiện tại và các thao tác cấu hình/bảo trì cập nhật bản ghi ngày hiện tại.
- Báo cáo ngày đã qua dùng snapshot đã ghi, giữ ổn định trước các lần đổi cấu hình sau đó. Ngày hiện tại/tương lai dùng cấu hình hiện tại.
- Dữ liệu trước khi cơ chế snapshot hoạt động không thể tự tái dựng chính xác: API trả `historicalEstimate=true`, UI nêu rõ ước tính theo cấu hình hiện tại.
- Giới hạn: snapshot là cấu hình được ghi nhận gần nhất trong ngày, không phải event sourcing từng phút. Downtime qua mốc đổi ngày và sửa hồi tố lịch cũ cần đối soát; không gọi dữ liệu thiếu là lịch sử đầy đủ.

## 7. Migration và phát hành có kiểm soát

Migrations mới riêng hai provider:

- SQL Server: `20261025133237_HardenNonPaymentWorkflows`.
- PostgreSQL: `20261025133322_HardenNonPaymentWorkflows`.

Thêm recovery metadata trên Users, invite hash trên PlayGroups, bảng thứ 19 InventoryDaySnapshots, và bút toán quy đổi số dư. PostgreSQL bật RLS/revoke PUBLIC/anon/authenticated cho bảng/sequence mới. Không sửa migration lịch sử.

**Không chạy migration trên live từ nhiệm vụ này.** Quy trình phát hành phải:

1. Review script đúng provider; backup và kiểm restore trên fixture/bản sao được phép.
2. Dừng/drain các writer và worker phiên bản cũ; không chạy lẫn hai phiên bản với hai đơn vị earn điểm.
3. Áp migration bằng thao tác phát hành được owner cho phép; đối chiếu marker mỗi user, tổng số dư mới và các đơn Pending cũ.
4. Khởi động API mới với startup init/seed vẫn tắt; xác nhận `/ready`, đăng nhập, lịch sân, điểm, review, nhóm và check-in.

Down tự động bị chặn nếu đã tồn tại marker quy đổi. Khôi phục phải dùng backup phù hợp hoặc migration bù được review, không xóa marker/ledger để ép rollback. Đây là chuyển đổi quyền lợi khách theo quyết định owner, không phải thay đổi schema có thể đảo mù quáng.

## 8. NOT VERIFIED / điều kiện bên ngoài

Xem [worklog](worklog.md) để biết chính xác các lệnh và số test đã chạy trong lượt này. Local tests, rehearsal nhiều process và backup/restore fixture không chứng minh production endurance, camera/USB thật, ingress/metrics live, RPO/RTO/retention hay cloud release. Không gọi sandbox/tiền thật; phần thanh toán giữ gate riêng trong [08](08-plan.md).
