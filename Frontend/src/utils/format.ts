export function vietnamDate(date = new Date()): string {
  return new Intl.DateTimeFormat("en-CA", {
    timeZone: "Asia/Ho_Chi_Minh", year: "numeric", month: "2-digit", day: "2-digit",
  }).format(date);
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

/** Backend slotLabel = "14:00 (02:00-03:00)" — the parenthetical is a
 *  12-hour time that confuses users. Keep just the label portion ("14:00"). */
export function slotLabelClean(label: string | null | undefined): string {
  if (!label) return "—";
  return label.includes("(") ? label.split("(")[0].trim() : label;
}
