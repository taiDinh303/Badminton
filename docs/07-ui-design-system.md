# 07 — Design system & giao diện đặt sân cầu lông

> **Chủ tài liệu: Đinh Văn Tài.** Cập nhật: 16/10/2026.
> **EN/AI:** Tokens below match the source code in `Frontend/src/index.css` (Tailwind v4 `@theme` + CSS variables). Don't hardcode hex in JSX — use the CSS variables (`bg`, `fg`, `primary`, `accent`, `muted`, `card`, `border`, `slot-*`, `state-*`).

## 1. Hướng thiết kế

- Bộ màu lấy từ cảm giác sân thể thao trong nhà: **xanh lá chủ đạo** (trạng thái trống, hành động chính) + **vàng điểm nhấn** (CTA). Bề mặt **sáng** (light) là mặc định.
- Chưa có file logo thương hiệu trong workspace → các mã màu là **màu thiết kế chọn**, cần đối chiếu file logo gốc khi có.
- Khách ưu tiên **mobile** (đặt sân); manager/admin ưu tiên **desktop**. Mọi trạng thái kèm **chữ giải thích**; không dùng chỉ màu hoặc chỉ icon cho thành công/lỗi/trạng thái slot.
- Giao diện tiếng Việt; ngày `dd/MM/yyyy`, giờ 24h, tiền `#.##0` (VNĐ, helper `formatVnd` trong `src/lib/api.ts`).

Các bảng màn hình, drawer, filter URL, keyboard grid và kích thước dưới đây là **TARGET**, cần browser QA; không suy ra đã có đầy đủ từ route/CSS. Font stack khai báo chưa chứng minh font web đã tải.

Nguyên tắc: **một màn hình một hành động chính** trên điện thoại; manager desktop thấy việc cần xử lý (đặt chờ, no-show, bảo trì) trước biểu đồ; grid sân là trung tâm. Slot dùng **chữ + màu + icon**.

## 2. Design tokens V1 (khớp `Frontend/src/index.css`)

| Token (CSS var / Tailwind) | Giá trị | Cách dùng |
| --- | --- | --- |
| `--primary` / `color-primary` | `#16A34A` | Nút chính, link, focus, menu đang chọn. |
| `--accent` | `#FACC15` | CTA nổi bật (Thanh toán, Đặt ngay); chữ trên accent dùng màu tối. |
| `--slot-free-bg/fg` | `#DCFCE7` / `#15803D` | Slot trống, khả dụng. |
| `--slot-booked-bg/fg` | `#FEE2E2` / `#991B1B` | Slot đã đặt. |
| `--slot-maint-bg/fg` | `#E5E7EB` / `#6B7280` | Slot bảo trì/đóng. |
| `--fg` (text.primary) | `#0F172A` | Nội dung chính. |
| `--muted` (text.secondary) | `#475569` | Nội dung phụ, nhãn. |
| `--bg` (surface.page) | `#F8FAFC` | Nền trang. |
| `--card` (surface.card) | `#FFFFFF` | Card, grid, form. |
| `--border` | `#E2E8F0` | Viền bảng/form/card. |
| `--state-success` | `#16A34A` | Đã xác nhận/trả, luôn kèm chữ. |
| `--state-warning` | `#F59E0B` | Chờ thanh toán/sắp hết hạn, kèm chữ. |
| `--state-error` | `#EF4444` | Lỗi/xung đột, kèm hướng xử lý. |

- **Font:** `Be Vietnam Pro` (fallback Inter/system) cho tiếng Việt (khai trong `@theme --font-sans`). Cỡ: nội dung 16px, nhãn 14px, tiêu đề trang 24px, section 18–20px; số giờ/giá dùng `tabular-nums` (class `.tnum`).
- Khoảng cách thang 4/8/12/16/24/32px; bo góc 8px (input/nút), 12px (card); bóng đổ chỉ ở menu/hộp nổi/grid đang chọn. Vùng chạm chính ≥ 44×44px, khoảng cách 2 nút ≥ 8px. Tương phản chữ ≥ 4.5:1; focus ring rõ (`:focus-visible` outline `--primary`). Tôn trọng `prefers-reduced-motion` (đã có CSS).
- Tailwind v4: dùng `@theme` + CSS variables, **không** hardcode màu trong JSX.

## 3. Điều hướng và bố cục mục tiêu

- **Khách (mobile):** bottom nav (Trang chủ, Đặt sân, Đơn của tôi, Tài khoản). Thanh chọn **cơ sở + ngày** (mũi tên chuyển tuần); giữa là **grid sân** (hàng = sân, cột = giờ cuộn ngang snap, ô = slot trống/đặt/bảo trì kèm chữ). Chạm slot trống → **drawer xác nhận** (sân, giờ, giá, số người, voucher, điểm, nút Xác nhận). Một CTA chính/lúc; bottom nav không che CTA.
- **Manager/admin (desktop):** sidebar (Tổng quan, Đặt chỗ, Sân/giá, Voucher, Slots, Nhân viên, Nhật ký) + header (tên màn hình, cơ sở đang chọn, thông báo, tài khoản). Bảng đặt chỗ: search, filter (cơ sở/ngày/trạng thái), sort, phân trang (dùng `VirtualizedTable` khi >100 dòng). Filter quan trọng nằm trong URL. Form giá/voucher chia section; hủy/bảo trì có hộp xác nhận kèm lý do.

## 4. Màn hình và trạng thái cần đạt — TARGET

| Màn hình | Ưu tiên nội dung | Trạng thái cần thiết |
| --- | --- | --- |
| `/auth` | SĐT + (dev) mật khẩu / (prod) OTP, link tìm tài khoản | Đang gửi OTP/sai, hết hạn OTP, tài khoản khóa, chưa xác minh |
| `/` (grid đặt) | Chọn cơ sở + ngày, lưới sân/giờ, chú giải trạng thái, giá theo khung | Chưa có dữ liệu, slot trống/đặt/bảo trì, đang tải, hết hạn khung |
| `/book` (xác nhận) | Sân, giờ, số người, giá (gốc/voucher/điểm), phương thức | Chưa chọn đủ, slot vừa bị chiếm, voucher không hợp lệ, đang giữ slot (đếm ngược), thanh toán chờ/lỗi |
| `/bookings/:id` | Mã đặt, QR, trạng thái, info sân/giờ, nút hủy | Chờ thanh toán, đã xác nhận, hoàn thành, hủy, hoàn tiền, hết hạn, no-show |
| `/bookings` (Đơn của tôi) | Danh sách sắp diễn ra/đã qua/đã hủy, điểm, voucher | Chưa có đơn, chờ, hoàn thành, hủy có hoàn tiền |
| `/account` | Hồ sơ, lịch sử, điểm, voucher, nhóm | Chưa xác minh, điểm có thể đổi, nhóm đang tham gia |
| `/admin` | Việc cần xử lý, doanh thu, lấp đầy peak, đặt chờ, no-show | Loading, không ngoại lệ, lỗi tải, cảnh báo storage |
| `/admin/courts` | Danh sách sân, giá peak/low, phụ thu, bảo trì | Cấu hình chưa đủ, xem trước giá, xác nhận lưu, audit |
| `/admin/vouchers` | Tạo/hiệu lực, loại %, min_spend, số lần, thời hạn | Hết hạn, vượt tổng số, đang chạy |

Hiển thị rõ từng khoản + tổng; không hiện giá gốc mờ gây nhầm. TARGET QR chỉ cho đơn hợp lệ và được xác thực server. CURRENT QrTile chỉ hình minh họa, chưa quét được. Slot vừa bị chiếm → grid tự làm mới, ô chuyển "đã đặt" kèm chú thích.

## 5. Nội dung thông báo mẫu

| Tình huống | Câu hiển thị |
| --- | --- |
| Giữ slot chờ thanh toán | “Đang giữ sân này 10 phút. Vui lòng hoàn tất thanh toán.” |
| Slot vừa bị đặt | “Sân A1 lúc 19:00 vừa được đặt. Hãy chọn khung giờ khác.” |
| Thanh toán thành công | “Đặt chỗ đã xác nhận. Mã: BM-XXXX. Xem QR để vào sân.” |
| Thanh toán chưa thành công | “Thanh toán chưa hoàn tất. Thử lại hoặc hủy để giải phóng slot.” |
| Hết hạn giữ slot | “Slot đã hết hạn và mở lại. Vui lòng chọn khung giờ khác.” |
| Hủy & hoàn tiền | “Yêu cầu hủy/hoàn đang được xử lý. Kiểm tra trạng thái giao dịch để biết kết quả.” |
| Hủy <1 giờ | “Không đủ điều kiện hoàn tiền theo thời gian hủy. Liên hệ cơ sở nếu cần hỗ trợ.” |
| Voucher không hợp lệ | “Voucher không áp dụng cho đơn này (không đạt điều kiện tối thiểu).” |
| Sân bảo trì | “Sân đang bảo trì, chưa thể đặt khung giờ này.” |
| Vượt hạn mức | “Bạn đã đạt giới hạn số slot trong ngày. Vui lòng chọn ngày khác.” |

Không dùng “thành công” cho dữ liệu chỉ đang nằm trong hàng đợi đồng bộ (đặt-chờ). Nút đang gửi khóa tạm + phản hồi nhanh để không bấm lặp.

## 6. Tiếp cận, responsive & kiểm tra

- Kiểm tra 375 / 768 / 1024 / 1440 px; không cuộn ngang ngoài ý muốn ở mobile. Tôn trọng safe area, không che CTA bằng bottom nav.
- Input có nhãn thật + lỗi cạnh trường; lưới slot điều hướng bằng bàn phím (mũi tên di chuyển, Enter chọn; dialog có Escape + focus hợp lý). Trạng thái slot = **chữ + màu + icon**.
- Ưu tiên tải ít ảnh, kích thước cố định (tránh layout shift); code-split route-level (admin pages lazy), mục tiêu bundle khách <200 KB gzip, chưa có số đo lượt này. Tôn trọng `prefers-reduced-motion`. Dùng bộ icon SVG nhất quán (lucide-react), không emoji.

Khi triển khai, kiểm tra tương phản từng cặp chữ/nền và làm ≥1 vòng đặt–thanh toán–xem QR trên điện thoại thật. Luồng đối chiếu `03-workflow.md`; quyền/giới hạn đối chiếu `01-business-analysis.md`.

## 7. Coverage CURRENT và sai khác cần sửa

| Khu vực | Có trong source | Giới hạn / tiêu chí cần đạt |
| --- | --- | --- |
| Auth | SĐT/password, JWT | Chưa OTP/reset/recovery đầy đủ; trạng thái khóa cần server xác thực |
| Route | App.tsx có customer/staff guard, admin route lazy | Chưa Admin guard riêng cho mọi route admin-only; API vẫn là bảo vệ thật |
| Detail | Giá/trạng thái/payment, cancel Pending | QrTile minh họa; method mock và URL init chưa xử lý đầy đủ; refresh mutation còn thiếu |
| Booking | Tạo Pending/voucher/slot | Không coi hiển thị giá client là giá chốt; timeout phải xác minh lại đơn |
| Lifecycle | API complete/no-show | Cần UI/quyền/thời điểm theo quyết định và lỗi mở |
| Dashboard | Summary endpoint/UI | Công thức doanh thu/peak còn REP-01; chưa mô tả KPI đã chính xác |
| CSS | Token, focus-visible, reduced-motion, tabular nums | Chưa có bằng chứng contrast/responsive/browser/keyboard hoàn chỉnh |

Đối chiếu [App.tsx](../Frontend/src/App.tsx), [BookingDetailPage](../Frontend/src/pages/BookingDetailPage.tsx), [API client](../Frontend/src/lib/api.ts). Đồng bộ Figma theo [12](12-figma-sync.md), chưa có file thiết kế thật được xác nhận.

## 8. Hợp đồng tương tác và nội dung

| State | Nội dung và hành vi cần đạt |
| --- | --- |
| Loading | Skeleton/busy có accessible label; không hiển thị “không có đơn” trong lúc đang tải |
| Empty | Nêu chưa có dữ liệu và CTA phù hợp, giữ filter/ngày đang chọn |
| Validation | Nhãn/lỗi cạnh trường, giữ dữ liệu đã nhập, focus lỗi đầu |
| Submitting | Disable thao tác lặp nhưng vẫn thông báo tiến trình; dùng cùng key khi retry |
| Success | Đọc dữ liệu server mới, invalidate cache liên quan, không chỉ toast |
| Conflict | Nêu slot/giờ cụ thể, làm mới grid, cho chọn lại; không xóa dữ liệu không liên quan |
| Network unknown | “Chưa xác minh được kết quả”; tra lại đơn trước gửi lại |
| Forbidden | Không lộ dữ liệu/cache của user trước; hướng về màn có quyền |
| Expired | Dựa server expiry, không kéo dài TTL bằng refresh browser |
| Refund pending | Phân biệt đã gửi yêu cầu/đã hoàn; không hứa thời gian chưa được gateway xác nhận |

Không đồng nhất Cash/QR tại quầy với QR check-in. “Thanh toán thành công” chỉ khi server đã xác nhận; “Hoàn tiền thành công” chỉ theo trạng thái refund/đối soát hợp lệ. Hủy <1 giờ không tự đồng nghĩa no-show.

## 9. Nghiệm thu giao diện

- Một lượt customer tạo đơn → payment → detail → đọc lại lịch sử; một lượt staff xử lý đúng branch và bị chặn sai branch.
- Responsive 375/768/1024/1440px, keyboard/focus trap/Escape, label và error đọc được, trạng thái không chỉ màu.
- Contrast đo từng cặp thực tế: xanh primary/vàng accent/text disabled không tự bảo đảm đạt chỉ vì có token.
- Deep link/Back/refresh, đổi user/logout, cache, timeout/retry, Pending expiry và conflict từ hai browser.
- Giá/tổng/điểm đúng đơn vị; thời gian UTC+7; QR thực cần scan và server verify trước nghiệm thu.

Ghi browser/device/commit/ảnh và lỗi mở ở [worklog](worklog.md). [05](05-testing-strategy.md) quản lý kịch bản, [08](08-plan.md) quản lý việc cần làm.
