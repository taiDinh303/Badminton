import { Link } from "react-router-dom";
import { ArrowRight, Users, ShieldCheck } from "lucide-react";
import FeatureHeading from "../../../components/FeatureHeading";
import { features } from "../components/ClubUi";
import { isStaff, useAuth } from "../../../store/auth";

const descriptions: Record<string, string> = {
  "/players": "Lọc buổi chơi theo trình độ, khu vực và giờ rảnh. Gửi yêu cầu tham gia hoặc mở buổi của bạn.",
  "/group-expenses": "Chia tiền sân đều hoặc theo phút tham dự. Xem phần của từng thành viên và ai đã đóng.",
  "/equipment": "Xem dụng cụ còn sẵn, đặt thuê vợt hoặc mua cầu và theo dõi việc nhận, trả tại cơ sở.",
  "/coaches": "Xem huấn luyện viên và giờ học còn chỗ. Đặt buổi cá nhân hoặc lớp nhóm phù hợp.",
  "/tournaments": "Đăng ký đội, theo dõi bảng đấu, lịch thi đấu, kết quả và xếp hạng của giải.",
  "/calendar-sync": "Đăng ký lịch sân vào điện thoại để ứng dụng lịch tự lấy giờ mới và trạng thái hủy.",
};

export default function ClubPage() {
  const user = useAuth((state) => state.user);
  return (
    <div className="club-shell">
      <div className="club-hero">
        <span className="club-eyebrow">Kết nối · Luyện tập · Thi đấu</span>
        <FeatureHeading icon={Users} title="Câu lạc bộ" subtitle="Mọi tiện ích cho buổi cầu lông của bạn, từ tìm đồng đội đến theo dõi lịch chơi." />
      </div>
      <nav className="club-feature-grid" aria-label="Khám phá câu lạc bộ">
        {features.map(({ path, label, icon: Icon, hint }) => (
          <Link key={path} to={path} className="club-feature-card">
            <span className="club-feature-icon"><Icon size={26} aria-hidden="true" /></span>
            <div className="club-feature-copy">
              <h2>{label}</h2>
              <span className="club-feature-hint">{hint}</span>
              <p>{descriptions[path]}</p>
            </div>
            <span className="club-feature-open">Khám phá <ArrowRight size={17} aria-hidden="true" /></span>
          </Link>
        ))}
      </nav>
      <aside className="club-overview-note">
        <ShieldCheck size={22} aria-hidden="true" />
        <p>Liên kết lịch là riêng tư. Ứng dụng lịch quyết định thời điểm cập nhật; thay đổi giờ và hủy đơn có thể xuất hiện chậm.</p>
      </aside>
      {isStaff(user) && <section className="club-staff-shortcuts" aria-label="Quản lý câu lạc bộ">
        <h2>Quản lý tại cơ sở</h2>
        <div className="club-tags">
          <Link to="/admin/club/equipment" className="club-tag">Kho & giao trả dụng cụ <ArrowRight size={15} aria-hidden="true" /></Link>
          <Link to="/admin/club/coaches" className="club-tag">HLV & lịch học <ArrowRight size={15} aria-hidden="true" /></Link>
          <Link to="/admin/club/tournaments" className="club-tag">Tổ chức giải <ArrowRight size={15} aria-hidden="true" /></Link>
        </div>
      </section>}
    </div>
  );
}
