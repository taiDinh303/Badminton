# 01 — Phân tích nghiệp vụ hệ thống đặt sân cầu lông

## 1. Mục tiêu và phạm vi

Hệ thống cho phép khách xem lịch trống và đặt sân cầu lông theo khung giờ, thanh toán trực tuyến, quản lý đặt chỗ và nhóm chơi; quản lý vận hành từng cơ sở; một admin quản lý cấu hình và nhiều cơ sở. Giao diện và thuật ngữ dùng tiếng Việt; múi giờ nghiệp vụ là `Asia/Ho_Chi_Minh`.

V1 gồm: hồ sơ khách (đăng ký bằng số điện thoại + OTP), tài khoản và phân quyền, đặt sân cá nhân và nhóm, khung giờ cố định 1h, thanh toán VNPay/Momo/Zalopay và tại sân, voucher, điểm tích lũy, đánh giá sau khi hoàn thành, lịch sử đơn, bảo trì sân, dashboard và báo cáo theo quyền. Không gồm: gói membership (vàng/bạc), huấn luyện viên, lớp học, bán hàng/sỉn, giải đấu, đa ngôn ngữ, tính năng no-show tự động trừ phí (V1 chỉ gắn cờ no-show và giới hạn đặt sau 3 lần), và không tự động tính giá theo từng ngày lễ.

Quy tắc nghiệp vụ trong tài liệu này là tiêu chuẩn của hệ thống; các file khác chỉ tham chiếu, không phát sinh quy tắc mới.

## 2. Người dùng và quyền

Ba role: `customer`, `manager`, `admin`. Khách tự đăng ký bằng số điện thoại + OTP; manager và admin do admin tạo. Có thể có nhiều khách, nhiều manager và **đúng một tài khoản admin đang hoạt động** trên toàn hệ thống.

| Khả năng | Customer | Manager | Admin |
| --- | --- | --- | --- |
| Xem lịch trống, đặt sân, thanh toán, nhận QR | Có | Có nếu gắn tài khoản customer | Có nếu gắn tài khoản customer |
| Hủy đặt chỗ của mình theo quy tắc hoàn tiền | Có | Có | Có |
| Tạo/quản lý nhóm chơi, mời thành viên, đặt cho nhóm | Có | Có | Có |
| Dùng voucher, xem/đổi điểm tích lũy, đánh giá | Có | Có | Có |
| Xem đặt chỗ/báo cáo của **cơ sở mình** | Không | Có | Có |
| Đánh dấu bảo trì, no-show, hủy đặt chỗ của khách | Không | Có | Có |
| Chỉnh giá sân, khung giờ, tạo voucher, quản lý nhiều cơ sở | Không | Không | Có |
| Cấp/khóa tài khoản, đổi role, khóa/mở khóa cấu hình, xem audit toàn hệ thống | Không | Không | Có |
| Duyệt/hủy đặt chỗ của manager khác | Không | Không | Có |

Hồ sơ khách và tài khoản đăng nhập là hai đối tượng tách biệt để giữ lịch sử khi tài khoản bị khóa. Manager gắn với **một hoặc nhiều cơ sở**; admin quản lý mọi cơ sở. Không ai hủy/duyệt yêu cầu của chính mình ở cấp cao hơn: nếu admin cũng gắn tài khoản customer và tạo đơn cá nhân, manager của cơ sở đó xử lý; admin chỉ tự xử lý yêu cầu ở phạm vi cấu hình.

## 3. Quy tắc sân và khung giờ chuẩn

- Mỗi cơ sở có một dải giờ mở cửa chung do admin cấu hình: **mặc định khởi tạo 06:00–23:00, bước 1 giờ, sinh 17 slot/ngày** (slot bắt đầu 06:00 đến 22:00, mỗi slot 60 phút). Giá trị này là mặc định có thể chỉnh, không phải giờ cố định; đổi bằng phiên bản có ngày hiệu lực và audit.
- **Khung peak** là các slot bắt đầu từ 18:00 đến 22:00 (5 slot); các slot còn lại là **low-peak**. Mỗi sân có `giá low-peak` và `giá peak` riêng; giá thực = giá peak nếu slot thuộc peak, ngược lại là low-peak.
- **Cuối tuần** (thứ 7, chủ nhật) áp phụ thu cuối tuần theo tỷ lệ sân (mặc định +20% lên giá peak). Admin cấu hình tỷ lệ và ngày áp dụng; có audit.
- Một đặt chỗ chiếm **một slot duy nhất** của một sân trong một ngày. Không đặt slot trùng nhau của cùng khách hay cùng nhóm vượt hạn mức.
- Slot có trạng thái `court_date_status` khác `open` (bao trì, đóng) thì **không cho đặt**; hiển thị là slot khóa.
- Thời điểm đặt được xác định theo `Asia/Ho_Chi_Minh`; hệ thống không đặt slot nằm trong quá khứ tại thời điểm tạo.

## 4. Đặt chỗ, hủy và hoàn tiền

- Cửa sổ đặt: **bắt đầu sớm nhất là thời điểm hiện tại + 30 phút, xa nhất là +30 ngày**. Không đặt slot đã bắt đầu hoặc bắt đầu trong vòng 30 phút tới.
- **Hạn mức:** một khách tối đa 4 slot/ngày; một nhóm tối đa 8 slot/ngày và tối đa 10 thành viên; người tạo nhóm là người duy nhất được đặt cho nhóm.
- Đặt chỗ đi theo trạng thái `pending → confirmed` (chưa trả → đã trả/xác nhận). Slot `pending` chưa thanh toán có hiệu lực **10 phút** rồi tự hủy để mở slot; không cho khách khác chiếm slot đang có `pending/confirmed` hợp lệ (chống trùng chi tiết ở file 02).
- Thanh toán trực tuyến (VNPay/Momo/Zalopay) hoặc tại sân (QR/tiền mặt, do manager xác nhận). Trạng thái xác nhận đặt chỗ là **webhook/gateway callback hoặc manager**, không phụ thuộc việc khách giữ nguyên trình duyệt.
- Hoàn tiền khi hủy: **ít nhất 2 giờ trước giờ bắt đầu = 100%; từ 1 đến 2 giờ = 50%; dưới 1 giờ = 0%**. Nếu đặt chỗ chưa thanh toán, hủy chỉ giải phóng slot, không phát sinh hoàn tiền. Sau giờ bắt đầu mà khách vắng là **no-show**, hoàn 0%; mỗi khách tích 3 lần no-show trong tháng thì bị gắn cờ giới hạn đặt trong tháng kế tiếp.
- Hủy hợp lệ phải giải phóng slot cho người khác đặt lại; không sửa trạng thái gốc, ghi điều chỉnh và audit.

## 5. Voucher, điểm tích lũy và đánh giá

Voucher có loại `percent` (giảm % có trần) hoặc `fixed` (giảm tiền cố định), kèm điều kiện `min_spend`, số lần dùng tối đa mỗi khách, tổng số và khoảng thời hạn. Một voucher chỉ dùng được **một lần mỗi khách** và phải thỏa `min_spend` tính trên giá gốc trước giảm. Voucher và điểm không cộng dồn vượt giá trị đơn; không phát hành voucher âm trị giá.

Điểm tích lũy: **1 đồng thanh toán thành công = 1 điểm**, tính trên đơn `confirmed` đã hoàn thành; **1.000 điểm quy thành 20.000 đồng** giảm giá một đơn. Điểm chỉ cộng khi đặt chỗ hoàn thành hợp lệ; hoàn tiền đơn có cộng điểm thì hệ thống trừ lại điểm bằng giao dịch, không sửa số dư trực tiếp.

Đánh giá chỉ tạo được cho đơn đã **hoàn thành**, tối đa một đánh giá một đặt chỗ, kèm sao 1–5, bình luận và ảnh (nếu có). Trung bình sao sân là trung bình trượt các đánh giá hợp lệ; hủy đơn đã đánh giá giữ đánh giá nhưng gắn cờ nếu đơn bị hoàn. Báo cáo dashboard ưu tiên: doanh thu kỳ, tỷ lệ lấp đầy slot peak, số đặt mới, số no-show, đơn chờ thanh toán, voucher đang chạy và tình trạng bảo trì.

## 6. Điều kiện nghiệm thu nghiệp vụ

1. Khách đặt một slot trống, thanh toán VNPay thành công; đặt chỗ chuyển `confirmed`, slot bị khóa, QR hợp lệ được tạo và khách nhận mã.
2. Hai khách đặt cùng một slot trong cùng thời điểm: chỉ một khách được đặt, khách còn lại nhận thông báo xung đột có hướng dẫn chọn slot khác; slot không bị đặt trùng.
3. Đặt chỗ `pending` quá 10 phút không thanh toán tự hủy; slot mở lại cho khách khác; khách cũ nhận thông báo đã hết hạn.
4. Hủy theo ba mức 2 giờ/1–2 giờ/dưới 1 giờ cho đúng tỷ lệ hoàn; hủy đơn chưa thanh toán chỉ giải phóng slot; no-show sau giờ bắt đầu hoàn 0% và tích lũy cờ.
5. Voucher đúng điều kiện giảm đúng mức, một khách một lần, không âm; điểm cộng khi hoàn thành và trừ khi hoàn tiền hợp lệ.
6. Slot bảo trì hoặc đóng không cho đặt; cuối tuần áp đúng phụ thu; khung peak dùng giá peak, low-peak dùng giá low.
7. Manager chỉ thấy/xử lý phạm vi cơ sở của mình; admin thấy mọi cơ sở; mọi thay đổi giá, voucher, bảo trì, hủy và role có audit và phi-ên bản.

Xem mô hình dữ liệu tại [02-database-design.md](02-database-design.md), luồng thao tác tại [03-workflow.md](03-workflow.md), hợp đồng API tại [04-system-architecture.md](04-system-architecture.md) và thiết kế giao diện tại [07-ui-design-system.md](07-ui-design-system.md).
