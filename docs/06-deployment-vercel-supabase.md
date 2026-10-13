# 06 — Triển khai Vercel và Supabase cho hệ thống đặt sân cầu lông

> Tài liệu này mô tả cách triển khai frontend React trên **Vercel** và dữ liệu trên **Supabase**, API .NET 8 trên **Azure App Service** (hoặc Render/ECS như phương án tương đương), kèm giới hạn kỹ thuật và các điểm phải theo dõi. Không dùng Docker cho frontend; API .NET đóng container Linux một tầng runtime.

## 1. Mục tiêu và điều kiện

Dự kiến dùng **Vercel (Hobby hoặc Pro)** cho React SPA và **Supabase Free/Pro** cho Postgres/Auth/Storage. Tài liệu **không xác nhận gói Vercel Hobby phù hợp mục đích thương mại**: [Vercel ghi Hobby chỉ dành cho mục đích cá nhân phi thương mại](https://vercel.com/docs/plans/hobby) và [Fair Use bao gồm dự án do nhân viên được trả lương phát triển](https://vercel.com/docs/limits/fair-use-guidelines). Đây là rủi ro điều kiện sử dụng còn mở; phải ghi rõ trong bàn giao, không che giấu trong checklist.

Các con số gói Free có thể thay đổi; kiểm tra lại trước khi tạo tài khoản và trước mỗi lần đưa bản mới vào dùng. Theo [Supabase billing](https://supabase.com/docs/guides/platform/billing-on-supabase), Free hiện có 500 MB database mỗi project và 1 GB Storage. Supabase [có thể tạm dừng project Free ít hoạt động trong 7 ngày](https://supabase.com/docs/guides/deployment/going-into-prod). Khi database Free vượt 500 MB, project [có thể chuyển sang chỉ đọc](https://supabase.com/docs/guides/platform/database-size). API .NET cần hạ tầng riêng (Azure App Service P0/P1v2 hoặc Render) — không chạy được trên Vercel Serverless khi dùng Hangfire và cần 2+ instance.

## 2. Sơ đồ môi trường và biến cấu hình

Mã nguồn React được build trên Vercel; dữ liệu/Auth/ảnh ở một Supabase project; API .NET chạy container riêng và gọi Supabase bằng connection string quản trị. Nếu dùng project thứ hai cho thử nghiệm, tách hoàn toàn URL, key và dữ liệu thật. Không dùng dữ liệu khách thật ở preview. Các bí mật chỉ nhập trong Vercel Environment Variables, Azure App Service (Key Vault reference) và máy quản trị an toàn, không commit `.env`.

**Quy trình không dùng Docker cho frontend:** cài Node.js và PostgreSQL client (`psql`, `pg_dump`, `pg_restore`) trực tiếp trên máy phát triển. Chạy Vite bằng Node.js và dùng một Supabase project thử nghiệm hosted; không cài Docker Desktop, không có docker-compose, không dùng `supabase start`. [Vercel build Vite/React không cần Docker](https://vercel.com/i/do-you-need-docker-to-deploy). API .NET đóng Dockerfile một tầng runtime (build ở CI, image push ACR/ECR, chạy trên App Service) — đây là container cho hạ tầng, không phải quy trình phát triển local. Dùng kết nối Postgres từ mục **Connect** của Supabase cho tác vụ quản trị; [direct connection được khuyến nghị cho migration và backup, session pooler là lựa chọn nếu mạng chỉ có IPv4](https://supabase.com/docs/guides/database/connecting-to-postgres).

| Biến | Nơi dùng | Lưu ý |
| --- | --- | --- |
| `VITE_SUPABASE_URL` | Client | URL project, không phải bí mật. |
| `VITE_SUPABASE_ANON_KEY` | Client | Key public cho Auth OTP và Storage RLS; không cấp quyền admin bằng key này. |
| `SUPABASE_SERVICE_ROLE_KEY` | Server .NET only | Chỉ tác vụ quản trị (job, seed); tuyệt đối không có tiền tố `VITE_`. |
| `SUPABASE_DB` | Server .NET | Connection string pooler (port 6543) cho 2+ instance App Service. |
| `JWT_SIGNING_KEY` | Server .NET | Ký/verify token; giữ trong Key Vault. |
| `VNPAY_CONFIG`, `MOMO_CONFIG`, `ZALOPAY_CONFIG` | Server .NET | Config gateway (sandbox/prod tách riêng). |
| `HANGfire_STORAGE` | Server .NET | Dùng schema `hangfire` trên Supabase Postgres; bật **leader election** để nhiều instance không chạy job chồng. |
| `VITE_API_URL` | Client React | URL API .NET production (qua Cloudflare), dùng cho mọi gọi API. |

Tên key cần đối chiếu với SDK hiện hành khi code. Không đưa giá sân, khung slot, phụ thu cuối tuần hay hạn lưu ảnh vào biến môi trường: chúng là cấu hình nghiệp vụ có version/audit do admin quản lý trong database.

## 3. Trình tự thiết lập

1. Tạo Supabase project ở region phù hợp (Singapore/đông Á), bật **Auth bằng số điện thoại + OTP** và tắt đăng ký email công khai. Tạo ba bucket: `court-images` (public), `user-uploads` và `receipts` (private, RLS theo `auth.uid()`). Kiểm tra policy RLS cho bảng và Storage trước khi nhập dữ liệu thật; RLS cho DB **bật**, client không ghi trực tiếp — mọi mutation qua API .NET.
2. Lưu migration SQL có version trong repository và áp dụng bằng `psql` cài trực tiếp: chạy trên Supabase project thử nghiệm trước, kiểm tra schema/RLS và index chống overbooking, sau đó mới áp dụng production; ghi version đã áp dụng trong bảng lịch sử migration. Không sửa schema production bằng SQL Editor ngoài quy trình này. Tạo admin đầu tiên bằng quy trình bootstrap chạy phía server, chỉ một lần; không commit mật khẩu/OTP mặc định. Kiểm tra không có admin thứ hai.
3. Admin cấu hình cơ sở, sân (code, bề mặt, giá low/peak, phụ thu cuối tuần, tiện ích), khung slot (mặc định 06:00–23:00, 17 slot, peak từ 18:00) và các cơ sở manager phụ trách. Kiểm tra giá và khung có hiệu lực trước khi mở đặt chỗ; cấu hình mới không sửa đặt chỗ đã xác nhận.
4. Đổ bộ API .NET (image container) lên Azure App Service: 2 instance tối thiểu, health probe `GET /health`, Key Vault cho secret, bật Hangfire SQL storage với leader election. Kết nối Supabase bằng pooler connection (port 6543) để nhiều instance không vượt connection limit. Chạy migration đã kiểm tra ở bước 2.
5. Kết nối repository React với Vercel (framework Vite), cấu hình biến môi trường production và preview, domain/HTTPS, redirect URL Supabase Auth OTP tương ứng. Deploy, chạy smoke test: nhận OTP, xem grid, đặt sân, thanh toán sandbox, webhook, QR.
6. Cấp tài khoản manager theo cơ sở và kiểm tra quyền từng role. Gateway VNPay/Momo/Zalopay chuyển từ sandbox sang **production** có chữ ký webhook riêng; webhook URL công khai nhưng bắt buộc verify ký hiệu, idempotent theo `gateway_ref`.
7. Chạy thử ít nhất một vòng đặt–thanh toán–hủy với dữ liệu giả và một vòng sao lưu/khôi phục. Sau đó mới mở đặt chỗ thật.

## 4. Ảnh, dung lượng và retention

Ảnh sân upload một lần, nén WebP/JPEG, mục tiêu **150 KB/ảnh**, giới hạn đề xuất **400 KB/ảnh**; đường dẫn theo `branch/court/yyyy_mm/` không chứa tên thật khách. Ảnh review của khách lưu trong bucket private, nén trên thiết bị trước upload. Postgres chỉ lưu đường dẫn, kích thước, trạng thái và thời điểm; không lưu blob/base64. Trang danh sách không tải ảnh đầy đủ hàng loạt.

Phép tính gần đúng (20 sân/cơ sở, 3 cơ sở, 3 ảnh/sân, + 5.000 ảnh review/tháng):

| Nội dung | Ảnh | Dung lượng (150 KB/ảnh) | Ghi chú |
| --- | ---: | ---: | --- |
| Ảnh sân | ~180 | ~27 MB | Một lần, tái upload khi đổi |
| Ảnh review | 5.000/tháng | ~750 MB/tháng | Cần retention hoặc nâng Pro |

Với Supabase Free (1 GB Storage), ảnh review **phải có retention ngắn** (ví dụ 30–60 ngày) hoặc dùng gói Pro ngay từ đầu; admin phải chọn thời hạn lưu trước khi vận hành. Dọn ảnh hết hạn bằng job server (Hangfire, hằng ngày), xóa object Storage trước rồi đánh dấu metadata; xử lý lặp an toàn. Dashboard admin hiển thị tổng Storage/DB và ước tính tháng còn lại; cảnh báo ở 70%, 85%, 95% hạn mức. Khi gần đầy, ưu tiên dọn ảnh review hết hạn; **không xóa đặt chỗ, thanh toán, điểm hay audit** để lấy chỗ.

## 5. Sao lưu và giám sát

[Supabase Free không có backup tự động](https://supabase.com/docs/guides/platform/backups). Dùng `pg_dump`/`pg_restore` của PostgreSQL cài trực tiếp để sao lưu **schema và dữ liệu nghiệp vụ do ứng dụng sở hữu** ít nhất hằng tuần và trước migration; lưu bản mã hóa ngoài Supabase. Không dump rồi restore bừa toàn bộ schema Supabase managed (`auth`, `storage`): backup nghiệp vụ không tự chứa mật khẩu/OTP hay file ảnh. Ảnh Storage cần sao lưu riêng (job tải về archive hoặc bucket dự phòng); danh sách tài khoản và quy trình tái cấp/ghép lại với hồ sơ cũng phải ghi trong runbook khôi phục. Thử restore trên project thử nghiệm trước khi dùng thật và theo quý; chỉ đánh dấu backup đạt nếu khôi phục được đặt chỗ, thanh toán, voucher, điểm và quyền tài khoản. Ghi người thực hiện, thời điểm và kết quả.

Theo dõi hằng tuần: dung lượng DB/Storage, số đặt chỗ xung đột, webhook lỗi, hàng đợi Hangfire, độ trễ API p95 và trạng thái project. Nếu hạn mức tới ngưỡng chặn, admin nhận cảnh báo trước khi khách mất khả năng đặt chỗ; giao diện vẫn cho xem lịch nhưng không cho tạo đặt mới khi DB chuyển chỉ đọc. Cần có quy trình đối soát bằng hủy/hoàn có kiểm soát khi service tạm ngừng.

**Chi phí vận hành (ước tính hằng tháng, quy mô nhỏ):**

| Dịch vụ | Gói | Ước tính |
| --- | --- | --- |
| Vercel | Hobby/Pro | 0–~$20 |
| Supabase | Free → Pro (DB + Storage + Auth) | 0 → ~$25–$60 |
| Azure App Service (2 instance P0/P1v2) + ACR | Pay-as-you-go | ~$50–$150 |
| Cloudflare WAF/CDN | Free | 0 |
| Sentry (error) | Free tier | 0 |

## 6. Checklist nghiệm thu triển khai

- [ ] Migration, RLS và policy Storage đã qua test quyền customer/manager/admin.
- [ ] Chỉ một admin hoạt động; khách không tự nâng role; manager chỉ thấy cơ sở gắn.
- [ ] Cấu hình giá, khung slot, phụ thu cuối tuần, voucher đã có hiệu lực; unique index chống overbooking tồn tại trong production.
- [ ] Webhook ba gateway verify ký hiệu, idempotent; `service_role` key không nằm trong bundle React.
- [ ] Đặt–thanh toán sandbox–hủy và pending quá 10 phút chạy đúng trong smoke test.
- [ ] Có bản sao lưu DB, kế hoạch sao lưu ảnh và bài thử restore; retention ảnh review đã chốt.
- [ ] Không có bước Docker cho frontend, `supabase start` hay local stack trong quy trình; migration và backup đã thử bằng PostgreSQL client cài trực tiếp.
- [ ] API .NET 2+ instance chạy song song, Hangfire leader election bật, pooler connection không cạn.
- [ ] Rủi ro điều kiện Vercel Hobby (thương mại) và hạn mức Supabase Free đã ghi nhận trong bàn giao.

Xem kiểm thử chi tiết tại [05-testing-strategy.md](05-testing-strategy.md).
