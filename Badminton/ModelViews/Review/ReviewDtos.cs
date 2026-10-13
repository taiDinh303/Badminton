namespace ModelViews.Review
{
    public record CreateReviewRequest(int BookingId, int Rating, string? Comment,
        List<string>? Images = null);

    public record ReviewView(int Id, int BookingId, int CourtId, string CourtName,
        string UserName, int Rating, string? Comment, List<string>? Images,
        DateTime CreatedAt, bool FlaggedRefunded);
}
