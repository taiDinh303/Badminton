# 19 — Hoàn thiện sáu chức năng CLB theo mốc chất lượng 8/10

> Owner yêu cầu ngày 27/10/2026: khắc phục các điểm yếu trong ảnh đánh giá. CURRENT theo source, kết quả kiểm thử thực tế ở [worklog](worklog.md). “8/10” là mục tiêu chất lượng theo phạm vi dưới đây, không phải chứng nhận production hoặc thiết bị thật.

## 1. CURRENT — những điểm yếu đã được xử lý

| Chức năng | Hành vi mới | Điều kiện kiểm chứng |
| --- | --- | --- |
| Tìm bạn | Tự đóng buổi khi hết giờ, không phụ thuộc chủ buổi. Worker xử lý tối đa100buổi/tick dưới cùng khóa với thao tác người dùng; chạy lại không đổi kết quả. Search/join vẫn chặn lịch đã bắt đầu và cap chỉ đếm buổi chưa kết thúc. | Worker trước/sau giờ kết thúc, chạy lặp; các test capacity/privacy cũ. |
| Chi phí nhóm | Giữ chia đều và thêm chia theo phút tham dự do người đặt ghi nhận. Phút0 cho người vắng, tối đa thời lượng slot; tổng trọng số phải dương. Dùng phần dư lớn nhất để phân bổ VND, UserId chỉ phân xử khi phần dư bằng nhau. Hiển thị dự kiến trước khi chốt, lưu phương pháp và phút cùng snapshot bất biến; thử lại chỉ hợp lệ khi cùng dữ liệu. | Tổng100001đ cho30/60phút là33334/66667đ;0đ,0phút, phút vượt slot, retry khác phương pháp, quyền owner và browser payload. |
| Dụng cụ/cầu | Yêu cầu mới giữ hàng30phút hoặc đến DueAtUtc nếu sớm hơn. Tồn khả dụng và cap20không tính hold đã hết hạn ngay cả khi worker chậm. Server từ chối giao hàng hết hạn; worker đổi Expired và báo inbox một lần. Hàng đã giao quá hạn trả vẫn giữ trong tồn đang thuê, nhắc một lần; không tự trả giả. Staff lập biên bản hư/mất, số không dùng được, lý do, khoản đề nghị đối chiếu; giảm stock đúng một lần, coi phần còn lại đã nhận trả. Biên bản chốt không ghi đè, chỉ owner/staff đúng branch đọc. | Race/replay hư/mất, stock không âm, report riêng tư, giữ stock hàng quá hạn trả, inbox không trùng, không sinh Payments. |
| HLV | Slot chưa gắn sân là bản nháp, Available=0, không nhận đặt. Staff chọn đơn sân Confirmed cùng cơ sở, bao trùm giờ học và đủ sức chứa. Gắn/đặt khóa booking transition trước coach; cùng đơn sân không gắn hai lớp giao nhau. Mỗi lần đọc/đặt kiểm backing booking, closure và refund intent; lịch lệch thì dừng nhận ngay. Worker hủy lớp/chỗ Reserved và thông báo nếu sân mất hiệu lực, không tự thu/hoàn học phí. | Draft reject, attach đúng/sai, capacity/branch, backing booking bị hủy → Available0/reserve409 → compensation/inbox một lần; regression last-seat/overlap. |
| Giải nhỏ | Mỗi lần ghi/sửa điểm thêm TournamentScoreRevisions: phiên bản, điểm trước/sau, người sửa, thời gian, lý do. Sửa phải có lý do và ExpectedRevision đúng; màn hình cũ409. Ghi lại cùng điểm không nhân bản lịch sử. UI hiển thị audit, server tiếp tục tính xếp hạng từ kết quả hiện tại. | Điểm lần đầu, sửa có lý do, stale version, history nguyên vẹn, route role/branch, browser gửi đúng revision. |
| Lịch điện thoại | Trạng thái phân biệt liên kết vừa tạo và đã được đọc; lưu LastFetchedAtUtc, xoay link xóa mốc đọc cũ. UI làm mới trạng thái mỗi30giây, nói rõ lần tải feed không chứng minh đã hiện trên điện thoại. Giữ token hash, revoke, UID/tombstone và che URL trong log ứng dụng. | Tạo → chưa đọc → GETfeed → có timestamp → xoay → chưa đọc; giữ HTTP/MIME/revoke/escape và browser hồi quy. |

Phút tham dự là số do owner ghi nhận, không giả là bằng chứng check-in từng người. Khoản đề nghị đối chiếu hư/mất không phải tiền đã thu; không đặt mức phạt từ chính sách chưa được owner chốt. Lịch HLV dùng **đơn sân cá nhân đã xác nhận do chính staff thao tác đứng tên**, không lấy đơn riêng của khách/nhóm, không tạo booking miễn phí hoặc tự xác nhận thanh toán. Staff cần đặt/xác nhận sân bằng luồng hiện hữu, rồi gắn cho lớp. API tạo slot cũ giữ đường dẫn và payload, nhưng slot chưa gắn không còn nhận khách.

## 2. CURRENT — vận hành và nhất quán

Tác vụ `IClubMaintenanceService` chạy cùng WaitlistPromotionJob mỗi2phút, có job lock/heartbeat và yêu cầu BackgroundJobs bật. Mỗi tick tối đa100buổi,100yêu cầu dụng cụ và100lớp. Lớp được xoay lượt theo LastCourtCheckedAtUtc để lớp hợp lệ ở đầu không che lớp sai ở sau. Chậm/đứt worker không làm hold hết hạn tiếp tục chiếm kho và không cho lớp sân sai nhận thêm khách; cập nhật trạng thái/inbox có thể chậm theo backlog. Không cam kết toàn bộ lớp được đối soát trong2phút nếu số lượng vượt batch.

Các thao tác tài nguyên dùng transaction và khóa hiện hữu; worker không cần identity giả. Giao nhận và biên bản dùng khóa item; nhận học viên và gắn sân dùng cùng coach lock và booking-transition lock. Đơn sân đổi/hủy vẫn tuân chính sách cũ; lớp liên quan dừng nhận khách ngay ở lần đọc/đặt tiếp theo và được bù trạng thái qua worker. Học phí đã thu tại quầy cần nhân viên đối chiếu, không ghi ledger hoàn tiền sân thay cho học phí.

Lịch sử sửa điểm là append-only **qua API/service**, không tuyên bố chống người có quyền SQL quản trị trực tiếp. Kết quả cũ trước migration chưa có người/lý do sửa lịch sử; lần sửa mới lưu lại điểm cũ thay vì bịa audit quá khứ. Phạm vi giải vẫn vòng tròn chia bảng; chưa thêm nhánh loại trực tiếp hoặc xác thực luật thi đấu chính thức.

Form sửa điểm giữ phiên bản đã nạp cùng bản đang nhập. Refetch lịch sử không tự nâng ExpectedRevision của bản nhập cũ; khi có phiên bản mới, giữ nguyên bản nhập nhưng khóa lưu, yêu cầu người dùng nạp kết quả mới trước khi sửa. Timestamp CLB không có hậu tố từ SQL Server được hiểu là UTC, hiển thị Việt Nam độc lập múi giờ trình duyệt.

## 3. CURRENT source — API và migration

Bổ sung, giữ các routes hiện hữu dưới `/api/v1/club`:

| Route | Ý nghĩa |
| --- | --- |
| POST `expenses/{bookingId}/attendance` | `{members:[{userId,minutes}]}` chốt theo thời lượng |
| GET/POST `equipment/orders/{id}/condition` | Đọc/chốt biên bản; POST chỉ staff đúng cơ sở |
| GET `coaches/court-bookings?branchId` | Tối đa200đơn Confirmed tương lai cho staff lựa chọn |
| PUT `coaches/slots/{id}/court` | Body int BookingId; gắn vào lớp chưa bắt đầu/hủy |
| GET `tournaments/{id}/matches/{matchId}/history` | Lịch sử phiên bản công khai cho người đăng nhập |
| PUT `.../score` | Thêm `reason` và `expectedRevision`; bắt buộc khi sửa điểm |
| GET `calendar` | Thêm `lastFetchedAtUtc` và `changedAtUtc`, không trả token |

HardenClubWorkflows: SQL Server `20261027130036`, PostgreSQL `20261027130113`, sau AddClubFeatures. Thêm2bảng biên bản/audit (model49bảng), cột TTL/expiry/nhắc quá hạn, phương pháp/phút chia tiền, liên kết sân và mốc kiểm/đọc lịch. SplitMethod của bảng cũ mặc định Equal; TTL cũ không được đoán lại, dùng DueAtUtc làm hạn nhận cũ; lớp cũ không có sân cần staff gắn trước khi nhận khách. Không sửa dữ liệu tiền/điểm lịch sử. PostgreSQL bật RLS/revoke bảng và sequence cho2bảng mới.

Down chặn khi có audit, chia theo phút, liên kết sân hoặc TTL/expiry mới; không lặng lẽ xóa bằng chứng/quy tắc giữ hàng. Review/backup/apply migration là việc phát hành riêng, không auto-migrate Production. Không chạy trên DB ứng dụng/live trong nhiệm vụ này.

Khi chuẩn bị nâng cấp dữ liệu cũ, phải rà soát các lớp còn học viên nhưng chưa có sân: worker mới sẽ hủy các chỗ Reserved của lớp không có backing booking hợp lệ và gửi inbox. Cần gắn sân qua quy trình staff trước khi bật worker hoặc chủ động chấp nhận quy trình bù này; không xem migration tự thêm cột là đã hoàn tất bàn giao các lớp lịch sử.

## 4. TARGET phát hành / NOT VERIFIED

Mốc chất lượng local đòi hỏi các luồng trên có API/UI và test logic, SQL Server/PostgreSQL, HTTP permissions và browser; lệnh/kết quả thật trong worklog. Build xanh đơn lẻ không đủ đóng việc.

Chưa nghiệm thu ingress/APM thật che token, đăng ký/refresh trên Apple/Google thật, endurance và backup/restore schema mới. ICS đăng ký vẫn đọc định kỳ theo client, không push tức thời hoặc hai chiều; revoke chặn lần đọc tiếp, không thể thu hồi bản đã cache trên thiết bị. Muốn đổi các giới hạn đó cần một tích hợp OAuth/provider và nhiệm vụ triển khai/thiết bị riêng, không giả kết quả bằng chỉnh mô tả hay chấm điểm cao hơn.
