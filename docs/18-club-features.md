# 18 — Tìm bạn, chi phí nhóm, dụng cụ, HLV, giải nhỏ và lịch đăng ký

> Nhiệm vụ owner ngày 27/10/2026: bổ sung sáu chức năng. CURRENT là source; bằng chứng chạy cụ thể ở [worklog](worklog.md). Không cấp quyền triển khai/migrate dữ liệu ứng dụng.

## 1. Baseline V1 — trước đợt hoàn thiện

**CURRENT mới nhất:** đợt owner yêu cầu nâng chất lượng 8/10 đã thay các giới hạn hết hạn/chia theo giờ/biên bản/sân học/audit ở bảng dưới bằng hành vi trong [19 — Hoàn thiện CLB](19-club-hardening.md). Giữ mô tả baseline này để truy được thiết kế và bằng chứng lần đầu; không dùng các câu “chưa có” bên dưới làm trạng thái hiện tại.

| Chức năng | Khách hàng | Quyền và nhất quán phía server |
| --- | --- | --- |
| Tìm bạn chơi | `/players`: lọc cơ sở, khu vực, trình độ 1–5, khoảng rảnh; mở buổi; xin tham gia/rút | Khoảng rảnh phải chứa toàn bộ buổi, tối đa 31 ngày khi tìm. Buổi tối đa 8 giờ, trong 90 ngày tới; 2–30 người gồm chủ buổi. Chủ buổi duyệt/từ chối/đóng; chỉ chủ xem danh sách yêu cầu, không công khai SĐT. Khóa giao dịch theo buổi ngăn duyệt quá chỗ. Tối đa 10 buổi mở/người. Buổi tìm bạn chưa tự giữ sân. |
| Chia chi phí | `/group-expenses`: chọn nhóm/đơn, chọn thành viên, xem ai đã đóng | Chỉ thành viên hiện tại đọc; người đặt sân chốt phần đóng góp và xác nhận/bỏ xác nhận. Tổng là `PayableAmount` của đơn Confirmed/Completed, chia đều số nguyên VND, phần dư lần lượt theo UserId tăng dần. Mỗi đơn một bảng bất biến; thử lại cùng danh sách không tạo trùng. Không ghi Payments/points/voucher. Sau hủy/hoàn giữ bảng cũ, cảnh báo tự đối chiếu; chặn ghi nhận đóng thêm, cho bỏ xác nhận khi đã trả lại tiền nội bộ. Có BookingHistory cho thay đổi xác nhận. |
| Dụng cụ/cầu | `/equipment`: xem tồn khả dụng, giữ vợt/dụng cụ cho thuê hoặc mua cầu, xem/hủy yêu cầu chưa nhận | Staff đúng cơ sở quản lý kho và giao/trả tại `/admin/club/equipment`. Giá/loại chốt trên đơn. Khả dụng = stock trừ Reserved và rental Issued. Bán cầu giảm stock đúng một lần khi giao; trả vợt giải phóng phần đang cho thuê. Không giảm stock dưới số đang giữ/cho thuê. Khóa theo item và request; cùng key khác payload từ chối. Tối đa 20 yêu cầu chưa nhận/người. Hẹn trong 30 ngày; nhân viên xử lý yêu cầu quá hẹn, chưa có chính sách tự hết hạn. |
| HLV | `/coaches`: chọn HLV, xem lịch, đặt số người và hủy trước giờ bắt đầu | Staff đúng cơ sở công bố hồ sơ/lịch ở `/admin/club/coaches`. Lịch 1 chỗ là cá nhân; 2–20 chỗ là lớp nhóm có thể nhận nhiều nhóm đặt riêng. Giá theo người chốt trên reservation. Khóa theo HLV chặn lịch giao nhau và vượt capacity, cho phép lịch sát biên. Staff hủy cả lớp trước giờ bắt đầu sẽ hủy các reservation; hoàn thành sau giờ kết thúc. Không ngầm giữ sân học. |
| Giải nhỏ | `/tournaments`: chọn giải, đăng ký tên đội/tên hiển thị thành viên, rút trước chia bảng, xem lịch/kết quả/xếp hạng | Staff đúng cơ sở tạo/quản lý tại `/admin/club/tournaments`. 2–32 đội, mỗi captain một đội/giải, tên không trùng. Chia 1–8 bảng, mỗi bảng ≥2 đội, phân đội lần lượt theo đăng ký; vòng tròn một lượt, hỗ trợ số đội lẻ với lượt nghỉ. Khóa theo giải, không chia lại khi đã tạo lịch. Xếp lịch chặn đội/sân trùng giờ trong các giải đang hoạt động cùng cơ sở. Đây là lịch tổ chức; staff giữ sân riêng. |
| Lịch đăng ký | `/calendar-sync`: bật/xoay liên kết, mở ứng dụng lịch, thu hồi | Token ngẫu nhiên 256 bit, chỉ trả nguyên token một lần, DB chỉ lưu SHA-256. Feed không yêu cầu bearer vì ứng dụng lịch sử dụng URL riêng. UID theo BookingId giữ khi đổi giờ; UTC start/end, LAST-MODIFIED và CANCELLED cho đơn hủy/hoàn/no-show/xóa/giữ chỗ hết hạn; Pending hợp lệ là TENTATIVE. Không có PII thành viên/ghi chú/payment trong feed. Khóa/thu hồi tài khoản hoặc liên kết chặn lần đọc tiếp theo. |

Các mặc định chia đều, thang trình độ, giao nhận tại cơ sở và vòng tròn là lựa chọn triển khai cho yêu cầu chưa chỉ định chi tiết. Chưa có thu tiền online cho học phí/dụng cụ, tính phạt hư/mất, chia tiền theo giờ tham dự hoặc vòng loại trực tiếp. Không áp chính sách loyalty/thanh toán mới lên đơn sân.

Điểm trận giải nhỏ là tổng điểm nguyên 0–99 do tổ chức nhập, không hòa, chỉ nhập sau giờ kết thúc lịch đấu. Không tự coi là kiểm chứng luật thi đấu chính thức/best-of-three. Xếp hạng theo trận thắng, hiệu số rồi Id; đồng điểm cần tổ chức quyết định riêng, không tự công bố vô địch. Có thể sửa điểm để sửa kết quả; giữ UpdatedAt, không có ledger chỉnh điểm riêng. Giải Completed khi tất cả trận có điểm; giải đã hoàn thành không bị hủy. Giải đã hủy giữ lịch sử.

## 2. CURRENT — API và cấu trúc

Routes bổ sung dưới `/api/v1/club`, không đổi routes/contracts đặt sân hiện hữu:

| Nhóm | API chính |
| --- | --- |
| Players | GET/POST `players`; POST `{id}/join`, `leave`, `close`; GET `{id}/members`; PUT `{id}/members/{userId}` boolean accept |
| Expenses | GET `expenses/bookings?groupId&page`; GET/POST `expenses/{bookingId}` (POST int[] thành viên); PUT `{bookingId}/members/{userId}` boolean paid |
| Equipment | GET/POST `equipment`; PUT `equipment/{id}`; GET/POST `equipment/orders`; POST `orders/{id}/cancel`, `issue`, `return` |
| Coaching | GET/POST `coaches`; PUT `{id}`; GET/POST `{id}/slots`; POST `slots/{id}/cancel`; GET/POST `reservations`; POST `reservations/{id}/cancel`, `complete` |
| Tournaments | GET/POST `tournaments`; GET `{id}`; POST `{id}/teams`; DELETE `{id}/teams/mine`; POST `{id}/draw` (int số bảng); PUT `{id}/matches/{matchId}/schedule`, `score`; POST `{id}/cancel` |
| Calendar | GET/POST/DELETE `calendar` (JWT); GET `calendar/feed/{token}.ics` (anonymous token + public rate limit) |

Danh sách phân trang 30 phần tử; client làm mới query `club` sau mutation, dùng transport/session isolation hiện hữu. `staff=true` cho list dụng cụ/HLV yêu cầu server kiểm quyền và giới hạn branch; không tin role gửi từ client. Timestamp request yêu cầu UTC (`Z`); form giờ Việt Nam chuyển cố định +07, không phụ thuộc timezone trình duyệt. Request key đặt dụng cụ/HLV giữ qua retry lỗi, server so payload gốc.

Entities trong Contract.Repositories; DTO trong ModelViews; interface Contract.Services; sáu service consume IUnitOfWork/IOperationsAccess/IBusinessClock, không phụ thuộc EF/ASP.NET. Repository cấu hình model và migrations; API compose DI/controllers/rate limit/MIME. Frontend module `club` tách pages/components, transport/types trong services và route/nav trong app. Không thêm dependency ứng dụng mới.

## 3. Baseline AddClubFeatures — schema và phát hành

Sau HardenClubWorkflows, CURRENT là49bảng và15bảng CLB; xem [19](19-club-hardening.md). Phần dưới ghi schema của lần bổ sung đầu tiên.

AddClubFeatures riêng SQL Server `20261027115559` và PostgreSQL `20261027115721`, sau AddBookingExperience. Thêm 13 bảng: PlayerSessions, PlayerSessionMembers, GroupExpenses, GroupExpenseShares, EquipmentItems, EquipmentOrders, CoachProfiles, CoachSlots, CoachingReservations, MiniTournaments, TournamentTeams, TournamentMatches, CalendarSubscriptions. Model hiện có **47 bảng** (đếm trực tiếp mapping; mô tả cũ 33 bảng thiếu một bảng của baseline 34).

Có FK Restrict, unique membership/share/captain/team-name/request-key/token-hash và indexes tìm/lịch. Migration không sửa bảng tiền/điểm/booking lịch sử, không seed catalogue. PostgreSQL bật RLS, revoke PUBLIC/anon/authenticated cho 13 bảng và sequences; chỉ API backend truy cập. Down chỉ cho phép khi toàn bộ 13 bảng còn rỗng; có dữ liệu phải khôi phục backup hoặc bản phát hành bù được review.

TARGET trước phát hành: review SQL đúng provider, backup/restore rehearsal và quyền áp migration riêng; cung cấp catalogue/kho/giá/HLV/lịch thực theo từng cơ sở. Không khởi tạo dữ liệu giả trong Production, không thêm auto-migrate.

## 4. Lịch điện thoại và vận hành

Đăng ký URL khác với nhập một file ICS một lần: client tự đọc lại cùng feed, server phản ánh dữ liệu booking hiện tại. Giữ cả tombstone đã hủy trong feed để client cập nhật sự kiện cũ. `REFRESH-INTERVAL`/TTL là gợi ý 15 phút; ứng dụng lịch quyết định thời điểm đọc, không cam kết push tức thời hoặc đồng bộ hai chiều. Thu hồi không xóa dữ liệu đã cache trên điện thoại; tạo link mới yêu cầu đăng ký link mới trên thiết bị. Link chỉ ở state trang, không lưu local/session storage hoặc cache query chứa token.

Feed trả `text/calendar`, CRLF, escape text và fold UTF-8, cache private/no-store, no-referrer/nosniff. RequestId/Exception logging che token; Serilog giữ framework ASP.NET ở Warning để không in URL ở request-start/action logs. Reverse proxy/hosting/APM phía ngoài cũng phải redact đoạn token và không ghi body trả token; không gửi URL vào analytics. Việc này cần kiểm khi triển khai thực.

Nguồn protocol: [RFC 5545](https://www.rfc-editor.org/rfc/rfc5545.html); hướng dẫn đăng ký [Apple Calendar](https://support.apple.com/en-ca/102301). Không cần credential Google/Apple/OAuth để sử dụng feed chỉ đọc.

## 5. Bằng chứng và giới hạn

Kết quả SQL Server/PostgreSQL/HTTP/browser/mock/config/model được ghi theo lệnh thực tế trong [worklog](worklog.md). NOT VERIFIED: migration và dữ liệu ứng dụng/live, đăng ký/refresh/hủy trên điện thoại Apple/Google thật, cấu hình ingress/APM che URL, endurance và remote CI. Không coi build hay một browser desktop là nghiệm thu production hoặc mọi thiết bị.
