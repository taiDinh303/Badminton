# 12 — Đồng bộ thiết kế và mã nguồn Badminton

> Chủ tài liệu: **Đinh Văn Tài**. Cập nhật: **16/10/2026**.
> CURRENT: CSS/component/route. NOT VERIFIED: chưa có file key/node ID Figma Badminton được xác nhận; chưa đồng bộ live.

## 1. Nguồn chuẩn

[CSS](../Frontend/src/index.css), [UI primitives](../Frontend/src/components/ui.tsx), [Layout](../Frontend/src/components/Layout.tsx), [routes](../Frontend/src/App.tsx) xác định code; [07](07-ui-design-system.md) quy định trải nghiệm cần đạt.

Không dùng file/key/quyết định thiết kế Marixa cho Badminton. Quy trình này là đề xuất; không xác nhận plugin/Code Connect/codegen đã tồn tại.

## 2. Mapping token

| Variable đề xuất | CSS CURRENT | Giá trị |
| --- | --- | --- |
| brand/primary | --primary | #16A34A |
| brand/accent | --accent | #FACC15 |
| surface/page | --bg | #F8FAFC |
| surface/card | --card | #FFFFFF |
| text/primary | --fg | #0F172A |
| text/secondary | --muted | #475569 |
| border/default | --border | #E2E8F0 |
| slot/free/bg, fg | --slot-free-bg, --slot-free-fg | #DCFCE7, #15803D |
| slot/booked/bg, fg | --slot-booked-bg, --slot-booked-fg | #FEE2E2, #991B1B |
| slot/maintenance/bg, fg | --slot-maint-bg, --slot-maint-fg | #E5E7EB, #6B7280 |
| state/success, warning, error | --state-success, --state-warning, --state-error | #16A34A, #F59E0B, #EF4444 |

Font stack Be Vietnam Pro → Inter → system chưa chứng minh tải webfont. Token tồn tại chưa chứng minh mọi cặp tương phản đạt chuẩn. Không tự thêm dark mode.

## 3. Coverage

| Nhóm | Màn/component | Trạng thái cần có |
| --- | --- | --- |
| Foundations | Màu/chữ/spacing/radius/focus/icon | Binding/naming thống nhất |
| UI Kit | Button/input/card/badge/callout/dialog/slot/table | Default/hover/focus/disabled/loading/error |
| Customer | Auth/home/book/bookings/detail/account | Loading/empty/error/success/permission/Pending hết hạn |
| Staff | Dashboard/bookings/courts/maintenance | Scope cơ sở/mutation |
| Admin | Voucher/slot/staff/audit | Validate/quyền/xác nhận/audit |

Mobile 375px và desktop 1440px là kích thước **đề xuất** cho mỗi route được chọn; không phải quyết định Figma đã xác nhận. Khi đồng bộ, ghi file/page/node/source path/commit/ngày kiểm tra vào bảng coverage.

## 4. Figma → code

1. Xác nhận file/node/phạm vi; đối chiếu source/route/quyền.
2. Tái sử dụng UI Kit/token/component trước tạo mới.
3. Map variable/state/responsive, ghi chi tiết chưa thiết kế.
4. Code trong nhiệm vụ được giao; không thay nghiệp vụ/API để giống mockup.
5. So sánh hai kích thước, keyboard/focus/lỗi mạng và ghi sai khác.

## 5. Code → Figma

Chốt commit → kiểm kê token/route/component → xác nhận nơi ghi → foundations/UI Kit → dựng màn/dữ liệu mẫu → kiểm tra editable/coverage. QR/payment minh họa không được ghi thành tính năng hoàn tất.

Chỉ đánh dấu đồng bộ khi đối chiếu cả code/design. Screenshot/scaffold/codegen chưa chạy không đủ. Đổi route/token cần cập nhật coverage và [07](07-ui-design-system.md).

## 6. Bàn giao

Ghi link file/node thật, commit, mapping token, frame/state, ảnh đối chiếu/lỗi mở/ngày UTC+7 vào [worklog](worklog.md). Trước đó Figma giữ NOT VERIFIED.
