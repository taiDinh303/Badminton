# Tham quan giao diện BadmintonBooking

[Mở bản demo](https://badminton-bvqs.onrender.com/) · [Quay lại README](../README.md)

## Nguồn ảnh

- **LIVE — 04/11/2026:** trang giới thiệu, đăng nhập và mobile được chụp trực tiếp từ link Render do chủ dự án cung cấp. Đã đăng nhập và chụp Trang chính khách, lịch sân, Tổng quan Manager/Admin. Xem [tài khoản và phạm vi demo](DEMO.md).
- **LOCAL / API MOCK — 31/10/2026:** tổng quan CLB và sáu tiện ích là ảnh giao diện React chạy bằng browser với dữ liệu test giả lập. Đây là ảnh ứng dụng, không phải mockup thiết kế; không dùng để khẳng định sáu tiện ích đã được deploy lên Render hoặc nghiệm thu API/DB live.
- Ảnh tài liệu được chọn để trình bày UI; log, token, cấu hình và kết quả kiểm thử thô tiếp tục nằm trong artifacts ignored.

## Trang giới thiệu và đăng nhập — LIVE

| Giới thiệu | Đăng nhập |
| --- | --- |
| ![Trang giới thiệu trên Render](media/home-desktop.png) | ![Đăng nhập trên Render](media/auth-desktop.png) |

## Người chơi và lịch sân — LIVE

Dùng Customer để xem các cơ sở, sân và khung giờ. Chỉ mở trang xem dữ liệu trong lượt chụp, chưa tạo đơn hoặc thanh toán.

| Trang chính | Lịch sân |
| --- | --- |
| ![Trang chính người chơi](media/customer-dashboard.png) | ![Lịch đặt sân](media/booking-calendar.png) |

## Quản lý và quản trị — LIVE

Đã kiểm tra đúng vai trò khi đăng nhập; ảnh minh họa trang tổng quan. Các chỉ số là snapshot lúc chụp, có thể đổi khi người khác dùng demo.

| Manager | Admin |
| --- | --- |
| ![Tổng quan quản lý](media/manager-dashboard.png) | ![Tổng quan quản trị](media/admin-dashboard.png) |

## Tổng quan câu lạc bộ — LOCAL / API MOCK

Sáu thẻ chức năng, mô tả và liên kết vào từng tiện ích. Nhân viên có lối vào các trang quản lý theo quyền.

![Tổng quan câu lạc bộ](media/club-overview-desktop.png)

## Tìm bạn chơi — LOCAL / API MOCK

Lọc cơ sở, khu vực, trình độ và giờ rảnh; mở buổi, gửi/rút yêu cầu. Chủ buổi duyệt người tham gia. Buổi tìm bạn không tự giữ sân.

![Tìm bạn chơi](media/players-desktop.png)

## Chia chi phí — LOCAL / API MOCK

Chọn nhóm và đơn đủ điều kiện, chia đều hoặc theo phút tham dự, theo dõi phần tiền và trạng thái đóng. Đây là ghi nhận đóng góp nội bộ.

![Chia chi phí nhóm](media/group-expenses-desktop.png)

## Dụng cụ và cầu — LOCAL / API MOCK

Xem hàng có sẵn, đặt thuê vợt hoặc mua cầu, theo dõi yêu cầu nhận/trả. Nhân viên quản lý tồn, giao trả và biên bản tại cơ sở.

![Dụng cụ](media/equipment-desktop.png)

## Huấn luyện viên — LOCAL / API MOCK

Xem hồ sơ, lịch học còn chỗ và đặt buổi cá nhân/nhóm; nhân viên quản lý lịch và sân học.

![Huấn luyện viên](media/coaches-desktop.png)

## Giải đấu nhỏ — LOCAL / API MOCK

Đăng ký đội, xem bảng đấu, lịch trận, kết quả và xếp hạng; staff có luồng chia bảng, xếp lịch và ghi/sửa điểm có lịch sử.

![Giải đấu nhỏ](media/tournaments-desktop.png)

## Lịch tự đồng bộ — LOCAL / API MOCK

Hướng dẫn đăng ký lịch điện thoại bằng URL riêng tư. Ảnh ở trạng thái chưa bật, không chứa token. Tốc độ cập nhật do ứng dụng lịch quyết định.

![Lịch tự đồng bộ](media/calendar-sync-desktop.png)

## Mobile

Trang giới thiệu là ảnh LIVE; các trang CLB là LOCAL / API MOCK. Cuộn dọc để dùng các thẻ và biểu mẫu, menu mobile giữ điều hướng chính.

| Giới thiệu — LIVE | Tổng quan CLB — MOCK |
| --- | --- |
| ![Trang chủ mobile](media/home-mobile.png) | ![CLB mobile](media/club-overview-mobile.png) |

| Tìm bạn | Chia chi phí |
| --- | --- |
| ![Tìm bạn mobile](media/players-mobile.png) | ![Chia tiền mobile](media/group-expenses-mobile.png) |

| Dụng cụ | Huấn luyện viên |
| --- | --- |
| ![Dụng cụ mobile](media/equipment-mobile.png) | ![HLV mobile](media/coaches-mobile.png) |

| Giải đấu | Lịch tự đồng bộ |
| --- | --- |
| ![Giải mobile](media/tournaments-mobile.png) | ![Lịch mobile](media/calendar-sync-mobile.png) |

## Thử các luồng khác

Đặt sân, báo giá, đơn của tôi, thanh toán tại quầy, check-in, nhóm, đánh giá và vận hành được hướng dẫn trong [kịch bản README](../README.md#kịch-bản-trải-nghiệm-5-phút). [Worklog](worklog.md) ghi bằng chứng từng boundary; các ảnh CLB không thay bằng chứng browser API/DB thật hoặc thiết bị điện thoại.
