# 21 — Backend thanh toán và bàn giao giao diện

## CURRENT — 14/11/2026

Backend đã có adapter MoMo, VNPAY, ZaloPay, lưu intent trước khi gọi gateway, callback xác minh chữ ký/số tiền/provider, chống ghi nhận trùng, thu tại quầy và đối soát/hoàn tiền. Lượt này bổ sung dữ liệu chuyển khoản VietQR theo cơ sở vào API init; không thêm bảng/migration, không sửa API cũ hoặc giao diện.

QR chuyển khoản ngân hàng là phương thức `QR`, độc lập ví `MoMo`. QR check-in của booking là mã khác, không dùng để chuyển tiền. Tiền nhóm, dụng cụ, học phí chưa được ghép vào thanh toán sân.

## Bàn giao tiếp theo — 16/11/2026

CURRENT: owner sẽ cung cấp thông tin nhận tiền sau. Chưa có tài khoản ngân hàng, ảnh QR hoặc khóa merchant do owner cung cấp cho nhiệm vụ này; chưa cấu hình lên web thật. Hướng đề xuất là QR theo đơn và nhân viên xác nhận tiền về; đây chưa phải quyết định bật thu tiền production. Ảnh QR cố định không tự điền đúng số tiền/nội dung từng đơn; backend hiện tạo Quick Link từ thông tin tài khoản, chưa có tính năng upload/giải mã ảnh QR thanh toán.

Thông tin cần nhận khi owner tiếp tục:

| Nội dung | Người cung cấp / mục đích |
| --- | --- |
| Tên ngân hàng, số tài khoản, tên chủ tài khoản đúng trên ngân hàng | Owner; đối chiếu BIN 6 chữ số, giữ số 0 đầu tài khoản. Có thể gửi ảnh QR để đối chiếu thêm, không bắt buộc. |
| Nhận cho tất cả cơ sở hay cơ sở cụ thể | Owner; lập danh sách BranchId thực. Nếu chung tài khoản vẫn khai báo entry cho từng BranchId, không có fallback chung. |
| URL frontend thật và dịch vụ backend/hosting đích | Owner xác nhận hoặc đối chiếu nguồn kết nối được; tránh cấu hình nhầm frontend hoặc môi trường. |
| QR staff-confirm hay tự động nhận chuyển khoản | Owner chốt. Tự động cần tích hợp thu hộ/webhook riêng, chưa có trong source này. |
| MoMo nếu cần bật | Bộ Partner Code/AccessKey/SecretKey đúng sandbox hoặc production, callback HTTPS. Cấp qua kho secret/host backend; không ghi khóa vào docs/chat/Git. |

Không yêu cầu mật khẩu ngân hàng, OTP, PIN hoặc thông tin thẻ. Thông tin tài khoản nhận thật cũng để ngoài Git, không thay placeholder trong tài liệu này bằng dữ liệu owner.

Trình tự để đồng đội tiếp tục khi có thông tin:

1. Đọc docs00/08/13/worklog và contract bên dưới; xác nhận môi trường, BranchId và tài khoản nhận. Làm UI ngay với fixture synthetic theo DTO, không chờ thông tin thật.
2. Với QR, đặt `Payment__Provider=onsite` và ba key BankBin/AccountNumber/AccountName cho từng BranchId trong cấu hình backend. Local có thể dùng User Secrets ở Development; host thật đặt Environment/Secrets. Không dùng VITE_* cho cấu hình này. Không commit ảnh QR/tài khoản thật hoặc thay đổi provider chỉ để vượt validation.
3. Chạy `dotnet run --project Backend/API/Badminton.API.csproj --no-launch-profile -- --check-config` tại root với environment/config đúng đích. Lệnh side-effect free chỉ kiểm cú pháp, không kiểm tài khoản nhận hay gateway có hoạt động.
4. Kiểm UI/API trên DB test riêng: owner đúng đơn, user khác/staff sai branch bị chặn, QR có đúng số tiền/nội dung/hạn, thiếu cấu hình/ảnh lỗi, hết hạn, staff-confirm và chống xác nhận trùng. Quét đối chiếu tên/ngân hàng/tài khoản trên app ngân hàng trước khi đề nghị chuyển; chuyển thử tiền thật chỉ trong phạm vi owner cho phép.
5. Khi có đủ thông tin và quyền triển khai, cấu hình/phát hành đúng dịch vụ, giữ Production không auto-migrate/seed. Kiểm version API/frontend, methods/init, hiển thị QR, quyền và quy trình xác nhận; ghi kết quả thực vào worklog. Push source không phải bằng chứng deploy hoặc tiền đã về.

MoMo riêng: `Payment__Provider=real`, `Payment__Momo__AppId` là Partner Code, thêm AccessKey/SecretKey/Endpoint/IpayUrl. Sandbox Endpoint là `https://test-payment.momo.vn` (chỉ domain, code tự nối path); IpayUrl là `https://<API-test-public>/api/v1/payments/callback/momo`, không localhost. ReturnUrl của frontend truyền theo request init, khác callback backend. **Giới hạn CURRENT:** mode real đòi đủ cấu hình VNPAY và ZaloPay; muốn chỉ bật MoMo cần task sửa cấu hình/DI/methods và tests trước, chưa được triển khai trong lượt bàn giao này. Không điền khóa giả cho gateway khác. Giữ OnlineRefunds=false đến khi nghiệm thu.

TARGET nghiệm thu MoMo: dùng DB test, merchant sandbox và MoMo Test App theo [hướng dẫn nhà cung cấp](https://developers.momo.vn/v3/docs/payment/onboarding/test-instructions/); thử thành công/thất bại/duplicate IPN/timeout/thu muộn và đối soát, kiểm Payment/booking thay vì chỉ redirect. Bộ khóa lấy theo [quy trình tích hợp MoMo](https://developers.momo.vn/v3/vi/docs/payment/onboarding/integration-process/). NOT VERIFIED các bước sandbox/live này; chưa có credential hoặc cấu hình host mới trong lượt 16/11.

## Contract chi tiết cho frontend

Các route dưới `/api/v1`, response thường bọc `{ code, statusCode, message, data }`; đối chiếu BaseResponse hiện hành. JWT Bearer bắt buộc trừ methods và callback. Tiền là số nguyên VND, lấy từ PayableAmount đã giảm voucher/điểm trên server.

| API | Cách dùng |
| --- | --- |
| `GET /payments/methods` | Public. `data` là mảng Cash/QR và các ví khi mode cho phép; mock có thể trả ví mô phỏng. |
| `POST /payments/init` | Body `{ "bookingId": 123, "method": "QR" }` hoặc `MoMo` kèm `returnUrl` tuyệt đối HTTPS (HTTP chỉ loopback). Owner hoặc staff đúng scope; đơn Pending chưa hết TTL. |
| `GET /payments/booking/123` | Owner/staff đọc danh sách PaymentView; dùng cùng GET booking để cập nhật trạng thái. Không lấy redirect query làm bằng chứng trả tiền. |
| `POST /payments/123/on-site?method=QR` | Chỉ Admin/Manager đúng cơ sở, sau khi kiểm tra sao kê; Cash tương tự. Khách không có quyền xác nhận. |
| `GET /payments/reconciliation?branchId=7&page=1&pageSize=50` | Staff đúng scope xem giao dịch cần đối soát. |
| `POST /payments/123/refund?reason=...` | Staff, tuân theo trạng thái/chính sách hiện hữu. |
| `POST /payments/reconciliation/456/refund` | 456 là paymentId; hoàn/kiểm tra lại khoản thu cần đối soát. |

Init giữ `method`, `gatewayUrl`, `gatewayRef`, `bookingCode`; thêm `bankTransfer` (null với Cash/ví hoặc cơ sở chưa cấu hình). Ví: điều hướng `gatewayUrl`; QR: hiển thị các trường dưới đây khi bankTransfer có giá trị:

```json
{
  "method": "QR",
  "gatewayUrl": null,
  "gatewayRef": null,
  "bookingCode": "<ma-don-tu-server>",
  "bankTransfer": {
    "bankBin": "<BIN-ngan-hang>",
    "accountNumber": "<tai-khoan-co-so>",
    "accountName": "<chu-tai-khoan>",
    "amount": 120000,
    "currency": "VND",
    "transferContent": "BM123",
    "qrImageUrl": "<https-quick-link-tu-server>",
    "expiresAtUtc": "2026-11-14T10:00:00Z",
    "requiresStaffConfirmation": true
  }
}
```

Hiển thị số tài khoản, tên, số tiền, nội dung có nút sao chép và hạn giữ chỗ theo UTC→Việt Nam. Không cho sửa số tiền/nội dung. Nếu bankTransfer null, hướng dẫn thanh toán tại quầy; không tự chế QR/tài khoản mặc định. Ảnh lỗi vẫn cho đọc/sao chép thông tin. QR image lấy từ dịch vụ ngoài img.vietqr.io: trình duyệt gửi tài khoản nhận, tên, số tiền, mã BM<bookingId> cho dịch vụ đó, nên dùng `referrerPolicy="no-referrer"` và cho phép host ảnh trong CSP. Backend không gọi dịch vụ tạo ảnh, không gửi tên/SĐT khách trong URL.

QR không tạo Payment Pending, không giữ chỗ thêm, không tự nhận tiền. Poll booking/payment có khoảng nghỉ (ví dụ 5 giây khi trang hiển thị), dừng khi terminal/hết hạn/đăng xuất; hủy cache khi đổi danh tính. Khách bấm “đã chuyển” chỉ có thể hiển thị chờ nhân viên, không gọi on-site. Hết TTL phải ngừng đề nghị chuyển; ảnh bên ngoài vẫn quét được sau hạn, không thể thu hồi lệnh chuyển ngân hàng. Chuyển muộn cần liên hệ cơ sở, không tự phục hồi booking. Không có webhook ngân hàng tự động trong lượt này.

MoMo: dùng init Method `MoMo`, lưu bookingId để trở về trang đơn. Callback server `POST /payments/callback/momo` mới có thẩm quyền xác nhận; không gọi callback bằng browser. Chờ booking Confirmed rồi báo thành công; lỗi/timeout init trả conflict cần kiểm tra đơn/đối soát, không đổi ví/tạo attempt khác để thử thu lại. API hiện chặn attempt online mới khi đã có attempt chưa rõ kết quả. Thu muộn được lưu để staff đối soát, không tự xác nhận đơn hết hạn.

## Cấu hình backend

Không có tài khoản mặc định. Thiết lập ngoài Git, theo đúng BranchId DB:

```text
Payment__BankTransfer__Branches__7__BankBin=<6-chu-so>
Payment__BankTransfer__Branches__7__AccountNumber=<6-den-19-chu-so>
Payment__BankTransfer__Branches__7__AccountName=<ten-chu-tai-khoan>
```

Không fallback sang cơ sở khác. Cấu hình đã khai báo sai bị check-config chặn bằng thông báo không chứa giá trị; thiếu entry vẫn giữ QR tại quầy cũ. Không xác minh được chủ tài khoản/BIN hoạt động bằng validation cú pháp. Ops phải đối chiếu và quét thử trên môi trường được phép trước thu tiền.

`Payment__Provider=onsite` chỉ Cash/QR, không cần merchant ví. `real` hiện yêu cầu đủ cấu hình cả ba gateway theo [13](13-configuration.md). MoMo dùng AppId (=partnerCode), AccessKey, SecretKey, Endpoint (base URL), IpayUrl (callback HTTPS); secret chỉ server, không đặt VITE_*. `mock` chỉ Development/Testing, không chứng minh tiền thật. Refund online mặc định tắt và giữ nguyên gate merchant sandbox.

Nguồn định dạng ảnh: [VietQR Quick Link](https://www.vietqr.io/qr-dong). Không dùng API generate sắp ngừng hỗ trợ. Luồng MoMo/chữ ký và cấu hình callback xem [13 mục 8–9](13-configuration.md).

## TARGET / NOT VERIFIED

TARGET: đồng đội xây màn thanh toán từ contract này, kiểm quyền owner/staff, pending/hết hạn/thất bại/ảnh lỗi và cache theo danh tính. Tự động nhận chuyển khoản cần nhà cung cấp thu hộ, credential và webhook/đối soát riêng; không dùng ảnh QR làm chứng từ nhận tiền.

NOT VERIFIED: merchant sandbox/live, quét QR ngân hàng thực, tài khoản nhận thực, callback ingress, API/frontend production và triển khai. Không apply migration, deploy, dùng DB live hoặc mua dịch vụ trong lượt này. Bằng chứng kiểm tra local và giới hạn máy ghi tại [worklog](worklog.md).
