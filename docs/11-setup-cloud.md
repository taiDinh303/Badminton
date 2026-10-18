# 11 — Chuẩn bị cloud và bàn giao vận hành

> Chủ tài liệu: **Đinh Văn Tài**. Cập nhật: **16/10/2026**.
> TARGET/PENDING; chưa tạo hoặc thay đổi dịch vụ cloud.

## 1. Thông tin cần xác nhận

| Nội dung | Hiện trạng | Bằng chứng bàn giao |
| --- | --- | --- |
| API host/service, region, plan | Chưa xác nhận | Dashboard/owner, URL, chi phí được duyệt |
| Frontend project/domain | Có rewrite, chưa xác minh live | Project ID/branch/root/build |
| PostgreSQL/Supabase project | Chưa xác nhận | Project ref, region, connection mode, backup |
| Test cloud | Chưa xác nhận | DB/tài khoản riêng, dữ liệu được phép |
| OTP và gateway | Chưa tích hợp | Sandbox/callback domain/credential owner |
| Vận hành | Chưa chốt | Người trực, RPO/RTO, retention/cảnh báo |

Không dùng plan/region Marixa. Giá, quota và điều khoản cần xác minh tại thời điểm triển khai; không cố định hạn mức Free hay SLA chưa thỏa thuận.

## 2. Chuẩn bị ứng dụng

Đóng P0 ở [08](08-plan.md). Nếu chọn PostgreSQL, hoàn thiện provider/migration/raw SQL và suite DB test trước runtime. Nếu giữ SQL Server trên host phù hợp, ghi phương án riêng, không gọi đã cutover Supabase.

OTP cần ánh xạ user int sang identity provider, validate issuer/audience/JWKS, liên kết lịch sử và kế hoạch đổi phiên. Không xóa PasswordHash/đổi loại khóa chỉ vì docs ghi mục tiêu.

Storage cần bucket/policy/mime/size/ownership/upload API/vòng đời file; ImageUrl/Images chưa là upload. RLS cần mô hình danh tính thực; xem [09](09-deploy-detail.md).

## 3. DB và API

1. Chọn môi trường test theo quyết định owner; lưu secret đúng nơi.
2. Migration đã kiểm tra tạo schema; xác minh 15 bảng/FK/unique slot/admin/idempotency/ledger và dataset test.
3. Chạy test provider/concurrency/quyền; lưu schema evidence đã lọc secret.
4. Chuẩn bị artifact .NET 8/binding/TLS/worker theo host. Repo chưa Dockerfile, readiness hoặc blueprint API hoàn chỉnh.
5. Đặt Production/key mạnh/connection đúng provider/CORS origin đầy đủ. Startup tự migrate, cần kiểm soát DDL/lần chạy.
6. Bootstrap admin thật có kiểm soát; seed demo Development không là bootstrap production.

Chỉ bật nhiều instance sau kiểm tra jobs/lock/cache/rate-limit/migration. Tăng replica chưa chứng minh sẵn sàng cao.

## 4. Frontend static

Theo source: root Frontend; install npm ci; build npm run build; output dist. Build-time VITE_API_URL=https://<api-host>/api/v1, thay host đã xác nhận.

[vercel.json](../Frontend/vercel.json) rewrite SPA về index.html; kiểm tra refresh /bookings/<id>. API host riêng phải nhận request thực; rewrite SPA không là API proxy.

Không đưa DB credential/JWT signing key/secret key vào VITE_*. Biến Supabase public trong ví dụ chưa kích hoạt OTP/Storage hiện tại.

## 5. Nghiệm thu

- Gateway sandbox: đúng amount/currency; chữ ký sai bị từ chối; callback lặp không cộng trùng; muộn được đối soát.
- On-site: manager đúng cơ sở xác nhận, sai cơ sở/khách bị chặn; hai lần không thu hai khoản.
- Worker: Pending hết TTL giải phóng/trả voucher/điểm; restart/nhiều worker không hoàn trùng.
- Browser: khách đặt–thanh toán–đọc lại; staff/admin đúng quyền; CORS/deep link/lỗi mạng.
- Restore: DB/ảnh sang môi trường riêng, kiểm tra booking/payment/ledger/index, đối soát cửa sổ mất dữ liệu.

Test trên môi trường chỉ định. Xem [05](05-testing-strategy.md) và [06](06-deployment.md).

## 6. Biên bản bàn giao

```text
Ngày/giờ UTC+7; người thực hiện/người nhận:
Commit API/SPA; môi trường/URL/project/service/region/plan:
Provider/schema/migration:
Nơi quản lý secret và owner (không ghi giá trị):
Startup/migrate/seed/worker:
Kết quả build/test/smoke/quyền/load:
Backup/restore evidence; RPO/RTO đã chốt:
Gateway/OTP sandbox hay production:
Lỗi mở; rollback artifact/điều kiện:
Kết luận đạt/chưa đạt/chờ quyết định:
```

Chỉ điền đã xác minh; lưu snapshot/bằng chứng không bí mật vào [worklog](worklog.md). Cấu hình ví dụ không là cloud hoàn tất.
