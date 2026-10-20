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
