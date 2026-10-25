# 14 — Checklist phát hành và bàn giao

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
