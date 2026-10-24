# 07 — Design system và trải nghiệm đặt sân

> Chủ tài liệu: **Đinh Văn Tài**. Cập nhật: **20/10/2026**. CURRENT source; browser evidence ở worklog. Không có file Figma live được xác nhận.

## 1. Phong cách

Tham khảo shell trắng/xám, sidebar và header rõ của Marixa; giữ thương hiệu thể thao xanh/vàng và nội dung đặt sân. Mobile customer ưu tiên chọn sân/giờ; staff ưu tiên đơn chờ/check-in/bảo trì. Không sao chép menu chấm công/nhân sự.

## 2. Token

[theme.css](../Frontend/src/theme.css) là nguồn giá trị màu; index.css map Tailwind @theme và hành vi chung.

| Token | CURRENT | Mục đích |
| --- | --- | --- |
| primary | #15803D | Hành động/focus, tương phản tốt hơn xanh sáng cũ |
| accent | #FACC15 | Nhấn CTA, chữ tối |
| bg/card | #F5F6F8 / #FFFFFF | Trang/bề mặt |
| fg/muted/border | #0F172A / #475569 / #E2E8F0 | Nội dung phụ/cấu trúc |
| slot-free | #DCFCE7 / #15803D | Trống |
| slot-booked | #FEE2E2 / #991B1B | Đã đặt |
| slot-maint | #FEF3C7 / #92400E | Bảo trì (vàng); Open xanh, Closed đỏ |

Font stack Be Vietnam Pro/Inter/system chưa chứng minh font web được tải. Spacing thang 4–32px, input/button radius 8px, card 12px, touch ≥44px; số tabular, focus-visible và reduced-motion. Mọi trạng thái phải có chữ/icon, không chỉ màu. Contrast phải đo theo cặp thực tế.

## 3. Điều hướng và module

app/navigation.ts quản lý sidebar/customer/mobile/staff items, app/routes.tsx quản lý lazy routes/guards. Các màn yêu cầu đăng nhập dùng desktop sidebar sticky, header tên màn/menu avatar; mobile bottom nav năm mục và Mở menu cho toàn bộ chức năng, bao gồm vận hành staff. Admin-only staff/audit/membership không hiển thị cho Manager.

| Route | Module | Nội dung |
| --- | --- | --- |
| /auth | auth | Login/register SĐT/password |
| /home, / | customer/public | Trang giới thiệu công khai, không sidebar; / chuyển hướng /home; header đăng nhập/đăng ký, tiện ích và hướng dẫn |
| /dashboard | customer | Trang chính sau đăng nhập: lời chào, đồng hồ UTC+7, thao tác nhanh và cơ sở thực từ API |
| /book, /bookings, /bookings/:id | booking | Availability, xác nhận, trạng thái/payment/hủy |
| /account, /groups, /reviews, /waitlist | Theo feature | Hồ sơ, nhóm, đánh giá, hàng chờ |
| /checkin | checkin | Nhân viên nhập code/token, đúng branch |
| /admin và màn con | admin | Dashboard/booking/courts/voucher/slot/staff/audit/membership |

## 4. Hợp đồng hiển thị

- Giá lịch sử đọc snapshot server. BasePrice hiện gồm phụ thu, WeekendSurcharge=0 vì không lưu split lịch sử; label cần nói rõ, không cộng lại giá hiện tại.
- Payment chỉ thành công khi server xác nhận; returnUrl không là bằng chứng. Refund phân biệt gửi yêu cầu và kết quả đã đối soát.
- QR mã hóa token server; camera BarcodeDetector nếu hỗ trợ, nhập/dán/USB dự phòng. Chưa nghiệm thu thiết bị thật.
- Tài khoản sửa họ tên/email và URL ảnh đại diện (HTTP/HTTPS), lưu server theo danh tính đăng nhập; hiện membership/hạn/hệ số thực. Chưa có upload Storage.
- Membership admin chọn khách theo tên/SĐT, đổi/gia hạn/hủy và phân biệt còn hiệu lực/đã hết hạn.
- Đánh giá “Tất cả” tải đánh giá từng sân; danh sách chờ lấy dữ liệu người đang đăng nhập, liên kết đơn được nhường chỗ.
- 401 xóa session và về login; identity đổi hủy/clear query cache. Loading không dùng empty state; network timeout không báo thất bại chắc chắn rồi tạo key mới.
- Role guard chỉ giúp UI, API/DB vẫn enforce ownership/branch.

## 5. State và nghiệm thu

CURRENT 22/10/2026: `Frontend/src/mobile.css` áp dụng dưới breakpoint `md` hiện có (768px), cùng điện thoại xoay ngang có pointer coarse/hover none, chiều cao <512px và chiều rộng <1024px. Mobile dùng một lớp padding nội dung, header gọn, KPI co hàng, input 16px, nút có vùng chạm tối thiểu 44px và safe-area. Bảng quản trị giữ đủ cột trong vùng cuộn ngang có hướng dẫn; drawer đặt sân và modal có vùng cuộn theo chiều cao viewport, dùng được ở màn hình ngang thấp. Style PC giữ nguyên; ảnh đối chiếu và phạm vi browser mock ghi ở worklog. Thiết bị iOS/Android thật chưa nghiệm thu.

Mỗi màn cần loading/empty/validation/submitting/success/conflict/network/forbidden. Giữ dữ liệu form, focus lỗi đầu, disable gửi lặp, refresh query sau mutation; slot conflict cập nhật grid. Dialog có label/keyboard/Escape; menu có aria-expanded/controls và skip-to-main.

Kiểm 375/768/1024/1440px; desktop/mobile staff; deep link/Back/refresh; logout/expired token/cache; một vòng khách đặt/đọc đơn và staff đúng/sai cơ sở. Browser mock test không chứng minh tiền/DB thật; HTTP fixture kiểm boundary server riêng. [05](05-testing-strategy.md), [12](12-figma-sync.md) quản lý bằng chứng tiếp theo.

## 6. Trang công khai và trải nghiệm khách — 23/10/2026

CURRENT: /home ở ngoài Gate/Layout, khách chưa đăng nhập truy cập trực tiếp hoặc reload được; trang giới thiệu không gọi API riêng tư. /auth?mode=register mở form đăng ký, /auth có liên kết về /home. Đặt sân khi chưa đăng nhập chuyển qua auth và giữ đường dẫn nội bộ cần quay lại; đăng nhập trực tiếp mặc định /dashboard. Các trang đơn/tài khoản/vận hành vẫn có guard, server giữ quyền hiện hành.

Header ứng dụng có avatar với cursor pointer, dropdown tài khoản/đơn/trang chính/trang giới thiệu/đăng xuất, hỗ trợ Escape, Tab và click ngoài. Đăng xuất về /home và xóa session/cache. Đồng hồ cập nhật mỗi giây theo Asia/Ho_Chi_Minh, không dùng aria-live để tránh đọc liên tục. Dashboard có ô icon thao tác nhanh; chọn cơ sở truyền branchId sang màn đặt sân. Không dùng minh họa trên landing làm bằng chứng sân trống, giá hay số lượng cơ sở thực.

## 7. Giao diện rộng và form liên hệ — 23/10/2026

CURRENT: `redesign.css` thống nhất palette xanh đậm/lime, nền sáng, sidebar tối, mục đang chọn tương phản, header gọn, card/bảng/form và bottom navigation. Style landing nền đặt trong layer `home-base`; thiết kế chung có thể điều chỉnh riêng qua redesign. Giữ nhãn ngắn cạnh icon, focus ring, skip link, reduced motion và responsive 320–1440px. Logo trong sidebar đưa về /home.

Home có form họ tên/SĐT bắt buộc, email/lời nhắn tùy chọn, đồng ý liên hệ; không cần đăng nhập. Không lưu PII trong localStorage. Chỉ báo thành công sau POST /api/v1/contacts thành công; lỗi mạng giữ nguyên form. Hộp thư /admin/contacts chỉ dành Admin, phân trang 20 yêu cầu, gọi điện/email và đánh dấu xử lý; Manager không có quyền đọc danh sách toàn hệ thống. Đây là yêu cầu tư vấn, không giữ chỗ hoặc xác nhận đặt sân.
