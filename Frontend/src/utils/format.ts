export function vietnamDate(date = new Date()): string {
  return new Intl.DateTimeFormat("en-CA", {
    timeZone: "Asia/Ho_Chi_Minh", year: "numeric", month: "2-digit", day: "2-digit",
  }).format(date);
}

/** A booking calendar date for an HTML date input; never reinterpret it in local time. */
export function dateInputValue(date: string): string {
  const value = date.trim();
  const displayDate = /^(\d{1,2})\/(\d{1,2})\/(\d{4})$/.exec(value);
  const iso = displayDate
    ? `${displayDate[3]}-${displayDate[2].padStart(2, "0")}-${displayDate[1].padStart(2, "0")}`
    : value;
  if (!/^\d{4}-\d{2}-\d{2}$/.test(iso) || iso.startsWith("0000-")) return "";
  const parsed = new Date(`${iso}T00:00:00Z`);
  return !Number.isNaN(parsed.getTime()) && parsed.toISOString().slice(0, 10) === iso ? iso : "";
}

export function formatVnd(n: number): string {
  return `${Math.round(n).toLocaleString("vi-VN")}đ`;
}

export function formatDay(date: string): string {
  if (!date) return "—";
  const value = String(date).trim();
  if (/^\d{1,2}\/\d{1,2}\/\d{2,4}$/.test(value)) return value;
  const dateOnly = /^(\d{4})-(\d{2})-(\d{2})$/.exec(value);
  if (dateOnly) return `${dateOnly[3]}/${dateOnly[2]}/${dateOnly[1]}`;
  const parsed = new Date(value);
  return Number.isNaN(parsed.getTime()) ? value : new Intl.DateTimeFormat("vi-VN", {
    timeZone: "Asia/Ho_Chi_Minh", day: "2-digit", month: "2-digit", year: "numeric",
  }).format(parsed);
}

export function slotLabelClean(label: string | null | undefined): string {
  if (!label) return "—";
  return label.includes("(") ? label.split("(")[0].trim() : label;
}
