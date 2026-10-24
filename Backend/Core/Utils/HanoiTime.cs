namespace Badminton.Core.Utils
{
    public static class HanoiTime
    {
        public static readonly TimeSpan FixedOffset = TimeSpan.FromHours(7);
        public static Func<DateTime> NowUtc { get; set; } = () => DateTime.UtcNow;
        public static DateTime HanoiNow() => NowUtc().ToUniversalTime().Add(FixedOffset);
        public static DateOnly HanoiToday() => DateOnly.FromDateTime(HanoiNow().Date);
        public static DateTime SlotStart(DateOnly date, TimeOnly start) => date.ToDateTime(start);
        public static DateTime SlotEnd(DateOnly date, TimeOnly end) => date.ToDateTime(end);
        public static DateOnly HanoiDateOf(DateTime utc) =>
            DateOnly.FromDateTime((utc.Kind == DateTimeKind.Unspecified
                ? DateTime.SpecifyKind(utc, DateTimeKind.Utc) : utc.ToUniversalTime()).Add(FixedOffset));
        public static DateOnly MonthStart(this DateOnly d) => new(d.Year, d.Month, 1);
    }
}
