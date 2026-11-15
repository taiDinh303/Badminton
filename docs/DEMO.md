# Tài khoản và phạm vi trải nghiệm demo

[Mở bản demo](https://badminton-bvqs.onrender.com/) · [README](../README.md) · [Ảnh chức năng](FEATURE-TOUR.md)

## Tài khoản công khai

Chủ dự án cho phép công khai tài khoản mẫu ngày 04/11/2026. Đã kiểm tra đăng nhập qua giao diện Render: cả ba request đăng nhập trả HTTP 200, có phiên hợp lệ và đúng vai trò. Không lưu token vào tài liệu hoặc ảnh.

| Vai trò | Số điện thoại | Mật khẩu |
| --- | --- | --- |
| Customer | `0910000000` | `Pass@123` |
| Manager | `0900000002` | `Admin@123` |
| Admin | `0900000001` | `Admin@123` |

Các tài khoản có nguồn từ bộ dữ liệu mẫu của Badminton; Admin có quyền quản trị, Manager có phạm vi chi nhánh. Đây không phải tài khoản demo bị giới hạn quyền riêng. Không dùng thông tin đăng nhập này cho dịch vụ khác.

## Cách trải nghiệm

1. Mở trang đăng nhập, dùng SĐT và mật khẩu ở bảng trên.
2. Với Customer: xem Trang chính, lịch Đặt sân, Đơn của tôi, Nhóm và Đánh giá.
3. Với Manager: xem Tổng quan, Đặt chỗ, Sân & giá và Check-in trong phạm vi cơ sở.
4. Với Admin: xem thêm Cơ sở, Nhân viên, Nhật ký và cấu hình hệ thống.
5. Dùng cửa sổ ẩn danh hoặc trình duyệt riêng nếu muốn xem nhiều vai trò đồng thời.

Bản source có thêm CLB và các tiện ích vận hành; ảnh LOCAL/API MOCK trong thư viện minh họa các trang đó. Bản Render có thể khác phiên bản source: tại thời điểm chụp, menu live quan sát được chưa có mục Câu lạc bộ. Không suy ra chức năng live đầy đủ từ ảnh local.

## Dữ liệu dùng chung và giới hạn

- Dữ liệu có thể thay đổi do người dùng chung tài khoản. Dùng thông tin giả khi trải nghiệm; giữ nguyên mật khẩu, tài khoản mẫu và dữ liệu của người khác.
- Không có bằng chứng reset tự động sau 24 giờ hoặc khóa đổi mật khẩu/upload dành riêng cho tài khoản demo. Chính sách demo WebAttendance không được áp dụng sang Badminton.
- Server miễn phí có thể cần chờ khi khởi động. Cập nhật schema/deploy không được thực hiện trong nhiệm vụ viết README này.
- Cash/QR tại quầy cần nhân viên xác nhận; adapter online và hoàn tiền không đồng nghĩa đã nghiệm thu tiền thật.
- Đồng bộ lịch là đăng ký feed chỉ đọc, thời điểm cập nhật do ứng dụng lịch quyết định; camera/USB cần kiểm riêng trên thiết bị.

## Phạm vi xác minh 04/11/2026

**CURRENT:** trang giới thiệu/đăng nhập live mở HTTP 200; đăng nhập ba vai trò thành công; đã xem/chụp Trang chính khách, lịch sân và Tổng quan Manager/Admin. Phiên chỉ tồn tại trong browser context tạm, đóng sau chụp.

**NOT VERIFIED:** lượt này không tạo/hủy đơn, thanh toán, sửa tài khoản, thay đổi kho, đặt lớp, chia giải, tạo token calendar hoặc chạy migration/seed/reset. Không coi đăng nhập và ảnh chụp là nghiệm thu toàn bộ nghiệp vụ live. Lịch sử test riêng ở [worklog](worklog.md).
