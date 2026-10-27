# 08 — Quyết định, hiện trạng và kế hoạch

## Nhiệm vụ bổ sung 27/10/2026 — CURRENT source / TARGET release

### Thứ tự ưu tiên owner chốt sau đợt hoàn thiện

1. **Dụng cụ — job tự hết hạn:** ưu tiên trước vì ảnh hưởng trực tiếp tồn khả dụng/trải nghiệm khách. CURRENT đã có source và kiểm thử local; phát hành phải kiểm migration, BackgroundJobs, heartbeat, nhả hold và không nhả hàng đã giao trên môi trường được duyệt.
2. **Giải — ledger chỉnh điểm:** ưu tiên thứ hai. CURRENT đã có TournamentScoreRevisions, lý do, phiên bản và kiểm thử local; phát hành phải kiểm quyền, lịch sử trước/sau và stale-version trên schema đã nâng cấp.
3. **ICS — cảnh báo UI và nghiệm thu hạ tầng:** cảnh báo polling/cache và trạng thái đọc feed đã có source/browser evidence. Hạ tầng/thiết bị thật vẫn NOT VERIFIED; đóng bằng quy trình và bằng chứng tại [checklist phát hành](14-release-checklist.md), không thay bằng sửa code hoặc điểm đánh giá cao hơn.

Thứ tự này chi phối việc kiểm tra và phát hành tiếp theo; không tạo lại chức năng đã có, không tự cấp quyền deploy/migrate hoặc dùng dữ liệu live.

Đợt owner yêu cầu nâng6chức năng lên mốc8/10: CURRENT source bổ sung worker expiry, chia theo phút, biên bản hư/mất, sân học có booking xác nhận, audit/CAS sửa điểm và trạng thái đọc calendar. Hành vi/giới hạn và migration49bảng ở [19](19-club-hardening.md); bằng chứng worklog. TARGET release giữ riêng, không gán production đạt8/10 khi chưa có thiết bị/ingress và deploy thực.

Owner giao sáu chức năng CLB: tìm bạn theo trình độ/khu vực/giờ; chia chi phí nhóm; thuê dụng cụ/mua cầu/tồn kho/trả; lịch HLV cá nhân/nhóm; giải nhỏ; lịch điện thoại đăng ký tự lấy thay đổi. Source/API/UI và quy tắc mặc định ở [18](18-club-features.md), lệnh/bằng chứng ở worklog. Thêm 13 bảng với migration hai provider, giữ toàn bộ lịch sử và contracts cũ. TARGET release: backup/review/apply migration riêng, catalogue và lịch thực, kiểm thiết bị/ingress. NOT VERIFIED ứng dụng/live; không suy ra quyền deploy từ nhiệm vụ viết chức năng.

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
| CURRENT upload local / P1 vận hành | Upload PNG/JPEG đã có source/API/UI (mục 7); retention/RPO/RTO/metrics ingress còn mở | Cấu hình volume và backup tệp thực, quyết định retention/RPO/RTO, nghiệm thu vận hành |
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

## 6. Nhiệm vụ hoàn thiện ngoài thanh toán — 25/10/2026

Owner chốt điểm cơ bản 100.000đ=100 điểm, số dư cũ quy 1/1.000, cửa sổ check-in trước 30 phút đến hết slot, hoàn thành sau check-in/hết giờ; thiếu hiện diện chờ staff. CURRENT source và migration riêng: [16](16-nonpayment-hardening.md). Chỉ ghi DONE theo bằng chứng trong worklog; chưa áp DB ứng dụng/live. Smoke Firefox/WebKit local đã có bằng chứng; TARGET bên ngoài còn thiết bị thật/endurance, vận hành cloud và các gate thanh toán đã tách.

## 7. Tiện ích và thiết kế — 26/10/2026

Owner giao toàn bộ 23 mục bổ sung ngoài bảng bảo mật của review. CURRENT source/API/UI: đổi lịch cùng giá/chưa check-in; batch/định kỳ atomic mỗi slot một đơn; đặt hộ, lịch vận hành, xử lý phản ánh/sự cố, inbox/reminder/retry, ảnh, moderation/reply, timeline/CSV/privacy, ca trực/RSVP/ICS/favorites/insights. Ma trận và tiêu chí OOP/SOLID/patterns/Clean Code ở [17](17-booking-experience.md); bằng chứng kiểm thử cụ thể ở worklog.

TARGET phát hành: migration sau backup, thư mục media bền vững, relay thực được cấu hình và giám sát retry. NOT VERIFIED: DB ứng dụng/live, provider relay/thiết bị thực, endurance và remote CI. Privacy là quy trình xác minh/ghi kết quả, chưa tự xóa ledger. Giữ các gate thanh toán riêng; thay đổi guard xác nhận tại quầy chỉ ngăn quyền cơ sở cũ sau đổi lịch, không đổi chính sách tiền/gateway.
