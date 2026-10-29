# 17 — Tiện ích đặt sân và chất lượng thiết kế

Phạm vi owner giao 26/10/2026: triển khai toàn bộ 23 mục bổ sung ngoài phần bảo mật của review, giữ gateway/thanh toán ngoài phạm vi; cải thiện OOP/SOLID/patterns/Clean Code. Các thay đổi source đang được kiểm chứng, không đồng nghĩa triển khai live.

## Quyết định nghiệp vụ

- Owner chốt: đổi lịch cùng giá gốc, chưa check-in; không thay tiền/điểm/voucher của đơn và không kéo dài TTL. Đơn phải chưa bắt đầu, lịch mới còn hợp lệ theo giới hạn 30 ngày và quota hiện có.
- Nhiều slot/định kỳ: mỗi slot một đơn; toàn bộ lịch cùng transaction, không tạo một phần. Các slot liên tiếp, tối đa 4 mỗi ngày, 1–5 tuần trong cửa sổ 30 ngày; quota nhóm/cá nhân cũ vẫn được server kiểm.
- Đặt hộ chỉ cho nhân viên đúng cơ sở và tài khoản khách hoạt động/đã xác minh. Không tạo mật khẩu hoặc giả xác minh khách mới ở quầy; khách đăng ký bằng luồng hiện có.
- Chấp nhận khiếu nại no-show yêu cầu nhân viên xác minh khách thực sự chơi và slot đã kết thúc; sửa trạng thái/điểm/counter có audit. Phản ánh check-in khác ghi kết quả xác minh, không tự đảo dữ liệu tiền hoặc lịch sử đã chốt.
- Yêu cầu sửa/xóa dữ liệu là workflow xác minh và ghi kết quả; không tự xóa ledger/booking, không suy ra chính sách lưu giữ pháp lý.
- Email/SMS/Zalo chỉ qua relay đã cấu hình; không mua dịch vụ, không gọi khách thật khi kiểm thử. In-app có trạng thái đọc riêng với trạng thái relay.
- Upload PNG/JPEG tối đa 2 MB, quota 100 ảnh/tài khoản và 6 ảnh/review; avatar công khai chỉ sau thao tác chọn avatar. Production cần thư mục private bền vững qua Media:Directory; không coi filesystem ephemeral là cloud storage đã nghiệm thu.

## Ma trận phạm vi

CURRENT: cả 23 mục đã có source/API và điểm vào UI. Đối chiếu bằng chứng local và giới hạn trong [worklog](worklog.md); không đồng nhất với đã phát hành production.

| ID | Mục được giao | Điểm vào |
| --- | --- | --- |
| EXP01 | Đổi lịch/đổi sân cùng giá | Chi tiết đơn → Tiện ích |
| EXP02 | Nhân viên đặt hộ/walk-in | Vận hành → Lịch ngày/tuần |
| EXP03 | Lịch vận hành ngày/tuần | Vận hành → Lịch |
| EXP04 | Khiếu nại hiện diện/no-show | Chi tiết đơn; Vận hành → Phản ánh |
| EXP05 | Xử lý đơn ảnh hưởng bảo trì | Vận hành → Sự cố/liên hệ |
| EXP06 | Trung tâm thông báo, chưa đọc | Chuông và /notifications |
| EXP07 | Nhắc lịch tùy chọn | Thông báo → Tùy chọn |
| EXP08 | Theo dõi/gửi lại thông báo lỗi | Vận hành → Trạng thái gửi |
| EXP09 | Thông báo khẩn theo cơ sở/sân/ngày | Vận hành → Sự cố |
| EXP10 | Upload ảnh hồ sơ/sân/review | Ảnh của tôi; tiện ích đơn/review |
| EXP11 | Báo cáo/kiểm duyệt review | Review; Vận hành → Kiểm duyệt |
| EXP12 | Phản hồi chính thức của cơ sở | Review → Phản hồi |
| EXP13 | Timeline cho khách | Chi tiết đơn → Tiện ích |
| EXP14 | Xuất CSV dùng trong Excel | Vận hành → Báo cáo |
| EXP15 | Yêu cầu dữ liệu cá nhân | Tài khoản → Yêu cầu dữ liệu |
| EXP16 | Ca trực/bàn giao | Vận hành → Ca trực |
| EXP17 | RSVP theo đơn nhóm | Buổi chơi nhóm |
| EXP18 | Đặt định kỳ | /planner |
| EXP19 | Nhiều slot liên tiếp | /planner |
| EXP20 | Sân/giờ thay thế | Drawer slot kín → Gợi ý |
| EXP21 | Lịch điện thoại ICS | Chi tiết đơn → Tiện ích |
| EXP22 | Sân yêu thích | Tài khoản → Sân yêu thích |
| EXP23 | Khách quay lại/no-show/hàng chờ | Vận hành → Báo cáo |

## Tiêu chí đánh giá thiết kế — TARGET 9/10

Điểm là đánh giá kỹ thuật, không chứng nhận. Chỉ chấm sau review và kiểm thử theo các tiêu chí:

1. Contracts/Core độc lập EF/HTTP; persistence và lưu file có adapter, DI tại composition root.
2. Mỗi use case có interface hẹp và service riêng, controller chỉ map HTTP; không dồn 23 chức năng vào một service/page.
3. Trạng thái yêu cầu có transition được kiểm soát; business clock thay được trong test; authorization dùng chung nhưng kiểm server từng thao tác.
4. Coordinator đặt hàng loạt tái dùng luồng tạo đơn hiện hữu; idempotency cho cả manifest, khóa có thứ tự, rollback toàn bộ.
5. Projection rating dùng chung cho tạo/ẩn/khôi phục review, lấy source rows; audit cùng transaction khi có thay đổi.
6. Frontend tách transport/contracts, hooks, component/page theo use case; lỗi/thời gian chờ thể hiện rõ, cache bị xóa khi đổi danh tính.
7. Kiểm thử tình huống sai quyền, cạnh tranh, replay, rollback, deadline, CSV injection, tệp không hợp lệ, retry và cả hai provider.

Không thêm inheritance/factory/event bus chỉ để tăng số lượng pattern. Patterns áp dụng: ports/adapters, repository/unit of work, coordinator, projection và strategy qua các kênh thông báo hiện hữu.

## NOT VERIFIED

Chưa áp schema/data vào ứng dụng/live; chưa gọi relay thật; chưa nghiệm thu lưu file production, camera/USB, tải dài, remote CI hoặc thiết bị thực. Các chức năng bảo mật ở bảng riêng của review không nằm trong phạm vi EXP01–EXP23.

Kiểm tra bản cuối local: Chromium79/79, WebKit30/30; luồng browser gọi API/DB thật1/1 mỗi provider; native backup/restore và18process mỗi provider đạt. Firefox trong suite tổng hợp còn3 lỗi teardown context trên Windows (ca lỗi thay đổi giữa các lượt, isolated planner có trace đạt); chưa kết luận root cause hoặc coi suite Firefox ổn định. Chi tiết full backend260ca/provider và3ca kỳ vọng cuối tuần đã sửa/rerun trong worklog.

## Review thiết kế CURRENT

Nhận định kỹ thuật source sau refactor: **khoảng 9/10 theo rubric trên**, không phải chứng nhận, điểm bảo mật hay điểm sẵn sàng production. Cơ sở để đánh giá:

| Tiêu chí | Thay đổi cụ thể | Giới hạn còn giữ rõ |
| --- | --- | --- |
| OOP | BookingCase/PrivacyRequest quản lý transition; DTO/use case tách riêng; clock và media có port | Entity legacy vẫn có public setters; không đổi toàn bộ thành aggregate chỉ để tăng điểm |
| SOLID | Interface hẹp theo use case; favorite/RSVP tách service; InventoryHistoryService inject qua contract vào service/job | Các thành phần tạo/availability nội bộ vẫn tái dùng service sẵn có, không thêm wrapper vô nghĩa |
| Patterns | Ports/adapters, repository/UoW, batch coordinator/idempotency, shared rating projection, strategy notification hiện hữu | Không dựng event bus/outbox tổng quát khi chưa có yêu cầu vận hành/crash acceptance |
| Clean Code | Feature pages/hooks/contracts riêng; SQL read port cho join/paging; cập nhật đúng cột; guards/input limits tập trung; format source mới | Đây là review source có chủ đích, không cam kết không còn code smell ở mọi file legacy |
| Kiểm chứng | Sai quyền/branch, race/replay, giá thay đổi/rollback, migration/backfill/RLS, HTTP multipart, UI hành vi và session | Test local không thay tải dài, filesystem backup, relay thật hoặc nghiệm thu người dùng |

CSV giới hạn 10.000 dòng và khoảng một năm; calendar tối đa 32 ngày. Insights đếm khách theo đơn trong kỳ/phạm vi: khách quay lại có đơn trước ngày bắt đầu tại cùng phạm vi; tỷ lệ no-show lấy số đơn NoShow chia toàn bộ đơn không soft-delete trong kỳ (kể cả đơn hủy). Hàng chờ là tổng entry trong kỳ, promoted là tập con. Đây là chỉ số vận hành có định nghĩa, không tự diễn giải thành doanh thu hay tỷ lệ khách duy nhất đã đến sân.

ICS là bản xuất lịch tại thời điểm tải, không phải calendar subscription tự đồng bộ. Khi đổi lịch, khách cần xuất lại; ứng dụng giữ nguồn lịch hiện hành. Gợi ý tối đa 12 lựa chọn cùng cơ sở, ưu tiên gần giờ mong muốn, giá gồm phụ thu cuối tuần; tạo đơn vẫn kiểm tra lại.

## Hoàn thiện Services/Contracts Operations — 26/10/2026

CURRENT source: 22 interface trong Contract.Services/Operations, mỗi interface một file đúng tên, giữ namespace/chữ ký HTTP hiện hữu. IMediaStore là port do API cung cấp; IDurableNotificationQueue dùng NotifyService hiện hữu. BookingExperienceRegistration nối các use case với implementation, có kiểm tra resolve toàn bộ đồ thị Operations thay vì chỉ kiểm build.

BookingTransitionGuard dùng khóa transaction chung cho đổi lịch, refund intent/finalize, check-in, no-show, completion và xử lý phản ánh. Refund tại quầy đọc lại booking sau khóa, kiểm lại quyền cơ sở và conditional claim có expectedBranchId; không dựa vào booking đã track từ trước. Durable refund chưa Succeeded và không phải hoàn khoản thu late/additional sẽ chặn đổi lịch/hiện diện/hoàn thành; worker bỏ qua đơn đó để tiếp tục các đơn khác. Typed repository commands cũng từ chối các transition này khi còn intent chưa xác định. Khoản thu dư/muộn cần hoàn riêng không khóa quyền sử dụng đơn hợp lệ.

Giá gợi ý lấy giá availability đã gồm phụ thu đúng một lần; không nhân phụ thu cuối tuần lần hai. Đặt hộ kiểm đúng tài khoản Customer đang hoạt động/đã xác minh, từ chối tài khoản nhân viên kể cả client gửi ID trực tiếp. Không thêm migration, thay chính sách tiền/điểm/voucher hoặc bật online refund. Bằng chứng SQL/PostgreSQL/HTTP/browser của lượt hoàn thiện được ghi riêng trong [worklog](worklog.md); nghiệm thu provider và production vẫn NOT VERIFIED.

CURRENT sửa hai luồng nối 26/10: bảo trì mang snapshot phạm vi và kiểm lại dưới transition/court-date locks, bỏ qua work item đã lỗi thời trước khi ghi hủy/hoàn hoặc online intent. BookingCancellationAuthorization thống nhất quyền hủy đơn nhóm giữa booking/payment/online refund; owner hiện tại được kiểm dưới group lock, tài khoản/điểm/voucher nhận hoàn vẫn là người đặt ban đầu. UI chi tiết hiển thị nút hủy theo group hiện do user sở hữu, không mời chủ mới khởi tạo checkout hay đọc receipt của người khác. Không tự sửa dữ liệu hoàn tiền lịch sử hoặc áp migration/live.

CURRENT sửa tiếp hai P2 ngày/hàng chờ 26/10: form đổi lịch chuẩn hóa ngày booking `dd/MM/yyyy` hoặc ISO sang `yyyy-MM-dd` cho input; giữ đúng calendar date, không đổi múi giờ hoặc tự cuộn ngày không hợp lệ. Browser kiểm trường hợp chỉ đổi giờ, không chọn lại ngày, cả mock và API/DB thật. Waitlist join/promotion dùng cùng quy tắc interval overlap với availability/create cho các slot legacy khác ID; các slot sát nhau vẫn trống và các trạng thái terminal không chặn chỗ. Regression kiểm biên, trạng thái và chuỗi Waiting → giải phóng → Pending/Promoted đúng một lần. Các HTTP model, dữ liệu ứng dụng và migrations giữ nguyên.
