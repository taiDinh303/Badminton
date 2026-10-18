# Thuật ngữ hệ thống đặt sân cầu lông

> Chủ tài liệu: **Đinh Văn Tài**. Cập nhật: **16/10/2026**.
> Định nghĩa dùng chung; giá trị/quy tắc nghiệp vụ ở [01](01-business-analysis.md). CURRENT/TARGET ở [00](00-README.md).

| Thuật ngữ | Định nghĩa | Liên quan / giới hạn |
| --- | --- | --- |
| Cơ sở / Branch | Địa điểm vận hành có nhiều sân | Branches; manager gán một branch theo yêu cầu |
| Sân / Court | Đơn vị khách đặt ở cơ sở | Courts, giá peak/low và capacity |
| Khung giờ / Time slot | Khoảng StartTime–EndTime của lịch | V1 60 phút; seed 06:00–23:00, 17 slot, slot cuối bắt đầu 22:00 |
| Peak / Low-peak | Slot cao điểm theo IsPeak; seed peak bắt đầu 18:00–22:00 | Giá theo loại slot, không theo giờ server |
| Phụ thu cuối tuần | Tỷ lệ cộng trên giá cơ sở của slot thứ 7/CN | Áp peak hoặc low; không mặc định giá peak cho mọi slot |
| Cửa sổ đặt | Thời gian cho phép tạo booking | Guard theo ngày/giờ UTC+7; chi tiết ở 01 |
| Pending TTL | Thời gian giữ sân chờ thanh toán | PendingExpiresAt UTC +10 phút; poll nên không bảo đảm giải phóng đúng giây 600 |
| Active slot | Slot có Pending/Confirmed | Unique court/date/slotId; không kiểm tra overlap giữa slot ID khác |
| Overbooking | Nhiều booking cùng chiếm một sân/giờ | ux_court_slot_active; không chỉ kiểm tra availability client |
| BookingCode | Mã nghiệp vụ của đơn | Unique; không phải token xác thực check-in |
| IdempotencyKey | Khóa để retry cùng ý định không tạo thêm đơn | CURRENT unique toàn hệ thống; actor/payload binding cần DATA-01 |
| GatewayRef | Tham chiếu giao dịch gateway | Unique khi khác null; đối soát callback/duplicate |
| Webhook | Callback server từ provider | Không tin returnUrl/client; mock chưa xác minh chữ ký thật |
| On-site | Thanh toán tại quầy Cash/QR | Staff xác nhận; QR chuyển khoản khác QR check-in |
| Refund | Hoàn tiền tạo Payment điều chỉnh riêng | RefundAmount là tiền hoàn; không xóa payment thu gốc |
| Cancel | Hủy booking/giải phóng slot | CURRENT cancel không tự gọi refund; PAY-01 |
| No-show | Khách không đến theo mốc nghiệp vụ | Cờ giới hạn tháng kế; mốc và cạnh tranh job còn DEC-03 |
| HanoiTime | Helper giờ Việt Nam fixed UTC+7 | Timestamp UTC; date/slot theo lịch địa phương; không phải mọi code đã dùng helper |
| Điểm / Points | Đơn vị loyalty, khác đơn vị VND | 1 VND thu = 1 điểm; 1.000 điểm đổi 20.000 VND; thời điểm earn còn DEC-01 |
| Ledger | Sổ giao dịch điểm | earn/restore dương, redeem/clawback âm; balance=sum Delta |
| LedgerKey | Khóa thao tác điểm một lần | earn/redeem/restore/clawback theo booking; không tự chống race balance giữa nhiều đơn |
| Voucher redemption | Lượt voucher giữ/dùng bởi user/booking | Unique voucher/user; release hiện có xóa record và giảm UsedQty |
| Branch scope | Quyền dữ liệu giới hạn cơ sở manager | TARGET toàn API; CURRENT chưa đầy đủ |
| Admin duy nhất | Yêu cầu một active admin | Index chỉ tối đa một; bootstrap/service bảo đảm không còn zero |
| Audit | Nhật ký actor/action/before/after/reason | Thiết kế append-only; chưa là DB chống mọi chỉnh sửa đặc quyền |
| Snapshot giá | Giá lưu theo thời điểm tạo đơn | Tổng tiền có lưu; breakdown lịch sử còn tính lại từ cấu hình hiện tại |
| Doanh thu thuần | Tiền thu thành công trừ tiền hoàn hợp lệ theo kỳ | Khác tổng giá trị booking theo ngày chơi; REP-01 |
| Occupancy | Tỷ lệ slot sử dụng / có thể bán | Peak phải lọc đúng IsPeak; thống nhất trạng thái và bảo trì |
| QR-like tile | Hình ô vuông mô phỏng mã từ BookingCode | Display-only CURRENT, chưa quét/xác thực |
| JWT | Access token do .NET ký | Lưu bb_token; role/user khóa cần test với token còn hạn |
| RLS | Chính sách bảo vệ hàng dữ liệu PostgreSQL | TARGET; JWT .NET không tự thành auth.uid() của DB connection |
| MockOnlineGateway | Adapter online giả lập | Không thu tiền thật; cần adapter signed sandbox trước production |
| BackgroundService | Worker trong tiến trình API | Bốn job hiện có; chưa scheduler phân tán/Hangfire |
| Service integration test | Test service + DB qua DI | Chưa kiểm HTTP middleware/authorize/JWT/controller |
| Migration | Thay đổi schema có phiên bản | EnsureCreated không kiểm migration nâng cấp |
| RPO / RTO | Mức mất dữ liệu chấp nhận / thời gian phục hồi mục tiêu | Chưa chốt; cần restore evidence |
| CURRENT / TARGET | Source hiện tại / yêu cầu cần đạt | Không đánh đồng có source và runtime pass |
| PENDING / NOT VERIFIED | Chưa đầy đủ / chưa có bằng chứng | Xem worklog và kế hoạch |

Liên quan: [02 dữ liệu](02-database-design.md), [03 workflow](03-workflow.md), [08 kế hoạch](08-plan.md), [09 cấu hình](09-deploy-detail.md).
