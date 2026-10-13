namespace ModelViews.TimeSlot
{
    // ---------- Time slot ----------
    public record TimeSlotView(int Id, TimeOnly StartTime, TimeOnly EndTime, string Label);

    public record CreateTimeSlotRequest(TimeOnly StartTime, TimeOnly EndTime);

    public record UpdateTimeSlotRequest(TimeOnly? StartTime, TimeOnly? EndTime, bool? IsActive);
}
