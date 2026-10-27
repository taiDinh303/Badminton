using Contract.Repositories.Entity;
using Microsoft.EntityFrameworkCore;
namespace Repositories.Context;

internal static class ClubFeaturesModel
{
    public static void Configure(ModelBuilder mb)
    {
        mb.Entity<PlayerSession>(e =>
        {
            e.ToTable("PlayerSessions");
            e.HasIndex(x => new { x.BranchId, x.StartsAtUtc, x.Level });
            e.HasOne<User>().WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Branch>().WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
        });
        mb.Entity<PlayerSessionMember>(e =>
        {
            e.ToTable("PlayerSessionMembers");
            e.HasIndex(x => new { x.SessionId, x.UserId }).IsUnique();
            e.HasOne<PlayerSession>().WithMany().HasForeignKey(x => x.SessionId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        });
        mb.Entity<GroupExpense>(e =>
        {
            e.Property(x => x.SplitMethod).HasMaxLength(20).HasDefaultValue("Equal");
            e.ToTable("GroupExpenses");
            e.HasIndex(x => x.BookingId).IsUnique();
            e.HasOne<Booking>().WithMany().HasForeignKey(x => x.BookingId).OnDelete(DeleteBehavior.Restrict);
        });
        mb.Entity<GroupExpenseShare>(e =>
        {
            e.ToTable("GroupExpenseShares");
            e.HasIndex(x => new { x.ExpenseId, x.UserId }).IsUnique();
            e.HasOne<GroupExpense>().WithMany().HasForeignKey(x => x.ExpenseId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        });
        mb.Entity<EquipmentItem>(e =>
        {
            e.ToTable("EquipmentItems");
            e.HasIndex(x => new { x.BranchId, x.Active });
            e.HasOne<Branch>().WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
        });
        mb.Entity<EquipmentOrder>(e =>
        {
            e.ToTable("EquipmentOrders");
            e.HasIndex(x => new { x.UserId, x.RequestKey }).IsUnique();
            e.HasOne<EquipmentItem>().WithMany().HasForeignKey(x => x.ItemId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        });
        mb.Entity<CoachProfile>(e =>
        {
            e.ToTable("CoachProfiles");
            e.HasOne<Branch>().WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
        });
        mb.Entity<CoachSlot>(e =>
        {
            e.HasOne<Booking>().WithMany().HasForeignKey(x => x.CourtBookingId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => x.CourtBookingId);
            e.ToTable("CoachSlots");
            e.HasIndex(x => new { x.CoachId, x.StartsAtUtc });
            e.HasOne<CoachProfile>().WithMany().HasForeignKey(x => x.CoachId).OnDelete(DeleteBehavior.Restrict);
        });
        mb.Entity<CoachingReservation>(e =>
        {
            e.ToTable("CoachingReservations");
            e.HasIndex(x => new { x.UserId, x.RequestKey }).IsUnique();
            e.HasOne<CoachSlot>().WithMany().HasForeignKey(x => x.SlotId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        });
        mb.Entity<MiniTournament>(e =>
        {
            e.ToTable("MiniTournaments");
            e.HasOne<Branch>().WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.OrganizerId).OnDelete(DeleteBehavior.Restrict);
        });
        mb.Entity<TournamentTeam>(e =>
        {
            e.ToTable("TournamentTeams");
            e.HasIndex(x => new { x.TournamentId, x.CaptainId }).IsUnique();
            e.HasIndex(x => new { x.TournamentId, x.Name }).IsUnique();
            e.HasOne<MiniTournament>().WithMany().HasForeignKey(x => x.TournamentId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.CaptainId).OnDelete(DeleteBehavior.Restrict);
        });
        mb.Entity<TournamentMatch>(e =>
        {
            e.ToTable("TournamentMatches");
            e.HasIndex(x => new { x.TournamentId, x.TeamAId, x.TeamBId }).IsUnique();
            e.HasOne<MiniTournament>().WithMany().HasForeignKey(x => x.TournamentId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<TournamentTeam>().WithMany().HasForeignKey(x => x.TeamAId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<TournamentTeam>().WithMany().HasForeignKey(x => x.TeamBId).OnDelete(DeleteBehavior.Restrict);
        });
        mb.Entity<CalendarSubscription>(e =>
        {
            e.ToTable("CalendarSubscriptions");
            e.HasIndex(x => x.UserId).IsUnique();
            e.HasIndex(x => x.TokenHash).IsUnique();
            e.Property(x => x.TokenHash).HasMaxLength(64);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        });
        mb.Entity<EquipmentConditionReport>(e => {
            e.ToTable("EquipmentConditionReports"); e.HasIndex(x => x.OrderId).IsUnique();
            e.Property(x => x.Reason).HasMaxLength(1000);
            e.HasOne<EquipmentOrder>().WithMany().HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.ActorId).OnDelete(DeleteBehavior.Restrict);
        });
        mb.Entity<TournamentScoreRevision>(e => {
            e.ToTable("TournamentScoreRevisions"); e.HasIndex(x => new { x.MatchId, x.Revision }).IsUnique();
            e.Property(x => x.Reason).HasMaxLength(1000);
            e.HasOne<TournamentMatch>().WithMany().HasForeignKey(x => x.MatchId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.ActorId).OnDelete(DeleteBehavior.Restrict);
        });
        Type[] types = [typeof(PlayerSession), typeof(PlayerSessionMember), typeof(GroupExpense), typeof(GroupExpenseShare), typeof(EquipmentItem), typeof(EquipmentOrder), typeof(CoachProfile), typeof(CoachSlot), typeof(CoachingReservation), typeof(MiniTournament), typeof(TournamentTeam), typeof(TournamentMatch), typeof(CalendarSubscription)];
        foreach (var type in types)
            foreach (var property in mb.Entity(type).Metadata.GetProperties().Where(p => p.ClrType == typeof(string)))
                if (property.GetMaxLength() is null)
                    property.SetMaxLength(property.Name == "RequestKey" ? 60 : property.Name == "Name" ? 120 : 1000);
    }
}
