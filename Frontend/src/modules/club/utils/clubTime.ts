// Club API timestamp fields are UTC. SQL Server's datetime2 can omit a JSON offset.
export function clubInstant(value: string): Date {
  const timestamp = value.trim();
  return new Date(
    /(?:z|[+-]\d{2}:\d{2})$/i.test(timestamp) ? timestamp : `${timestamp}Z`,
  );
}
export function formatClubTime(value: string): string {
  const instant = clubInstant(value);
  return Number.isNaN(instant.getTime())
    ? "—"
    : new Intl.DateTimeFormat("vi-VN", {
        dateStyle: "short",
        timeStyle: "short",
        timeZone: "Asia/Ho_Chi_Minh",
      }).format(instant);
}
