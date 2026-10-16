# 06 — Tổng quan triển khai, phát hành và khôi phục

> Chủ tài liệu: **Đinh Văn Tài**. Cập nhật: **16/10/2026**.
> CURRENT: cấu hình repo. TARGET: cloud dự kiến. Chưa kiểm tra live.

## 1. Mô hình và ranh giới

SPA static gọi API .NET, API xử lý JWT/nghiệp vụ/background jobs, DB hiện SQL Server. Có [rewrite SPA](../Frontend/vercel.json), chưa có Dockerfile/blueprint API hoặc PostgreSQL runtime trong checkout.

Hướng đích tài liệu cũ: frontend Vercel, API trên dịch vụ .NET lâu dài, PostgreSQL/Auth/Storage qua Supabase. Host API, project ID, URL, plan/region chưa xác nhận; không dùng snapshot Marixa để điền.

## 2. Phân công tài liệu

| Nội dung | Nguồn |
| --- | --- |
| Env, startup, chẩn đoán | [09-deploy-detail.md](09-deploy-detail.md) |
| Local, cổng, lệnh chạy | [10-setup-local.md](10-setup-local.md) |
| Cloud, biên bản bàn giao | [11-setup-cloud.md](11-setup-cloud.md) |
| Gate API/DB/nghiệp vụ | [05-testing-strategy.md](05-testing-strategy.md) |
| Ưu tiên/chính sách mở | [08-plan.md](08-plan.md) |

## 3. Điều kiện phát hành

- [ ] P0 quyền, hủy/hoàn và báo cáo đóng với test tương ứng.
- [ ] Build/test/model drift đúng path; CI không dùng thư mục cũ.
- [ ] Production không seed demo; có bootstrap admin thật và bảo vệ admin cuối.
- [ ] JWT/CORS/API URL đúng; secret không trong frontend.
- [ ] Provider/migration/raw SQL/schema/index đúng môi trường.
- [ ] Payment sandbox, chữ ký, idempotency và đối soát đã nghiệm thu.
- [ ] Jobs chạy đúng qua restart/concurrency; không hoàn voucher/điểm trùng.
- [ ] Backup restore được; có cách đối soát giao dịch mới sau rollback.

## 4. Quy trình release TARGET

1. Chốt commit, phạm vi, người thực hiện/môi trường, lỗi mở và điều kiện dừng.
2. Chạy test trên dữ liệu test; lưu kết quả build/test/migration.
3. Backup, kiểm tra restore; thay schema tương thích ngược khi có thể.
4. Phát hành API/env; kiểm tra startup/migration/worker trước mở traffic.
5. Build SPA với API base có `/api/v1`; kiểm tra deep link/refresh.
6. Smoke khách → giữ sân → thanh toán → đọc lại đơn; staff đúng cơ sở; khách khác bị chặn.
7. Theo dõi lỗi, pending quá hạn, sai lệch payment, latency; ghi bàn giao.

Chưa có pipeline phát hành hoàn chỉnh. Trang chủ trả 200 không chứng minh DB/payment hoạt động.

## 5. Vận hành và rollback

| Sự cố | Xử lý đầu tiên | Điều kiện phục hồi |
| --- | --- | --- |
| API không startup | Key/DB/migration/env; giữ log lọc secret | Startup và đọc/ghi DB đúng |
| Conflict tăng | Phân biệt conflict hợp lệ/lỗi DB; giữ unique index | 0 slot trùng, phản hồi đúng |
| Callback lỗi | Đối soát gateway_ref/payment/booking; không tin returnUrl | Tiền/trạng thái khớp, replay không trùng |
| Pending quá hạn | Kiểm tra worker/UTC/restart/backlog | Chạy lại an toàn, trả voucher/điểm một lần |
| Release lỗi | Về artifact trước nếu schema tương thích | Smoke pass, không mất giao dịch mới |
| DB hỏng | Cô lập ghi, restore DB khác, đối soát | Toàn vẹn và bàn giao đạt |

Không tự rollback DB bằng Down nếu có nguy cơ mất dữ liệu. RPO/RTO, backup/retention và người trực cần chốt ở [11](11-setup-cloud.md), chưa là SLA.

## 6. Bằng chứng

Ghi commit, môi trường, ngày giờ UTC+7, migration, URL, lệnh, kết quả flow, lỗi mở, người bàn giao vào [worklog](worklog.md). Không ghi mật khẩu/token/raw webhook nhạy cảm.
