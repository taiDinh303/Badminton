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
| slot-maint | #E5E7EB / #6B7280 | Bảo trì |

Font stack Be Vietnam Pro/Inter/system chưa chứng minh font web được tải. Spacing thang 4–32px, input/button radius 8px, card 12px, touch ≥44px; số tabular, focus-visible và reduced-motion. Mọi trạng thái phải có chữ/icon, không chỉ màu. Contrast phải đo theo cặp thực tế.

## 3. Điều hướng và module

app/navigation.ts quản lý sidebar/customer/mobile/staff items, app/routes.tsx quản lý lazy routes/guards. Desktop sidebar sticky, header tên màn/vai trò; mobile bottom nav năm mục và Mở menu cho toàn bộ chức năng, bao gồm vận hành staff. Admin-only staff/audit/membership không hiển thị cho Manager.

| Route | Module | Nội dung |
| --- | --- | --- |
| /auth | auth | Login/register SĐT/password |
| / | customer | Cơ sở, lời chào, hành động đặt sân |
| /book, /bookings, /bookings/:id | booking | Availability, xác nhận, trạng thái/payment/hủy |
| /account, /groups, /reviews, /waitlist | Theo feature | Hồ sơ, nhóm, đánh giá, hàng chờ |
| /checkin | checkin | Nhân viên nhập code/token, đúng branch |
| /admin và màn con | admin | Dashboard/booking/courts/voucher/slot/staff/audit/membership |

## 4. Hợp đồng hiển thị

- Giá lịch sử đọc snapshot server. BasePrice hiện gồm phụ thu, WeekendSurcharge=0 vì không lưu split lịch sử; label cần nói rõ, không cộng lại giá hiện tại.
- Payment chỉ thành công khi server xác nhận; returnUrl không là bằng chứng. Refund phân biệt gửi yêu cầu và kết quả đã đối soát.
- QR token có server API; tile hiển thị hiện chưa là QR chuẩn quét. Không gọi đó là camera scan hoàn tất.
- 401 xóa session và về login; identity đổi hủy/clear query cache. Loading không dùng empty state; network timeout không báo thất bại chắc chắn rồi tạo key mới.
- Role guard chỉ giúp UI, API/DB vẫn enforce ownership/branch.

## 5. State và nghiệm thu

Mỗi màn cần loading/empty/validation/submitting/success/conflict/network/forbidden. Giữ dữ liệu form, focus lỗi đầu, disable gửi lặp, refresh query sau mutation; slot conflict cập nhật grid. Dialog có label/keyboard/Escape; menu có aria-expanded/controls và skip-to-main.

Kiểm 375/768/1024/1440px; desktop/mobile staff; deep link/Back/refresh; logout/expired token/cache; một vòng khách đặt/đọc đơn và staff đúng/sai cơ sở. Browser mock test không chứng minh tiền/DB thật; HTTP fixture kiểm boundary server riêng. [05](05-testing-strategy.md), [12](12-figma-sync.md) quản lý bằng chứng tiếp theo.
