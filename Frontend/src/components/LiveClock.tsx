import "./session.css";
import { useEffect, useState } from "react";
import { Clock3 } from "lucide-react";

const clock = new Intl.DateTimeFormat("vi-VN", { timeZone: "Asia/Ho_Chi_Minh", hour: "2-digit", minute: "2-digit", second: "2-digit", hour12: false });
const calendar = new Intl.DateTimeFormat("vi-VN", { timeZone: "Asia/Ho_Chi_Minh", weekday: "long", day: "2-digit", month: "2-digit", year: "numeric" });

export default function LiveClock() {
  const [now, setNow] = useState(() => new Date());
  useEffect(() => {
    const timer = window.setInterval(() => setNow(new Date()), 1000);
    return () => window.clearInterval(timer);
  }, []);
  return <div className="live-clock">
    <Clock3 size={17} aria-hidden="true" />
    <div><time dateTime={now.toISOString()} aria-label="Giờ Việt Nam" className="tnum">{clock.format(now)}</time><p>{calendar.format(now)} · UTC+7</p></div>
  </div>;
}
