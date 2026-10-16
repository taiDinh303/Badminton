namespace ModelViews.TimeSlot
{
    public record TimeSlotView(int Id, string Label, string StartTime, string EndTime,
        bool IsPeak, bool IsActive);

    public record CreateTimeSlotRequest(string Label, string StartTime, string EndTime,
        bool IsPeak);

    public record UpdateTimeSlotRequest(bool? IsActive, string? Label);
}
