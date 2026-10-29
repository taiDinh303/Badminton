using System.Security.Cryptography;
using System.Text;
using Badminton.Core.Base;
using Badminton.Core.Enums;
using Contract.Repositories.Entity;
using Services.Operations;
using Xunit;

namespace Badminton.Tests;

public sealed class ClubPriorityLogicTests
{
    private static ClubMaintenanceService Worker(ClubLogicFixture f) => new(f, f, f.Inbox, f.Courts);

    [Theory]
    [InlineData(-1, true)]
    [InlineData(0, true)]
    [InlineData(1, false)]
    public async Task Pickup_deadline_expires_at_exact_boundary_and_does_not_mark_returned(int ticks, bool expired)
    {
        var f = new ClubLogicFixture();
        var row = f.Seed(new EquipmentOrder { ItemId = 4, UserId = 23, Status = "Reserved", Rental = true, DueAtUtc = f.UtcNow.AddDays(1), PickupExpiresAtUtc = f.UtcNow.AddTicks(ticks) });
        Assert.Equal(expired ? 1 : 0, await Worker(f).RunAsync());
        Assert.Equal(expired ? "Expired" : "Reserved", row.Status);
        Assert.Equal(expired ? f.UtcNow : (DateTime?)null, row.ExpiredAtUtc);
        Assert.Null(row.ReturnedAtUtc);
        Assert.Equal(expired ? 1 : 0, f.Messages.Count);
        if (expired) Assert.Equal((23, $"equipment-expired:{row.Id}"), Assert.Single(f.Messages));
        Assert.Equal(0, await Worker(f).RunAsync());
        Assert.Equal(expired ? 1 : 0, f.Messages.Count);
    }

    [Fact]
    public async Task Legacy_order_without_pickup_deadline_uses_due_time()
    {
        var f = new ClubLogicFixture();
        var due = f.Seed(new EquipmentOrder { Status = "Reserved", DueAtUtc = f.UtcNow });
        var future = f.Seed(new EquipmentOrder { Status = "Reserved", DueAtUtc = f.UtcNow.AddTicks(1) });
        Assert.Equal(1, await Worker(f).RunAsync());
        Assert.Equal("Expired", due.Status);
        Assert.Equal("Reserved", future.Status);
    }

    [Theory]
    [InlineData("Cancelled")]
    [InlineData("Returned")]
    [InlineData("Sold")]
    [InlineData("Lost")]
    [InlineData("Damaged")]
    public async Task Final_order_states_are_never_expired_or_notified(string status)
    {
        var f = new ClubLogicFixture();
        var row = f.Seed(new EquipmentOrder { Status = status, Rental = true, DueAtUtc = f.UtcNow.AddDays(-1), PickupExpiresAtUtc = f.UtcNow.AddDays(-2) });
        Assert.Equal(0, await Worker(f).RunAsync());
        Assert.Equal(status, row.Status);
        Assert.Null(row.ExpiredAtUtc);
        Assert.Empty(f.Messages);
        Assert.Equal(0, f.Saves);
    }

    [Theory]
    [InlineData("Issued")]
    [InlineData("Cancelled")]
    public async Task Worker_rechecks_state_after_lock_before_expiring_candidate(string newerStatus)
    {
        var f = new ClubLogicFixture();
        var row = f.Seed(new EquipmentOrder { ItemId = 9, Status = "Reserved", Rental = true, DueAtUtc = f.UtcNow.AddHours(1), PickupExpiresAtUtc = f.UtcNow });
        f.AfterLock = key => { if (key == "equipment:9") row.Status = newerStatus; };
        Assert.Equal(0, await Worker(f).RunAsync());
        Assert.Contains("equipment:9", f.Locks);
        Assert.Equal(newerStatus, row.Status);
        Assert.Null(row.ExpiredAtUtc);
        Assert.Empty(f.Messages);
    }

    [Fact]
    public async Task Overdue_issued_rental_remains_issued_and_notifies_only_once()
    {
        var f = new ClubLogicFixture();
        var row = f.Seed(new EquipmentOrder { UserId = 42, Status = "Issued", Rental = true, DueAtUtc = f.UtcNow });
        await Worker(f).RunAsync();
        await Worker(f).RunAsync();
        Assert.Equal("Issued", row.Status);
        Assert.Null(row.ReturnedAtUtc);
        Assert.Null(row.ExpiredAtUtc);
        Assert.Equal(f.UtcNow, row.OverdueNotifiedAtUtc);
        Assert.Equal((42, $"equipment-overdue:{row.Id}"), Assert.Single(f.Messages));
    }

    [Fact]
    public async Task Worker_drains_backlog_over_multiple_ticks_without_skipping_orders()
    {
        var f = new ClubLogicFixture { ReconciledChanges = 2 };
        for (var i = 0; i < 101; i++) f.Seed(new EquipmentOrder { Status = "Reserved", DueAtUtc = f.UtcNow.AddMinutes(-i) });
        Assert.Equal(102, await Worker(f).RunAsync());
        Assert.Equal(100, f.Repo<EquipmentOrder>().Rows.Count(x => x.Status == "Expired"));
        Assert.Equal(3, await Worker(f).RunAsync());
        Assert.Equal(101, f.Repo<EquipmentOrder>().Rows.Count(x => x.Status == "Expired"));
        Assert.Equal(101, f.Messages.Select(x => x.EventKey).Distinct().Count());
        Assert.Equal(2, f.Reconciliations);
    }

    private static (ClubLogicFixture F, TournamentService Service, TournamentMatch Match, MiniTournament Tournament) Scoring()
    {
        var f = new ClubLogicFixture();
        var tournament = f.Seed(new MiniTournament { BranchId = 3, Status = "Scheduled" });
        var match = f.Seed(new TournamentMatch { TournamentId = tournament.Id, EndsAtUtc = f.UtcNow });
        return (f, new(f, f.Access, f), match, tournament);
    }

    [Theory]
    [InlineData(-1, 10)]
    [InlineData(100, 10)]
    [InlineData(10, -1)]
    [InlineData(10, 100)]
    [InlineData(21, 21)]
    public async Task Invalid_scores_leave_match_and_ledger_unchanged(int a, int b)
    {
        var (f, service, match, tournament) = Scoring();
        await Assert.ThrowsAsync<BadRequestException>(() => service.ScoreAsync(tournament.Id, match.Id, new(a, b)));
        Assert.Null(match.ScoreA);
        Assert.Null(match.ScoreB);
        Assert.Empty(f.Repo<TournamentScoreRevision>().Rows);
        Assert.Equal("Scheduled", tournament.Status);
        Assert.Equal(0, f.Saves);
    }

    [Fact]
    public async Task Score_is_rejected_one_tick_before_end_and_allowed_at_exact_end()
    {
        var (f, service, match, tournament) = Scoring();
        match.EndsAtUtc = f.UtcNow.AddTicks(1);
        await Assert.ThrowsAsync<ConflictException>(() => service.ScoreAsync(tournament.Id, match.Id, new(21, 19)));
        Assert.Empty(f.Repo<TournamentScoreRevision>().Rows);
        f.UtcNow = match.EndsAtUtc.Value;
        await service.ScoreAsync(tournament.Id, match.Id, new(21, 19));
        var audit = Assert.Single(f.Repo<TournamentScoreRevision>().Rows);
        Assert.Equal(f.UtcNow, audit.CreatedAt);
        Assert.Equal(f.Actor.Id, audit.ActorId);
        Assert.Equal(1, audit.Revision);
        Assert.Null(audit.OldScoreA);
        Assert.Equal((21, 19), (audit.ScoreA, audit.ScoreB));
        Assert.Equal("Completed", tournament.Status);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(2)]
    public async Task Correction_requires_current_revision_without_overwriting_original(int? expected)
    {
        var (f, service, match, tournament) = Scoring();
        await service.ScoreAsync(tournament.Id, match.Id, new(21, 19));
        await Assert.ThrowsAsync<ConflictException>(() => service.ScoreAsync(tournament.Id, match.Id, new(19, 21, "Sửa theo biên bản", expected)));
        Assert.Equal((21, 19), (match.ScoreA, match.ScoreB));
        Assert.Equal(1, Assert.Single(f.Repo<TournamentScoreRevision>().Rows).Revision);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    [InlineData("Không\nhợp lệ")]
    public async Task Correction_requires_nonblank_safe_reason(string? reason)
    {
        var (f, service, match, tournament) = Scoring();
        await service.ScoreAsync(tournament.Id, match.Id, new(21, 19));
        await Assert.ThrowsAsync<BadRequestException>(() => service.ScoreAsync(tournament.Id, match.Id, new(19, 21, reason, 1)));
        Assert.Equal((21, 19), (match.ScoreA, match.ScoreB));
        Assert.Single(f.Repo<TournamentScoreRevision>().Rows);
    }

    [Fact]
    public async Task Correction_trims_reason_before_recording_audit()
    {
        var (f, service, match, tournament) = Scoring();
        await service.ScoreAsync(tournament.Id, match.Id, new(21, 19));
        await service.ScoreAsync(tournament.Id, match.Id, new(19, 21, "  Biên bản trọng tài\n", 1));
        Assert.Equal("Biên bản trọng tài", f.Repo<TournamentScoreRevision>().Rows.Single(x => x.Revision == 2).Reason);
    }

    [Fact]
    public async Task Correction_appends_before_after_actor_reason_and_replay_adds_nothing()
    {
        var (f, service, match, tournament) = Scoring();
        f.Seed(new TournamentMatch { TournamentId = tournament.Id });
        await service.ScoreAsync(tournament.Id, match.Id, new(21, 19));
        Assert.Equal("Scheduled", tournament.Status);
        f.UtcNow = f.UtcNow.AddMinutes(5);
        await service.ScoreAsync(tournament.Id, match.Id, new(19, 21, "Đối chiếu biên bản", 1));
        await service.ScoreAsync(tournament.Id, match.Id, new(19, 21, "Đối chiếu biên bản", 1));
        Assert.Equal(2, f.Repo<TournamentScoreRevision>().Rows.Count);
        var audit = f.Repo<TournamentScoreRevision>().Rows.Single(x => x.Revision == 2);
        Assert.Equal((21, 19), (audit.OldScoreA, audit.OldScoreB));
        Assert.Equal((19, 21), (audit.ScoreA, audit.ScoreB));
        Assert.Equal(f.Actor.Id, audit.ActorId);
        Assert.Equal(f.UtcNow, audit.CreatedAt);
        Assert.Equal("Đối chiếu biên bản", audit.Reason);
        Assert.Equal(21, f.Repo<TournamentScoreRevision>().Rows[0].ScoreA);
    }

    [Fact]
    public async Task Score_calls_branch_authorization_before_changing_data()
    {
        var (f, service, match, tournament) = Scoring();
        f.DenyStaff = true;
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.ScoreAsync(tournament.Id, match.Id, new(21, 19)));
        Assert.Equal(3, f.RequestedBranch);
        Assert.Null(match.ScoreA);
        Assert.Empty(f.Repo<TournamentScoreRevision>().Rows);
    }

    private const string Token = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private static (ClubLogicFixture F, CalendarSubscriptionService Service, CalendarSubscription Subscription) Calendar()
    {
        var f = new ClubLogicFixture();
        f.Seed(f.Actor);
        var subscription = f.Seed(new CalendarSubscription { UserId = f.Actor.Id, TokenHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Token))) });
        f.Seed(new TimeSlot { Id = 1, StartTime = new(10, 0), EndTime = new(11, 0) });
        return (f, new(f, f.Access, f), subscription);
    }

    [Theory]
    [InlineData(1, "TENTATIVE")]
    [InlineData(0, "CANCELLED")]
    [InlineData(-1, "CANCELLED")]
    public async Task Pending_calendar_event_becomes_tombstone_at_expiry_with_same_uid(int seconds, string status)
    {
        var (f, service, _) = Calendar();
        f.Seed(new Booking { Id = 99, UserId = f.Actor.Id, SlotId = 1, Date = f.Today, Status = BookingStatus.Pending, CreatedAt = f.UtcNow.AddHours(-1), PendingExpiresAt = f.UtcNow.AddSeconds(seconds) });
        var feed = await service.FeedAsync(Token);
        Assert.Contains("UID:booking-99@badmintonbooking\r\n", feed);
        Assert.Contains($"STATUS:{status}\r\n", feed);
        if (seconds <= 0) Assert.Contains($"LAST-MODIFIED:{f.UtcNow.AddSeconds(seconds):yyyyMMdd'T'HHmmss'Z'}\r\n", feed);
        Assert.Contains("DTSTART:20261027T030000Z\r\n", feed);
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("gggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggg")]
    [InlineData("bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb")]
    public async Task Invalid_or_unknown_token_does_not_record_successful_fetch(string token)
    {
        var (f, service, subscription) = Calendar();
        await Assert.ThrowsAsync<NotFoundException>(() => service.FeedAsync(token));
        Assert.Null(subscription.LastFetchedAtUtc);
        Assert.Equal(0, f.Saves);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Token_revoked_or_rotated_between_lookup_and_lock_cannot_fetch_or_mark_read(bool revoked)
    {
        var (f, service, subscription) = Calendar();
        f.AfterLock = _ =>
        {
            if (revoked) subscription.Revoked = true;
            else subscription.TokenHash = "ROTATED_HASH";
        };
        await Assert.ThrowsAsync<NotFoundException>(() => service.FeedAsync(Token));
        Assert.Null(subscription.LastFetchedAtUtc);
        Assert.Equal(0, f.Saves);
    }

    [Fact]
    public async Task Unverified_owner_cannot_fetch_calendar_or_mark_read()
    {
        var (f, service, subscription) = Calendar();
        f.Actor.IsVerified = false;
        await Assert.ThrowsAsync<NotFoundException>(() => service.FeedAsync(Token));
        Assert.Null(subscription.LastFetchedAtUtc);
        Assert.Equal(0, f.Saves);
    }

    [Fact]
    public async Task Feed_isolates_owner_omits_private_note_and_preserves_deleted_tombstone()
    {
        var (f, service, _) = Calendar();
        f.Seed(new Booking { Id = 1, UserId = f.Actor.Id, SlotId = 1, Date = f.Today, Status = BookingStatus.Confirmed, DeletedAt = f.UtcNow, Note = "PRIVATE_NOTE_SENTINEL" });
        f.Seed(new Booking { Id = 2, UserId = 888, SlotId = 1, Date = f.Today, BookingCode = "OTHER_OWNER_SENTINEL" });
        var feed = await service.FeedAsync(Token);
        Assert.Contains("UID:booking-1@badmintonbooking", feed);
        Assert.Contains("STATUS:CANCELLED", feed);
        Assert.DoesNotContain("booking-2@", feed);
        Assert.DoesNotContain("PRIVATE_NOTE_SENTINEL", feed);
        Assert.DoesNotContain("OTHER_OWNER_SENTINEL", feed);
    }

    [Fact]
    public async Task Vietnamese_and_emoji_lines_fold_by_utf8_bytes_without_losing_text()
    {
        var (f, service, _) = Calendar();
        var longText = string.Concat(Enumerable.Repeat("Cầu lông 🏸 ", 25));
        var address = longText + ";A,B\\C\nTầng 2";
        f.Seed(new Branch { Id = 1, Address = address });
        f.Seed(new Booking { UserId = f.Actor.Id, BranchId = 1, SlotId = 1, Date = f.Today });
        var feed = await service.FeedAsync(Token);
        foreach (var line in feed.Split("\r\n")) Assert.InRange(Encoding.UTF8.GetByteCount(line), 0, 75);
        var unfolded = feed.Replace("\r\n ", "");
        Assert.Contains("LOCATION:" + longText + "\\;A\\,B\\\\C\\nTầng 2\r\n", unfolded);
        Assert.DoesNotContain("\uFFFD", feed);
    }
}
