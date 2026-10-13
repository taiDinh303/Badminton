# 07 — Design system và giao diện đặt sân cầu lông

## 1. Hướng thiết kế

Thiết kế dùng bộ màu lấy từ cảm giác sân thể thao trong nhà: xanh lá chủ đạo (sân, trạng thái trống) và vàng điểm nhấn (CTA, cảnh báo giờ cao điểm); bảng dữ liệu nền sáng, ít trang trí. Chưa có file logo thương hiệu trong workspace; các mã màu dưới đây là **màu thiết kế chọn**, cần đối chiếu với file logo gốc (nếu có) khi phát triển UI. Giao diện ưu tiên mobile cho khách đặt sân, desktop cho manager/admin; mọi trạng thái phải kèm chữ giải thích. Không dùng chỉ màu hoặc chỉ icon để báo thành công/lỗi/trạng thái slot. Giao diện tiếng Việt, ngày `dd/MM/yyyy`, giờ 24h, số tiền `#.##0` (VNĐ) và hiển thị nhất quán.

Nguyên tắc: một màn hình một hành động chính trên điện thoại; manager desktop thấy việc cần xử lý (đặt chờ, no-show, bảo trì) trước biểu đồ; grid sân là trung tâm trải nghiệm. Slot dùng **cả chữ và màu** (trống/đặt/bảo trì) để không phụ thuộc màu duy nhất.

## 2. Design tokens V1

| Token | Giá trị | Cách dùng |
| --- | --- | --- |
| `brand.primary` | `#16A34A` | Nút chính, link, focus, mục menu đang chọn. |
| `brand.accent` | `#FACC15` | CTA nổi bật (Thanh toán, Đặt ngay); chỉ làm nền nút, chữ trên accent dùng màu tối. |
| `slot.free` | nền `#DCFCE7`, chữ `#15803D` | Slot trống, khả dụng. |
| `slot.booked` | nền `#FEE2E2`, chữ `#991B1B` | Slot đã đặt, không chọn được. |
| `slot.maint` | nền `#E5E7EB`, chữ `#6B7280` | Slot bảo trì/đóng. |
| `text.primary` | `#0F172A` | Nội dung chính. |
| `text.secondary` | `#475569` | Nội dung phụ, nhãn mô tả. |
| `surface.page` | `#F8FAFC` | Nền trang. |
| `surface.card` | `#FFFFFF` | Card, grid, form, thanh trạng thái. |
| `border.default` | `#E2E8F0` | Viền bảng/form/card. |
| `state.success` | `#16A34A` | Đã xác nhận/đã thanh toán, luôn kèm chữ. |
| `state.warning` | `#F59E0B` | Chờ thanh toán/sắp hết hạn slot, luôn kèm chữ. |
| `state.error` | `#EF4444` | Lỗi/trong/chung đột, luôn kèm hướng xử lý. |
| `focus.ring` | `#16A34A` | Viền focus rõ trên nền sáng. |

Chữ dùng `Be Vietnam Pro` (hoặc font sans hệ thống nếu font không tải được) để hỗ trợ tiếng Việt. Cỡ chữ: nội dung 16 px, nhãn phụ 14 px, tiêu đề trang 24 px, tiêu đề section 18–20 px; dữ liệu giờ/giá dùng chữ số tabular. Thang khoảng cách 4/8/12/16/24/32 px; bo góc 8 px cho input/nút, 12 px cho card; bóng đổ chỉ ở menu/hộp nổi/grid đang chọn. Phần tử chạm chính tối thiểu 44×44 px, khoảng cách giữa hai nút chạm tối thiểu 8 px. Đảm bảo độ tương phản chữ thông thường tối thiểu 4,5:1 và focus hiển thị rõ.

## 3. Cấu trúc điều hướng

### Khách trên mobile

Bottom navigation tối đa bốn mục: **Trang chủ**, **Đặt sân**, **Đơn của tôi**, **Tài khoản**. Ở trang đặt sân, phần trên là thanh chọn **cơ sở** và **ngày** (có mũi tên chuyển tuần); giữa màn hình là **grid sân**: hàng = sân, cột = giờ (cuộn ngang có snap), ô = slot với trạng thái trống/đặt/bảo trì kèm chữ. Chạm một slot trống mở **drawer xác nhận** (sân, giờ, giá, số người, voucher, nút Xác nhận). Chỉ hiện một CTA chính tại một thời điểm. Thanh dưới không che CTA.

### Manager/admin trên desktop

Sidebar gồm Tổng quan, Đặt chỗ, Sân/giá, Voucher, Báo cáo, Nhật ký (admin); header gọn với tên màn hình, cơ sở đang chọn, thông báo quan trọng và tài khoản. Bảng đặt chỗ có search, filter (cơ sở/ngày/trạng thái), sorting, phân trang và cột trạng thái dễ quét; filter quan trọng nằm trong URL. Form giá/voucher dài chia section; thao tác hủy/bảo trì có hộp xác nhận kèm lý do. Không dùng modal cho một quy trình dài.

## 4. Màn hình và trạng thái chính

| Màn hình | Ưu tiên nội dung | Trạng thái cần thiết |
| --- | --- | --- |
| Đăng nhập | Logo, số điện thoại, nút lấy OTP, xác thực 6 số, link tìm tài khoản | Đang gửi OTP, sai/đã hết hạn OTP, tài khoản bị khóa, lỗi mạng, chưa xác minh số. |
| Grid đặt sân | Chọn cơ sở + ngày, lưới sân/giờ, chú giải trạng thái, giá theo khung | Chưa có dữ liệu, slot trống, slot đã đặt, slot bảo trì, đang tải, hết hạn khung giờ. |
| Xác nhận đặt chỗ | Sân, giờ, số người, giá (gốc, voucher, phụ thu), phương thức thanh toán | Chưa chọn đủ, slot vừa bị chiếm, voucher không hợp lệ, đang giữ slot (đếm ngược), thanh toán chờ, thanh toán lỗi. |
| Chi tiết đặt chỗ | Mã đặt, QR, trạng thái, thông tin sân/giờ, nút hủy | Chờ thanh toán, đã xác nhận, đã hoàn thành, đã hủy, đã hoàn tiền, đã hết hạn, no-show. |
| Đơn của tôi | Danh sách sắp diễn ra/đã qua/đã hủy, điểm, voucher | Chưa có đơn, đơn chờ, đơn hoàn thành, đơn hủy có hoàn tiền. |
| Tài khoản | Hồ sơ, lịch sử, điểm tích lũy, voucher, nhóm chơi | Chưa xác minh, điểm có thể đổi, nhóm đang tham gia. |
| Admin tổng quan | Việc cần xử lý, doanh thu, lấp đầy peak, đặt chờ thanh toán, no-show | Loading, không có ngoại lệ, lỗi tải, cảnh báo storage. |
| Admin sân/giá | Danh sách sân, giá peak/low, phụ thu cuối tuần, khung slot, bảo trì | Cấu hình chưa đủ, kiểm tra thứ tự giờ, xem trước giá sau đổi, xác nhận lưu, lịch sử và audit. |
| Admin voucher | Tạo/hiệu lực, loại giảm %, min_spend, số lần, thời hạn | Voucher hết hạn, vượt tổng số, đang chạy. |

Không hiện giá gốc mờ một cách gây hiểu nhầm; hiển thị rõ từng khoản và tổng cuối cùng. QR chỉ hiện khi đặt chỗ xác nhận hợp lệ. Khi slot vừa bị khách khác chiếm, grid tự làm mới và ô chuyển sang trạng thái đã đặt kèm chú thích, không để người dùng bấm và chỉ báo lỗi muộn.

## 5. Nội dung thông báo mẫu

| Tình huống | Câu hiển thị |
| --- | --- |
| Giữ slot chờ thanh toán | “Đang giữ sân này 10 phút. Vui lòng hoàn tất thanh toán.” |
| Slot vừa bị khách khác đặt | “Sân A1 lúc 19:00 vừa được đặt. Hãy chọn khung giờ khác.” |
| Thanh toán thành công | “Đặt chỗ đã xác nhận. Mã: BM-XXXX. Xem QR để vào sân.” |
| Thanh toán chưa thành công | “Thanh toán chưa hoàn tất. Bạn có thể thử lại hoặc hủy để giải phóng slot.” |
| Đã hết hạn giữ slot | “Slot đã hết hạn giữ và được mở lại. Vui lòng chọn khung giờ khác.” |
| Hủy và hoàn tiền | “Đã hủy đặt chỗ. Hoàn tiền 100% (hoặc 50%) theo quy tắc, dự kiến 1–3 ngày.” |
| Hủy dưới 1 giờ | “Không đủ thời gian để hoàn tiền theo quy tắc. Đặt chỗ sẽ tính no-show.” |
| Voucher không hợp lệ | “Voucher không áp dụng cho đơn này (không đạt điều kiện tối thiểu).” |
| Sân bảo trì | “Sân đang bảo trì, chưa thể đặt khung giờ này.” |
| Vượt hạn mức | “Bạn đã đạt giới hạn số slot trong ngày. Vui lòng chọn ngày khác.” |

Không dùng “thành công” cho dữ liệu chỉ mới nằm trong hàng đợi chờ đồng bộ (đặt-chờ). Nút đang gửi phải khóa tạm và có phản hồi trong thời gian ngắn; không khiến người dùng bấm lặp vì không rõ trạng thái.

## 6. Tiếp cận, responsive và kiểm tra thiết kế

Kiểm tra ở 375, 768, 1024 và 1440 px; không cuộn ngang ngoài ý muốn ở mobile. Tôn trọng safe area điện thoại, không che CTA bằng bottom nav. Input có nhãn thật và thông báo lỗi cạnh trường; lưới slot điều hướng được bằng bàn phím (mũi tên di chuyển, Enter chọn slot); dialog có nút hủy, phím Escape và focus hợp lý. Trạng thái slot diễn đạt bằng **chữ + màu + icon** (trống/đã đặt/bảo trì), không chỉ màu. Ưu tiên tải ít ảnh và giữ kích thước cố định để tránh nhảy bố cục. Tôn trọng `prefers-reduced-motion`; chuyển động chỉ dùng để thể hiện kết quả thao tác, không cản thao tác. Dùng một bộ icon SVG nhất quán thay cho emoji.

Khi triển khai UI, kiểm tra lại màu (nếu có logo), test tương phản từng cặp chữ/nền, và làm ít nhất một vòng đặt–thanh toán–xem QR trên điện thoại thật. Luồng nghiệp vụ đối chiếu với [03-workflow.md](03-workflow.md); quyền màn hình và giới hạn nghiệp vụ đối chiếu với [01-business-analysis.md](01-business-analysis.md).
