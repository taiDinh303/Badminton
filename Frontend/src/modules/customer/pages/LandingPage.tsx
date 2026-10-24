import { Link } from "react-router-dom";
import { ArrowUpRight, ArrowRight, Feather, CalendarDays, MapPin, QrCode, Users, Ticket, Check } from "lucide-react";
import { useAuth } from "../../../store/auth";
import AccountMenu from "../../../components/AccountMenu";
import ContactForm from "../components/ContactForm";
import LiveClock from "../../../components/LiveClock";
import "../styles/home.css";

const steps = [
  { icon: MapPin, title: "Chọn sân", text: "Tìm cơ sở và sân phù hợp." },
  { icon: CalendarDays, title: "Chọn giờ", text: "Xem lịch trống, chọn khung giờ." },
  { icon: QrCode, title: "Ra sân", text: "Theo dõi đơn và mã check-in." },
];

export default function LandingPage() {
  const user = useAuth(s => s.user);
  return <div className="public-home">
    <a href="#home-content" className="sr-only focus:not-sr-only">Bỏ qua điều hướng</a>
    <header className="public-header"><div className="public-header-inner">
      <Link to="/home" className="home-brand" aria-label="Cầu Lông Booking - Trang chủ"><span><Feather size={24} /></span><strong>Cầu Lông<small>BOOKING</small></strong></Link>
      <nav aria-label="Điều hướng trang chủ" className="public-links"><a href="#trai-nghiem">Trải nghiệm</a><a href="#cach-dat">Cách đặt sân</a><a href="#lien-he">Liên hệ</a></nav>
      <div className="public-actions">{user ? <><Link className="home-button home-button-dark compact" to="/dashboard">Vào đặt sân <ArrowUpRight size={17} /></Link><AccountMenu /></> : <><Link className="login-link" to="/auth">Đăng nhập</Link><Link className="home-button home-button-dark compact" to="/auth?mode=register">Đăng ký <ArrowUpRight size={17} /></Link></>}</div>
    </div></header>
    <main id="home-content">
      <section className="landing-hero">
        <div className="hero-copy"><span className="home-eyebrow"><span /> HẸN NHAU TRÊN SÂN</span>
          <h1>Bớt chờ đợi.<br />Thêm <em>trận hay.</em></h1>
          <p>Chọn sân, chọn giờ. Còn lại là khoảnh khắc cùng đồng đội.</p>
          <div className="hero-actions"><Link className="home-button home-button-yellow" to="/book">Đặt sân ngay <ArrowUpRight size={21} /></Link><a className="hero-secondary" href="#lien-he">Nhận tư vấn <ArrowRight size={17} /></a></div>
          <div className="hero-promises"><span><Check size={15} /> Lịch sân trực quan</span><span><Check size={15} /> Quản lý đơn dễ dàng</span></div>
        </div>
        <div className="court-scene" aria-hidden="true"><span className="court-orbit" /><div className="court-board"><div className="court-lines"><i /><i /><i /><i /><i /></div><span className="court-net" /><span className="court-shuttle"><Feather size={45} /></span></div><div className="court-label"><span className="status-dot" /> Sẵn sàng cho trận tiếp theo <ArrowUpRight size={18} /></div><span className="court-caption">YOUR COURT. YOUR TIME.</span></div>
      </section>
      <section id="trai-nghiem" className="experience-strip" aria-label="Tiện ích"><div><CalendarDays /><span>Lịch sân<strong>Chọn giờ chủ động</strong></span></div><div><Users /><span>Đồng đội<strong>Hẹn nhóm cùng chơi</strong></span></div><div><Ticket /><span>Ưu đãi<strong>Voucher & điểm</strong></span></div><div><QrCode /><span>Tại sân<strong>Mã check-in tiện lợi</strong></span></div></section>
      <section id="cach-dat" className="landing-steps"><div className="steps-heading"><span className="home-eyebrow">CHẠM NHẸ. RA SÂN.</span><h2>Một lịch hẹn, ba bước.</h2><p>Dành thời gian cho trận đấu bạn mong chờ.</p></div><div className="step-grid">{steps.map((step, index) => <article key={step.title}><div className="step-top"><step.icon size={25} /><span>0{index + 1}</span></div><h3>{step.title}</h3><p>{step.text}</p></article>)}</div></section>
      <ContactForm /><section className="landing-invite"><div><span className="home-eyebrow">ĐẾN LƯỢT BẠN</span><h2>Hôm nay, mình chơi chứ?</h2></div><Link className="home-button home-button-yellow" to="/book">Tìm giờ chơi <ArrowUpRight size={20} /></Link></section>
    </main>
    <footer className="public-footer"><p><Feather size={18} /> Cầu Lông Booking</p><LiveClock /><Link to={user ? "/dashboard" : "/auth"}>{user ? "Trang chính" : "Đăng nhập để đặt sân"} <ArrowRight size={15} /></Link></footer>
  </div>;
}
