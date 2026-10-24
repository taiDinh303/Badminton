import { useRef, useState } from "react";
import { ArrowUpRight, CheckCircle2, LoaderCircle, Mail, Phone, UserRound, MessageCircle } from "lucide-react";
import { ApiError, request } from "../../../services/http";

export default function ContactForm() {
  const [busy, setBusy] = useState(false);
  const [sent, setSent] = useState(false);
  const [error, setError] = useState("");
  const sending = useRef(false);
  async function submit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (sending.current) return;
    const values = new FormData(event.currentTarget);
    const fullName = String(values.get("fullName") ?? "").trim();
    const phone = String(values.get("phone") ?? "").replace(/[\s.-]/g, "");
    if (fullName.length < 2 || !/^(0\d{9}|\+84\d{9})$/.test(phone)) {
      setError("Vui lòng nhập họ tên và số điện thoại Việt Nam hợp lệ."); return;
    }
    sending.current = true; setBusy(true); setError("");
    try {
      await request("POST", "/contacts", {
        fullName, phone, email: String(values.get("email") ?? "").trim() || null,
        message: String(values.get("message") ?? "").trim(), consent: true,
        website: String(values.get("website") ?? ""),
      });
      setSent(true);
    } catch (err) {
      setError(err instanceof ApiError && err.status === 429 ? "Bạn đã gửi quá nhiều lần. Vui lòng thử lại sau một phút." : "Chưa gửi được yêu cầu. Vui lòng thử lại, thông tin vẫn được giữ trong form.");
    } finally { sending.current = false; setBusy(false); }
  }
  return <section id="lien-he" className="contact-section">
    <div className="contact-copy"><span className="home-eyebrow">LET’S PLAY TOGETHER</span><h2>Để lại lời nhắn.<br /><em>Hẹn bạn trên sân.</em></h2><p>Cần tìm giờ chơi hoặc đặt sân cho nhóm?<br />Để chúng tôi hỗ trợ bạn.</p><div className="contact-perks"><span><Phone size={19} /> Tư vấn đặt sân</span><span><UserRound size={19} /> Không cần tài khoản</span></div><span className="contact-watermark" aria-hidden="true">LET’S<br />PLAY.</span></div>
    {sent ? <div className="contact-form contact-success" role="status"><CheckCircle2 size={48} /><h3>Đã nhận lời nhắn!</h3><p>Thông tin đã được gửi đến đội ngũ quản trị. Cảm ơn bạn đã quan tâm.</p><button className="home-button home-button-dark" onClick={() => setSent(false)}>Gửi lời nhắn khác <ArrowUpRight size={18} /></button></div> :
      <form className="contact-form" onSubmit={submit} aria-label="Liên hệ tư vấn">
        <div className="contact-form-title"><MessageCircle size={23} /><h3>Bạn cần hỗ trợ gì?</h3></div>
        <div className="contact-field-grid"><label><span><UserRound size={15} /> Họ và tên *</span><input name="fullName" autoComplete="name" required minLength={2} maxLength={100} placeholder="Tên của bạn" /></label><label><span><Phone size={15} /> Số điện thoại *</span><input name="phone" type="tel" autoComplete="tel" required maxLength={20} placeholder="09xx xxx xxx" /></label></div>
        <label><span><Mail size={15} /> Email <small>(tùy chọn)</small></span><input name="email" type="email" autoComplete="email" maxLength={150} placeholder="ban@email.com" /></label>
        <label><span><MessageCircle size={15} /> Lời nhắn <small>(tùy chọn)</small></span><textarea name="message" maxLength={1000} rows={3} placeholder="Ví dụ: Nhóm 4 người, muốn đặt sân vào buổi tối…" /></label>
        <div hidden aria-hidden="true"><label>Website<input name="website" tabIndex={-1} autoComplete="off" /></label></div>
        <label className="contact-consent"><input type="checkbox" required name="consent" /><span>Tôi đồng ý để đội ngũ sân liên hệ tư vấn qua thông tin đã cung cấp.</span></label>
        {error && <p className="contact-error" role="alert">{error}</p>}
        <button disabled={busy} className="home-button home-button-dark" type="submit">{busy ? <><LoaderCircle className="animate-spin" size={18} /> Đang gửi…</> : <>Gửi yêu cầu <ArrowUpRight size={19} /></>}</button>
        <p className="contact-note">Yêu cầu tư vấn chưa phải xác nhận đặt sân.</p>
      </form>}
  </section>;
}
