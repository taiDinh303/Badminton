# 05 — Chiến lược kiểm thử hệ thống đặt sân cầu lông

## 1. Mục tiêu và mức kiểm thử

Ưu tiên kiểm chứng **không đặt trùng slot, không mất đơn, đúng giá, đúng quyền và đúng hoàn tiền**. Ba tầng:

- **Unit** (xUnit, phía .NET): hàm giá peak/low/cuối tuần, quy tắc hoàn tiền 2h/1h, hạn mức slot/ngày, logic voucher, điểm, chuyển trạng thái đặt chỗ. Chạy nhanh, không cần DB.
- **Integration** (xUnit + Supabase test project thật): API đặt chỗ + chống overbooking + webhook + RLS + migration. Nối tới **Supabase project thử nghiệm riêng với dữ liệu giả**, không dùng production.
- **End-to-end** (Playwright, phía React Vite): luồng khách đăng nhập OTP → đặt sân → thanh toán (mock/sandbox gateway) → QR, và luồng manager bảo trì/hủy.

**Không dùng Docker.** Frontend React chạy/test bằng Node.js trên máy (Vitest + Playwright). Backend .NET chạy `dotnet test` (xUnit) trên máy; integration test nối trực tiếp Supabase project thử nghiệm bằng connection string quản trị, không chạy `supabase start` hay Supabase stack cục bộ. [Supabase yêu cầu container runtime cho local stack](https://supabase.com/docs/guides/local-development), nên local stack nằm ngoài quy trình. Gateway VNPay/Momo/Zalopay dùng **sandbox** cho test; không dùng tiền thật. Mọi trường hợp ngày/giờ chạy cố định múi giờ `Asia/Ho_Chi_Minh`.

Bằng chứng thực thi mới nhất nằm trong `worklog.md`; thiết bị thật và URL deploy chỉ đánh dấu sau khi có kết quả thực tế.

## 2. Ma trận kịch bản bắt buộc

Các ví dụ giá dưới đây dùng **giá khởi tạo và khung slot mặc định**, không phải giá cố định trong code. Chạy lại với giá/cao do admin đổi và phụ thu cuối tuần khác nhau.

| Nhóm | Kịch bản | Kết quả mong đợi |
| --- | --- | --- |
| Đặt slot | Khách đặt slot trống giờ 18:00 (peak), chủ nhật | Giá peak × phụ thu cuối tuần; slot khóa; tạo QR sau thanh toán. |
| Đặt slot | Khách đặt slot giờ 07:00 (low-peak) thứ 3 | Giá low-peak, không phụ thu cuối tuần. |
| Giá | Cùng slot 18:00 thứ 7 so với thứ 2 | Thứ 7 = peak × (1 + cuối tuần), cao hơn thứ 2. |
| Cửa sổ đặt | Slot bắt đầu trong <30 phút hoặc >30 ngày | Không tạo được, thông báo đúng hạn. |
| Hạn mức | Khách đặt 5 slot cùng ngày | Từ chối slot thứ 5 (giới hạn 4), 4 slot đầu giữ. |
| Nhóm | Nhóm 8 người, nhóm thứ hai 12 người | Nhóm tối đa 10 thành viên; đặt 8 slot/ngày tối đa; 12 người bị chặn. |
| Chống trùng | Hai khách đặt cùng slot cùng lúc | Chỉ một khách `confirmed`, khách kia nhận `SLOT_CONFLICT`; unique index giữ đúng một record. |
| Pending | Đặt 08:00, không thanh toán trong 10 phút | Job tự hủy, slot mở lại; khách thấy thông báo hết hạn. |
| Thanh toán | Gateway sandbox trả success | Webhook chuyển `confirmed`, cộng điểm, tạo QR; đóng tab giữa chừng vẫn xác nhận. |
| Thanh toán | Gateway sandbox trả fail | Giữ `pending`, cho retry hoặc hủy; không tạo điểm. |
| Webhook lặp | Gateway gửi webhook success 2 lần | Idempotent theo `gateway_ref`, không cộng điểm 2 lần, không đổi trạng thái. |
| Hoàn tiền | Hủy cách 3 giờ (≥2h) trước giờ bắt đầu, đã thanh toán | Hoàn 100%, slot mở, giao dịch điểm âm nếu đã cộng. |
| Hoàn tiền | Hủy cách 1,5 giờ (1–2h) | Hoàn 50%. |
| Hoàn tiền | Hủy cách 30 phút (<1h) | Hoàn 0%, tính no-show nếu sau giờ bắt đầu. |
| No-show | 3 lần no-show trong tháng | Gắn cờ giới hạn đặt tháng kế tiếp. |
| Voucher | Voucher giảm 20% trần 50.000, min_spend 300.000 | Giảm đúng 20% (trần nếu vượt); đơn dưới min_spend không áp dụng. |
| Voucher | Khách dùng cùng voucher 2 lần | Lần hai bị chặn (unique redemption). |
| Điểm | Đơn 200.000 hoàn thành, sau đó hoàn tiền | Cộng 200 điểm rồi trừ 200 điểm (giao dịch âm), số dư về 0. |
| Đánh giá | Đánh giá đơn đang `pending` | Từ chối; chỉ đơn `completed` cho phép. |
| Quyền | Khách A gọi API đặt chỗ/điểm của khách B | Bị từ chối ở API và RLS, không lộ dữ liệu. |
| Quyền | Manager cơ sở A xem cơ sở B | Không thấy dữ liệu; admin thấy mọi cơ sở. |
| Admin duy nhất | Thử tạo admin thứ hai / khóa admin đang hoạt động | Từ chối; luôn đúng một admin hoạt động. |
| Bảo trì | Slot bảo trì rồi khách thử đặt | Slot khóa, không cho đặt; đơn đã confirm không tự hủy. |
| Export | Báo cáo doanh thu theo ngày/tuần/tháng | Đúng theo dữ liệu kỳ, có phân trang và quyền. |

## 3. Kiểm thử quyền và dữ liệu

Chạy test RLS cho cả `anon`, khách A/B, manager hai cơ sở và admin trên bảng đặt chỗ, thanh toán, voucher, điểm, review, audit và settings. Test cả `SELECT`, `INSERT`, `UPDATE`, `DELETE` để chứng minh không có đường ghi trực tiếp ngoài API .NET. Kiểm tra không tạo được admin thứ hai và không vô hiệu hóa được admin đang hoạt động cuối cùng. Kiểm tra `service_role` không nằm trong bundle React (chạy bundle analysis / grep output build).

Kiểm tra tính nguyên tử khi hai reviewer/transaction đặt cùng slot: chỉ một record giữ, không có double booking. Kiểm tra đổi giá/cấu hình sân không làm thay đổi kết quả của đặt chỗ đã xác nhận. Áp dụng migration có version vào Supabase test project sạch, kiểm tra schema/RLS rồi mới áp dụng production; dữ liệu test có cách dọn/reset riêng. Seed admin an toàn, không mật khẩu mặc định trong repo. Không yêu cầu `supabase start` hay `supabase test db` phụ thuộc local stack.

## 4. Kiểm thử giao diện và thiết bị (React Vite)

- Mobile ở 375 px và 768 px: grid sân cuộn dọc, chọn slot/số người/voucher dùng được, không cuộn ngang ngoài ý muốn, trạng thái pending/confirmed/hết hạn dễ hiểu; đủ vùng chạm.
- Desktop ở 1024 px và 1440 px: manager/admin lọc theo cơ sở/ngày/trạng thái, xem báo cáo và audit; refresh/Back/Forward giữ route và filter.
- Dùng bàn phím cho đăng nhập OTP, form, bảng, lưới slot và hộp thoại; focus rõ; thông báo trạng thái được đọc bởi screen reader; màu không là dấu hiệu duy nhất (slot dùng chữ + màu).
- Thử mạng chậm, ngắt mạng giữa thanh toán, đóng/mở tab; retry cùng `idempotency_key` không tạo đơn trùng; kiểm tra Chrome Android/desktop và Safari iOS trong phạm vi thiết bị hỗ trợ.
- Thử slot trùng: grid tự làm mới và hiển thị đúng trạng thái "vừa được đặt" chứ không crash.

## 5. Load và hiệu năng

Chạy k6 (hoặc JMeter) mô phỏng giờ cao điểm (17h–23h thứ 7/chủ nhật): tăng dần đến mức thiết kế, đo p95 API và kiểm tra **không bao giờ double-booking** dù hàng nghìn request song song vào các slot tranh thủ. Ngưỡng: p95 < 300 ms, 0 record trùng slot, không crash. Kiểm tra kết nối PgBouncer (Supabase pooler) không cạn pool khi nhiều instance .NET chạy song song. Đo độ trễ webhook payment và job hủy-pending không nghẽn hàng Hangfire.

## 6. Cổng chấp nhận trước dùng thật

Không đưa vào sử dụng nếu còn lỗi đặt trùng slot, mất đơn, tính sai giá/hoàn tiền, vượt quyền, hoặc webhook không xác nhận đúng. Đạt toàn bộ kịch bản trong ma trận, kiểm tra dung lượng theo dữ liệu giả, thử backup/restore ít nhất một lần, xác nhận admin đã cấu hình giá sân, khung slot, phụ thu cuối tuần, voucher và hạn lưu ảnh. Cảnh báo hạn mức/điều kiện gói (Free/Hobby) trong [06-deployment-vercel-supabase.md](06-deployment-vercel-supabase.md) phải được bàn giao rõ.
