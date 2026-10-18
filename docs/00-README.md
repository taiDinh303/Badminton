# 00 — Chỉ dẫn tài liệu hệ thống đặt sân cầu lông

> Chủ tài liệu: **Đinh Văn Tài** · Đồng đội: **Phùng Vĩnh Luân**. Cập nhật: **16/10/2026**.
> Phạm vi: đối chiếu Markdown, mã nguồn và cấu hình checkout. Chưa nghiệm thu runtime hoặc hạ tầng live trong lần chuẩn hóa này.

## 1. Bắt đầu ở đây

Đọc file này → [kế hoạch](08-plan.md) → [worklog](worklog.md) → tài liệu chuyên trách. Tham khảo cách tổ chức Marixa; nghiệp vụ, đường dẫn và hạ tầng thuộc **Badminton**. Giữ tên file hiện có để bảo toàn tham chiếu; đọc theo nhiệm vụ, không bắt buộc theo số.

| Công việc | Tài liệu chính |
| --- | --- |
| Quyết định, ưu tiên, phụ thuộc, điều kiện đóng việc | [08-plan.md](08-plan.md) |
| Phạm vi V1, quyền, giá, hủy/hoàn, điểm/voucher | [01-business-analysis.md](01-business-analysis.md) |
| Entity, quan hệ, index và migration | [02-database-design.md](02-database-design.md) |
| Đặt sân, thanh toán, lifecycle và ngoại lệ | [03-workflow.md](03-workflow.md) |
| Phân tầng, route, HTTP và bảo mật | [04-system-architecture.md](04-system-architecture.md) |
| Kiểm thử, dữ liệu test, bằng chứng nghiệm thu | [05-testing-strategy.md](05-testing-strategy.md) |
| Tổng quan triển khai, phát hành, khôi phục | [06-deployment.md](06-deployment.md) |
| Token, điều hướng, trạng thái UI, tiếp cận | [07-ui-design-system.md](07-ui-design-system.md) |
| Cấu hình, startup và chẩn đoán | [09-deploy-detail.md](09-deploy-detail.md) |
| Chạy trên máy phát triển | [10-setup-local.md](10-setup-local.md) |
| Chuẩn bị cloud và bàn giao | [11-setup-cloud.md](11-setup-cloud.md) |
| Đồng bộ thiết kế và code | [12-figma-sync.md](12-figma-sync.md) |
| Thuật ngữ chuẩn | [glossary.md](glossary.md) |
| Ma trận chức năng, bằng chứng, lịch sử | [worklog.md](worklog.md) |

## 2. Nguồn chuẩn và trạng thái

| Nhãn | Ý nghĩa |
| --- | --- |
| CURRENT / Hiện trạng | Có trong source; chưa tự chứng minh chạy đúng |
| TARGET / Mục tiêu | Yêu cầu nghiệp vụ hoặc thiết kế cần đạt |
| PENDING / Chưa triển khai | Chưa có luồng đầy đủ hoặc còn sai khác |
| NOT VERIFIED / Chưa xác minh | Chưa có bằng chứng chạy/kiểm tra live |

**Code xác định hiện trạng; yêu cầu nghiệp vụ xác định kết quả cần đạt.** Khi khác nhau, ghi lỗi và tiêu chí sửa; không đổi chính sách để hợp thức hóa lỗi. Ngày sửa docs không phải ngày nghiệm thu phần mềm.

Nguồn nghiệp vụ là [01](01-business-analysis.md), quyết định/backlog ở [08](08-plan.md), bằng chứng ở [worklog](worklog.md). Entity/mapping, controller/service, route và CSS phải đối chiếu trực tiếp khi làm nhiệm vụ.

## 3. Baseline và mục tiêu

| Thành phần | CURRENT trong repo | TARGET / PENDING |
| --- | --- | --- |
| Backend | ASP.NET Core .NET 8, EF Core, service/repository, REST `/api/v1` | Hoàn thiện quyền và nhất quán trước dùng thật |
| Frontend | React 19, Vite 8, TypeScript, Tailwind 4, Router 7, React Query, zustand | Kiểm thử browser, hoàn thiện trải nghiệm |
| DB | SQL Server `UseSqlServer`, 15 bảng, đa số PK int | PostgreSQL/Supabase; cần đổi provider/migration/raw SQL |
| Danh tính | SĐT + BCrypt + JWT .NET | Phone OTP qua Supabase Auth là hướng cũ, chưa tích hợp |
| Thanh toán | Mock online + Cash/QR tại quầy | Gateway thật, chữ ký và đối soát |
| Jobs | 4 BackgroundService trong API | Scheduler an toàn nhiều instance |
| Ảnh/QR | Trường URL/metadata; QR-like tile minh họa | Upload Storage, QR quét/xác thực thực sự |
| Hosting | Có `Frontend/vercel.json`; chưa có Dockerfile/blueprint API | SPA Vercel; host API/project DB cần xác nhận |

Không sao chép URL, project ID, plan, tài khoản hoặc quyết định riêng của Marixa. Chưa có snapshot hosting Badminton được xác nhận trong nhiệm vụ này.

## 4. Quy tắc bảo trì

1. Chủ dự án giao hoàn thiện toàn bộ docs ngày 16/10/2026. Quy tắc cũ chỉ sửa worklog được thay bằng quyền bảo trì tài liệu trong phạm vi nhiệm vụ được giao.
2. Bảo toàn thay đổi local; sửa mô tả/link/ví dụ dựa trên source. Chỉnh docs không tự cấp quyền đổi code/deploy.
3. Chính sách mâu thuẫn ghi cần chốt ở [08](08-plan.md); không giả lập quyết định chủ dự án.
4. Không thêm secret hoặc dữ liệu cá nhân vào ví dụ. `VITE_*` là dữ liệu frontend, không chứa khóa server.
5. ✅ trong worklog chỉ nghĩa có code; build/test/API/browser/live là mức bằng chứng riêng. Giữ lịch sử, ghi kết quả mới có ngày/lệnh/phạm vi.
6. Một file một H1, UTF-8, link tương đối trong repo. Lệnh phải nêu thư mục; đường dẫn hiện tại là `Backend/`.

**EN — Agent contract:** Preserve approved requirements and local work. Separate CURRENT, TARGET and NOT VERIFIED. Read relevant source, maintain authorized docs, record evidence and unresolved defects. Backlog is not permission to deploy.

## 5. Mẫu giao việc

```text
Đọc docs/00-README.md, docs/08-plan.md và docs/worklog.md.
Nhiệm vụ: <kết quả cần đạt và phạm vi>.
Đối chiếu source/đặc tả; giữ quyết định còn hiệu lực.
Thực hiện phần được giao, cập nhật docs chuyên trách và worklog.
Báo kiểm tra đã chạy, kết quả, giới hạn và phần chưa hoàn tất.
```

## 6. Quy ước tên và đường dẫn sau chuẩn hóa

Backend/API chứa Badminton.API.csproj (namespace Badminton.API); Backend/Tests chứa Badminton.Tests.csproj; Core dùng namespace Badminton.Core. Giữ tên các tầng Services, Contract.Services, Repositories, Contract.Repositories, ModelViews, Core. File BadmintonBookingDbContext.cs khớp tên lớp; migration/table/API URL không đổi.

Solution ở Backend/Badminton.slnx. SDK theo [global.json](../global.json), target ứng dụng vẫn net8.0; EF tool theo [.config/dotnet-tools.json](../.config/dotnet-tools.json). Build script và CI đã cập nhật path; kết quả kiểm tra mới ở [worklog](worklog.md).
