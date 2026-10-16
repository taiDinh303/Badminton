# 08 — Kế hoạch, quyết định và backlog Badminton V1

> Chủ tài liệu: **Đinh Văn Tài**. Cập nhật: **16/10/2026**.
> Phạm vi: kế hoạch dựa trên source; chưa xác nhận runtime/hạ tầng.

## 1. Quyết định nền đang giữ

| Chủ đề | Nội dung | Căn cứ |
| --- | --- | --- |
| V1 | Đặt sân cá nhân/nhóm, thanh toán, voucher, điểm, review, bảo trì, quản trị | [01](01-business-analysis.md) |
| Stack | .NET 8 service/repository + React/Vite SPA; chưa CQRS/MediatR | Source/worklog cũ |
| Thời gian | Asia/Ho_Chi_Minh, UTC+7; timestamp UTC | HanoiTime/yêu cầu hiện hữu |
| Quyền | Customer, Manager, Admin; manager chỉ cơ sở được gán | Yêu cầu; code thiếu coverage |
| Giá | Low/peak; cuối tuần phụ thu trên giá cơ sở của slot; tiền đơn có snapshot | PricingService/Booking |
| Giữ chỗ | Pending 10 phút; unique index bảo vệ Pending/Confirmed | Mapping/job |
| Admin | Cần đúng một admin active; index chỉ bảo đảm tối đa một | Model/StaffService |
| Cloud | PostgreSQL/Supabase, OTP, Storage; scheduler/Redis/gateway thật chưa tích hợp | Hướng tài liệu trước, chưa nghiệm thu |

Định mức nghiệp vụ chỉ quản lý ở [01](01-business-analysis.md). Hướng cloud không đồng nghĩa đã chọn host, region, plan hoặc có tài khoản dịch vụ.

## 2. Các điểm cần chốt

| Mã | Mâu thuẫn / thiếu quyết định | Cần xác nhận trước sửa hành vi |
| --- | --- | --- |
| DEC-01 | Spec cộng điểm khi Completed; code cộng ngay Confirmed | Thời điểm ghi nhận; điểm đã tiêu trước hoàn tiền |
| DEC-02 | Spec trả điểm đã dùng khi hoàn full; code trả ở mọi tỷ lệ | Full/partial/0%, voucher và số dư âm |
| DEC-03 | No-show mô tả giờ bắt đầu/kết thúc khác nhau; job complete có thể chạy trước nhân viên | Mốc đánh dấu, thời gian xử lý, ưu tiên job/nhân viên |
| DEC-04 | Voucher có MaxPerUser nhưng unique ép một lượt/khách | V1 giữ một lượt; tăng lượt phải đổi constraint/test |
| DEC-05 | Retention 90 ngày/12 tháng chưa có job hoặc căn cứ pháp lý | Thời hạn, quyền xóa, backup, trách nhiệm |
| DEC-06 | Host/project cloud, Auth cutover và nhiều instance chưa bàn giao | Môi trường, chi phí, vận hành, rollback |

## 3. Backlog và phụ thuộc

| ID | Ưu tiên | Công việc | Tiêu chí đóng |
| --- | --- | --- | --- |
| SEC-01 | P0 | AuthorizeCancel từ chối actor không có quyền | A không hủy đơn B; staff đúng scope; test HTTP |
| SEC-02 | P0 | Manager scope: list, summary, payment read, lifecycle; chưa gán cơ sở fail closed | Không vượt scope qua ID/query/thiếu branch; 2 manager |
| SEC-03 | P0 | Quyền cấu hình slot theo đặc tả chỉ Admin; CURRENT Manager được POST/PATCH | Chốt/sửa quyền, test HTTP và UI theo role |
| PAY-01 | P0 | Liên kết hủy đơn đã trả và hoàn tiền | Không rơi Cancelled mất khả năng refund; audit/ledger đúng |
| PAY-02 | P0 | Gateway thật, callback muộn/hoàn lỗi | Sandbox chữ ký sai/lặp/đảo thứ tự/timeout; đối soát |
| DATA-01 | P0 | Idempotency theo actor/payload; giá lịch sử không tính lại từ cấu hình mới | Retry cùng yêu cầu cùng đơn; không lộ đơn actor khác |
| REP-01 | P0 | Đúng doanh thu, hoàn tiền, peak occupancy và UTC+7 | Dataset Pending/Refunded/low/peak; tổng khớp payment |
| DEV-01 | P0 | Sửa path build.ps1/CI; xác minh SDK đọc .slnx | Pipeline restore/build/model drift/test đúng checkout |
| QA-01 | P0 | HTTP/browser/load và assertion đúng | Bằng chứng [05](05-testing-strategy.md); 0 đặt trùng |
| INF-01 | P0 khi chuyển cloud | Provider/migration/raw SQL/Auth/Storage | Phụ thuộc DEC-06; suite PostgreSQL test, restore |
| OPS-01 | P1 | Jobs đa instance; rate-limit/lockout/cache/token khóa | Không xử lý trùng; tài khoản khóa không mutation |
| UI-01 | P1 | QR thực, lifecycle/redeem, payment/refresh | Browser theo role; mã xác thực server |
| OPS-02 | P1 | Backup/restore, correlation, readiness/cảnh báo | Restore evidence, owner, ngưỡng |
| DESIGN-01 | P2 | UI Kit/token/coverage Figma | File key/node mapping, kiểm tra mobile/desktop |
| OFF-01 | P2 | Offline/retry queue | Chỉ server xác nhận đơn; idempotency đúng |

Sửa độ đúng/quyền trước tăng tải hoặc nhiều instance. Không đổi DB/Auth/payment đồng loạt trong một lần release.

## 4. Điều kiện đóng việc

Mỗi việc: ID → source thay đổi → kiểm tra phù hợp → ngày/môi trường/kết quả → worklog. P0 chưa đóng thì chưa nghiệm thu dùng thật. Sửa tài liệu không đóng lỗi code.

Thay chính sách cần quyết định/người xác nhận/tác động dữ liệu/API/UI/test và chuyển dữ liệu cũ. Quyết định chưa chốt giữ PENDING. [worklog](worklog.md) lưu lịch sử và bằng chứng; ngày sửa docs không thay bằng chứng nghiệp vụ.
