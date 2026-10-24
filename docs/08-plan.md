# 08 — Quyết định, hiện trạng và kế hoạch

> Chủ tài liệu: **Đinh Văn Tài**. Cập nhật: **20/10/2026**. CURRENT theo source; kết quả kiểm tra ở worklog.

## 1. Nền được giữ

.NET 8 service/repository + React/Vite, SQL Server local, JWT/BCrypt; múi giờ nghiệp vụ UTC+7, timestamp UTC; ba role, manager một branch; một active slot; điểm bank khi Completed và restore điểm đã dùng khi full refund. Ngày 21/10/2026, người dùng chỉ định Supabase cho cloud và cho phép chuyển toàn bộ dữ liệu local: đã thêm PostgreSQL với bộ migration riêng. Không chuyển Identity/OTP chỉ để giống Marixa.

Cấu trúc tham khảo Marixa: contracts độc lập persistence, repository DI, composition ở API, pure config validation, explicit initialization, React feature modules/theme/navigation/providers. Người dùng giao chuẩn hóa docs/ASP.NET/React ngày 20/10; không bao gồm deploy live hoặc mua dịch vụ.

## 2. Kết quả kỹ thuật hiện tại

| ID | Trạng thái CURRENT | Giới hạn |
| --- | --- | --- |
| LAYER-01 | Contract.Services/Services không reference Repositories/EF/SqlClient/HttpContext; ICurrentUser API adapter | CURRENT 24/10: typed repository commands, không còn SQL string ở services/jobs; runtime PostgreSQL cần kiểm riêng |
| CONFIG-01 | Pure validation/check-config, Production cấm startup init/seed; dev flags, array/legacy CORS | Không xác minh credential/network thật |
| SEC-01 | Cancel owner guard có trong source mới; kiểm HTTP customer khác | Status domain conflict hiện vẫn 409, chưa đổi contract |
| SEC-02 | Shared staff active/branch guard cho list/summary/detail/payment-read/lifecycle/check-in | Cần full matrix các endpoint khi release |
| PAY-01 | CancelRefundAsync nối hủy đã trả với refund, có tests | Crash/gateway sandbox cần đối soát |
| DATA-01 | Retry key khóa transaction + fingerprint toàn payload; balance mutation khóa tài khoản; tiền lịch sử theo snapshot | Migration fingerprint cần áp riêng; đơn cũ thiếu hash từ chối replay |
| UI-01 | Lazy feature routes, shared nav/theme, mobile staff menu, cache/401 reset | QR mã hóa token thực/camera BarcodeDetector; chưa nghiệm thu thiết bị thật |
| DEV-01 | SDK9/paths/API/Tests/EF manifest/CI chuẩn hóa | CI remote phải đọc run thực tế |

## 3. Việc còn mở và tiêu chí

| Ưu tiên | Việc | Điều kiện đóng |
| --- | --- | --- |
| P0 trước thu tiền thật | Ba gateway sandbox/late/refund/đối soát | Evidence từ provider thực, không stub |
| CURRENT 24/10 | Full idempotency fingerprint và account lock cho points đã có source; hồi quy SQL trong worklog | NOT VERIFIED PostgreSQL runtime, crash gateway và triển khai migration live |
| P0 | Doanh thu theo tiền thu/hoàn/kỳ Việt Nam và full branch coverage | Dataset refund-only/Pending/Refunded/low/peak/qua ngày |
| P0 | Migration sạch/nâng cấp/backup restore | DB test riêng, kiểm index/ledger/tổng tiền |
| P1 | Worker multi-instance/restart/crash giữa claim và ledger | Không mất/nhân đôi nghiệp vụ, load evidence |
| P1 | QR chuẩn quét/server verify; camera UX nếu cần | Scan thiết bị thật, đúng trạng thái/branch |
| P1 | Upload ảnh/retention/RPO/RTO/metrics ingress | Quyết định owner và test quyền/vận hành |
| P2 | Offline retry, Figma UI Kit/file mapping | Không thành công giả, thiết kế có link thật |
| DONE local (PRICING-01, 24/10) | Owner chốt **giá theo sân/slot**; số người chỉ cap sức chứa. Server quote/create dùng chung giá và giảm voucher/điểm; không tính lại đơn lịch sử. | Bằng chứng mới trong worklog; không tự áp migration/deploy |
| Riêng | PostgreSQL/Supabase đã có provider, migration, raw SQL/lock và chuyển dữ liệu theo yêu cầu 21/10; OTP/Storage còn mở | Trạng thái cloud/test thực ở worklog; OTP/Storage cần nhiệm vụ riêng |

## 4. Chính sách cần giữ tách source

Ngày 21/10/2026, người dùng chốt voucher: mỗi tài khoản dùng **mỗi mã một lần**, vẫn dùng được mã khác. Bỏ cap toàn tài khoản cũ. Đồng thời yêu cầu tải trước trang theo quyền, review ngay trong chi tiết đơn (kể cả Admin là owner), bỏ nhãn ngày lặp và slot 02:00, thêm dữ liệu mẫu reviews/membership/vouchers trên Supabase. Các thay đổi này được triển khai theo URL live người dùng chỉ định; không phải quyền suy ra từ backlog.

V1 chưa bán subscription; membership Silver/Gold có code là mở rộng, không tự xác nhận gói bán/giá/gia hạn. Retention, RPO/RTO, host/domain/plan và mốc staff no-show so với auto-complete cần quyết định/bằng chứng vận hành. Không lấy chính sách nhân sự Marixa để điền.

Mỗi việc đóng bằng ID/source/lệnh/kết quả/giới hạn trong [worklog](worklog.md); release theo [14](14-release-checklist.md). Chỉ sửa docs không đóng lỗi ứng dụng hoặc xác nhận cloud.


## Security follow-up 24/10/2026 — CURRENT / TARGET / NOT VERIFIED

- CURRENT: callback bind reference/provider/merchant/amount/signed status; MoMo full IPN HMAC và Zalo data MAC; native callback routes; real refund fail closed; member PII chỉ cho owner/member; JWT đối chiếu trạng thái/quyền DB; voucher dates/validation/quote thống nhất và discount không vượt giá sân; tạo nhóm cùng owner membership trong một SaveChanges; waitlist không loại người chờ khi timeout hoặc conflict tạm thời.
- TARGET: refund thật và đối soát, nâng cấp/kiểm init adapters legacy với sandbox, transactional outbox, session version/reset/change-password/refresh/OTP/upload; kiểm backup-restore/load/device/cross-browser.
- NOT VERIFIED: GitHub Actions remote, Docker local (không có daemon), sandbox/provider certification và production. Test xanh local không đóng các mục này. Không triển khai hoặc gọi live data trong nhiệm vụ sửa lỗi.

## 5. Xử lý ưu tiên còn lại — 24/10/2026

- CURRENT: hai lỗi P2 hiển thị PartialRefund và ngày bảo trì UTC đã sửa, có browser/unit regression. Luồng refund/query online có intent bền vững, staff reconciliation theo branch, metadata đã xác minh và migration riêng hai provider; mặc định chưa bật.
- CURRENT local: SQL Server và PostgreSQL có bằng chứng full suite, native backup/restore, nhiều process completion/expiry và phục hồi DB job lock sau kill. Công cụ lặp lại ở tools/OperationsRehearsal, lệnh/kết quả/giới hạn ở worklog. Không đồng nhất diễn tập18 process với endurance production hoặc toàn bộ5worker.
- NOT VERIFIED / phụ thuộc bên ngoài: người dùng xác nhận chưa có merchant sandbox. Giữ Payment:OnlineRefunds:Enabled=false; P0 nghiệm thu init/charge/refund/query với provider còn mở. Không mua merchant/hosting, không áp migration hoặc restore vào live trong nhiệm vụ này.
- TARGET vận hành: nghiệm thu merchant, release migration mới theo backup/restore rehearsal, xác minh remote CI/Linux/Docker, tải dài và chốt RPO/RTO/retention. Không tuyên bố dự án hết lỗi hoặc mọi P0/P1 đã đóng chỉ từ local tests.
