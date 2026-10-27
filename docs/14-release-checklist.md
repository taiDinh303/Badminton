# 14 — Checklist phát hành và bàn giao

## Ưu tiên CLB owner chốt — 27/10/2026

Thực hiện theo thứ tự **dụng cụ → ledger chỉnh điểm → nghiệm thu ICS**. CURRENT source và bằng chứng local ở [19](19-club-hardening.md)/[worklog](worklog.md). Các ô dưới đây là TARGET của môi trường phát hành, chưa được tick bằng kết quả local và không cấp quyền triển khai.

### 1. Dụng cụ — job tự hết hạn

- [ ] Review/áp HardenClubWorkflows đúng provider theo quyền phát hành sau backup; cấu hình BackgroundJobs và kiểm heartbeat WaitlistPromotionJob.
- [ ] Trên fixture của môi trường được duyệt, đối chiếu hold chưa nhận hết hạn → không còn chiếm kho, từ chối giao, worker ghi Expired và inbox đúng một lần.
- [ ] Kiểm worker restart/chạy lặp và backlog nhiều batch; không giả định mọi bản ghi được xử lý trong một tick hai phút.
- [ ] Dụng cụ Issued quá hạn trả vẫn chiếm tồn đang thuê; nhắc trả không nhân đôi và không tự trả giả hoặc thu phạt.

### 2. Giải — ledger chỉnh điểm

- [ ] Đối chiếu TournamentScoreRevisions trên schema đã nâng cấp: người sửa, thời gian, điểm trước/sau, lý do và phiên bản.
- [ ] Kiểm staff đúng branch được sửa; khách/khác branch bị từ chối; sửa từ màn hình cũ trả409, không ghi đè kết quả và không tăng lịch sử khi replay cùng điểm.
- [ ] UI giữ bản đang nhập khi lịch sử có phiên bản mới; nạp lại có chủ ý trước khi sửa tiếp. Phân biệt append-only qua API với quyền SQL quản trị trực tiếp.

### 3. ICS — nghiệm thu UI, hạ tầng và điện thoại

**CURRENT local:** UI giải thích đăng ký URL, polling theo client, thu hồi không xóa bản đã cache và lần đọc feed chưa chứng minh đã hiện trên điện thoại. **NOT VERIFIED:** ingress/CDN/APM và ứng dụng lịch trên thiết bị thật.

- [ ] Trên URL HTTPS của môi trường được duyệt, ứng dụng lịch đọc được feed riêng tư không cần bearer; kiểm MIME text/calendar, cache private/no-store, không có cache dùng chung giữa hai tài khoản.
- [ ] Đối chiếu log thực tại API, reverse proxy/hosting, CDN và APM: che đoạn token trong URL; không ghi body trả token, query/header có secret hoặc lưu liên kết vào analytics. Bằng chứng phải được làm sạch, không đưa nguyên token vào ticket/ảnh/worklog.
- [ ] Đăng ký URL trên Apple/Google dùng tài khoản và đơn thử nghiệm được duyệt. Kiểm cùng UID sau đổi giờ, trạng thái hủy và không tạo sự kiện trùng; ghi thời điểm đổi trên server và thời điểm thiết bị cập nhật thực tế.
- [ ] Xoay/thu hồi liên kết: URL cũ bị từ chối, liên kết mới đọc được; xác nhận cách bỏ lịch đăng ký và xử lý bản cache trên từng ứng dụng. Không cam kết xóa từ xa bản đã lưu.
- [ ] Nếu thiết bị chưa cập nhật, đối chiếu LastFetchedAtUtc với log đã che token để phân biệt client chưa đọc, hạ tầng chặn và lỗi nội dung. Không dùng một GET thành công làm bằng chứng đồng bộ trên điện thoại.
- [ ] Chốt người chịu trách nhiệm, chu kỳ/trễ quan sát được, cách hỗ trợ khách và kết luận pass/fail/giới hạn. Chỉ đóng mục ICS khi có đủ bằng chứng hạ tầng và thiết bị theo phạm vi đã thống nhất.

Mỗi lần nghiệm thu ghi build/commit, môi trường, thiết bị/OS/ứng dụng lịch, ngày giờ UTC+7, dữ liệu thử, kết quả và người xác nhận. Không dùng đơn/khách thật hoặc bật deploy/migration chỉ từ checklist này.

> Chủ tài liệu: **Đinh Văn Tài**. Cập nhật: **20/10/2026**. TARGET release gate, chưa là nghiệm thu production.

## 1. Source/cấu hình

- [ ] Không secret/private artifact trong Git; SDK/runtime/Node/lockfile đúng.
- [ ] Check-config đúng env, không side effect; production cấm init/seed/mock payment.
- [ ] Publish không Development/Testing JSON; manifest không dùng domain giả như live.
- [ ] API base/CORS/issuer/audience/flags/notify/gateway đúng môi trường.

## 2. Nghiệp vụ/dữ liệu

- [ ] Config/architecture/SQL/HTTP ownership/branch tests đạt.
- [ ] Slot concurrency/idempotency/snapshot/voucher/điểm/refund đạt.
- [ ] Năm worker qua restart/nhiều instance, không nhân đôi ledger/booking.
- [ ] Migration sạch/nâng cấp, preflight dữ liệu, backup/restore rehearsal.
- [ ] Gateway sandbox thực: signature/replay/late/timeout/refund và đối soát.

## 3. Giao diện/vận hành

- [ ] Node/lint/build/browser đạt; tách mock API và API thật.
- [ ] Customer/Manager/Admin, deep link/mobile/session/cache/focus/contrast đạt.
- [ ] Không trình bày QR tile như camera scan hoàn chỉnh.
- [ ] /health, /ready đủ worker, metrics có giới hạn truy cập; log lọc secret.
- [ ] Người trực/RPO/RTO/retention/chi phí/rollback tương thích schema đã chốt.

## 4. Bằng chứng

### Gate tiện ích đặt sân — 26/10/2026

- [ ] Review migration AddBookingExperience đúng provider, backfill creator RSVP, RLS/grants và backup; không tự downgrade dữ liệu workflow.
- [ ] Đối chiếu đổi lịch cùng giá/TTL/hiện diện; batch toàn bộ hoặc không tạo; quyền owner/branch và race khi đổi cơ sở.
- [ ] Cấu hình Media:Directory bền vững, backup DB + tệp, kiểm đọc riêng tư/public avatar và quyền thư mục thực tế.
- [ ] Kiểm relay thật/preferences/retry; đối chiếu trạng thái gửi và đọc, xử lý sự cố không tự hủy đơn.
- [ ] Nhân viên nghiệm thu ca trực, phản ánh/no-show, moderation, CSV, privacy workflow và thông tin nhóm trên thiết bị thực.

Bằng chứng local và giới hạn ở [17](17-booking-experience.md) và worklog; checklist không cấp quyền phát hành.

Ghi ngày UTC+7/commit/môi trường/lệnh/pass-fail-skip/ảnh/lỗi mở ở [worklog](worklog.md). Chỉ tick khi có bằng chứng. Checklist không tự cấp quyền deploy. [06](06-deployment.md) quản lý release, [08](08-plan.md) quản lý việc mở.

## 5. Bằng chứng local bổ sung 24/10/2026

SQL Server/PostgreSQL đã có diễn tập native backup/restore, checksum dữ liệu/migration/index, kiểm unique slot sau restore và completion/expiry nhiều process + kill/recover job lock. Đây là bằng chứng local có giới hạn; giữ các gate production, endurance và merchant sandbox ở trạng thái chưa nghiệm thu. Người dùng xác nhận chưa có merchant sandbox; flag hoàn tiền online giữ tắt. Migration thêm OnlineRefundOperations chưa áp live. Quy trình/lệnh ở [công cụ rehearsal](../tools/OperationsRehearsal/README.md) và [cấu hình](13-configuration.md).

## Bổ sung gate ngoài thanh toán 25/10/2026

- [ ] Review/apply migration HardenNonPaymentWorkflows đúng provider sau backup và drain writer cũ; chưa được coi là đã chạy live.
- [ ] Đối chiếu marker unit-v2/số dư 1/1.000; legacy restore/clawback đúng đơn vị, không sửa ledger lịch sử.
- [ ] Kiểm hiện diện, completion, no-show, waitlist cạnh tranh, profile/session race, recovery một lần, search server và phân trang.
- [ ] Kiểm snapshot lấp đầy và nhãn historicalEstimate; không cam kết lịch sử trước baseline.
- [ ] Nghiệm thu thiết bị thật, cross-browser và endurance theo phạm vi được cấp.

Chi tiết [16](16-nonpayment-hardening.md), bằng chứng và giới hạn [worklog](worklog.md).

Local 25/10: full backend237/237 mỗi provider, bổ sung cuối16/16 mỗi provider; Firefox/WebKit smoke30/30, browser API/DB thật SQL Server và PostgreSQL1/1 mỗi provider, native backup/restore18process mỗi provider đạt. Các gate bên trên vẫn dành cho môi trường phát hành được chọn, đối chiếu dữ liệu thực và thiết bị/endurance; bằng chứng local không tự đóng gate production.
