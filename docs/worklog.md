# WORKLOG — Hệ thống đặt sân cầu lông

> File này là điểm bắt đầu cho bất kỳ AI/người làm việc tiếp trên dự án. Cập nhật sau mỗi lượt làm việc có thay đổi thực tế. Ghi sự thật đã kiểm tra; không đánh dấu "xong" khi chưa kiểm chứng. Quy tắc nghiệp vụ chuẩn nằm trong `01-business-analysis.md`; `01`–`07` và worklog ưu tiên khi khác nhau.

## Trạng thái hiện tại

- Ngày 13/10/2026, `Asia/Ho_Chi_Minh`. Bộ tài liệu `01`–`07` + worklog đã soạn theo đúng cấu trúc và mức độ chi tiết của chuẩn tham chiếu Marixa_Workforce.
- Quyết định kiến trúc hiện hành: **React + Vite SPA (không Next.js)** triển khai trên Vercel; **.NET 8 ASP.NET Core** (Clean Architecture 4 lớp + CQRS-lite) chạy container Linux trên Azure App Service (hoặc Render/ECS); **Supabase** (Auth OTP số điện thoại, Postgres + RLS, Storage) lưu dữ liệu. Frontend không dùng Docker; API đóng container một tầng runtime; không chạy `supabase start`/local stack.
- Chống overbooking dùng **unique partial index** `(court_id, date, slot_id) WHERE status IN (pending, confirmed)` + transaction `SELECT ... FOR UPDATE`; Redis lock chỉ là tối ưu, không phải source of truth. Webhook gateway là nguồn xác nhận đặt chỗ, không phụ thuộc client.
- Phạm vi V1: khách (đặt cá nhân/nhóm, thanh toán, voucher, điểm, đánh giá), manager (phạm vi cơ sở), admin (đa cơ sở, cấu hình). Không gồm membership, coach, shop, giải đấu, đa ngôn ngữ, phạt no-show tự động.
- **Chưa có:** mã nguồn .NET/React, Supabase project thật, migration đã áp dụng, kết quả load test hay URL deploy. Các mục dưới đây là tài liệu thiết kế, chưa phải bằng chứng kỹ thuật.

## Việc vừa hoàn thành (lượt 13/10/2026)

- Soạn lại 7 file thiết kế `01`–`07` theo chuẩn tham chiếu Marixa: tách rõ quy tắc nghiệp vụ (BR), ma trận quyền role, nguyên tắc dữ liệu và RLS, luồng state-machine, phân tầng .NET + 4 nguyên tắc OOP kèm SOLID, ma trận kiểm thử, quy trình triển khai Vercel/Supabase/Azure, và design system.
- Thống nhất cross-reference giữa các file (01 → 02 → 03 → 04 → 05 → 06 → 07). Bổ sung section **4 OOP + SOLID/DRY** vào `04` (phân tầng, phân module, Encapsulation/Abstraction/Inheritance/Polymorphism kèm ví dụ .NET và React).
- Rà luồng logic 8 file: phát hiện và sửa 3 điểm nhất quán — (1) sơ đồ `04` ghi "Next.js" đã đổi thành "Vite SPA" cho khớp quyết định §3.1; (2) làm rõ phạm vi **RLS** (DB bật + client không ghi, Storage RLS theo owner) trong `06` và ADR; (3) ghi rõ **backend lưu trữ Hangfire** (schema `hangfire` trên Supabase Postgres + leader election) trong `04` và `06`.

## Lệnh và kết quả kiểm chứng

- Các file `01`–`07` và `worklog` là Markdown viết tay trong workspace; nội dung đã được đọc lại sau chỉnh sửa. **Chưa chạy** command build/test nào vì chưa có mã nguồn .NET/React trong thư mục này.
- Chưa tạo Supabase project thật, chưa áp migration, chưa deploy Vercel hay Azure; mọi con số gói Free/Hobby và chi phí là **ước tính cần đối chiếu** với nhà cung cấp tại thời điểm triển khai.
- Chưa xác minh bằng thiết bị thật, không có kết quả load test p95. Không đánh dấu "đã triển khai" hay "đã test" khi chưa có bằng chứng.

## Việc tiếp theo

1. Tạo solution .NET (4 projects: Domain/Application/Infrastructure/Api) và skeleton React Vite; chuyển tài liệu `02` thành migration Postgres có version.
2. Viết unit test cho hàm giá, quy tắc hoàn tiền, hạn mức và unique index chống overbooking (50 request song song → đúng 1 winner).
3. Tạo Supabase project test, áp migration + RLS, seed admin duy nhất, kiểm tra quyền customer/manager/admin.
4. Nối một gateway thanh toán sandbox (VNPay/Momo/Zalopay), xác nhận webhook idempotent.
5. Chạy load test k6 giờ cao điểm và đo p95; đối chiếu hạn mức gói trước khi chọn production tier.
6. Chỉ sau các bước trên mới đánh dấu tương ứng mục "Trạng thái hiện tại" là hoàn thành.

Quy tắc nghiệp vụ chi tiết xem [01-business-analysis.md](01-business-analysis.md); dữ liệu xem [02-database-design.md](02-database-design.md); luồng xem [03-workflow.md](03-workflow.md); kiến trúc và OOP xem [04-system-architecture.md](04-system-architecture.md); kiểm thử xem [05-testing-strategy.md](05-testing-strategy.md); triển khai xem [06-deployment-vercel-supabase.md](06-deployment-vercel-supabase.md); giao diện xem [07-ui-design-system.md](07-ui-design-system.md).
