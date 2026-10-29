using Badminton.Core.Base;
using System.Text.Json.Serialization;
namespace Contract.Repositories.Entity;

public sealed class PlayerSession : IntBaseEntity
{
    public int OwnerId
    {
        get; set;
    }
    public int BranchId
    {
        get; set;
    }
    public string Area { get; set; } = "";
    public int Level
    {
        get; set;
    }
    public DateTime StartsAtUtc
    {
        get; set;
    }
    public DateTime EndsAtUtc
    {
        get; set;
    }
    public int Capacity
    {
        get; set;
    }
    public string Note { get; set; } = "";
    public bool Closed
    {
        get; set;
    }
}
public sealed class PlayerSessionMember : IntBaseEntity
{
    public int SessionId
    {
        get; set;
    }
    public int UserId
    {
        get; set;
    }
    public string Status { get; set; } = "Requested";
}
public sealed class GroupExpense : IntBaseEntity
{
    public string SplitMethod { get; set; } = "Equal";
    public int BookingId
    {
        get; set;
    }
    public int Total
    {
        get; set;
    }
    public int CreatedBy
    {
        get; set;
    }
}
public sealed class GroupExpenseShare : IntBaseEntity
{
    public int? AttendedMinutes
    {
        get; set;
    }
    public int ExpenseId
    {
        get; set;
    }
    public int UserId
    {
        get; set;
    }
    public int Amount
    {
        get; set;
    }
    public DateTime? PaidAt
    {
        get; set;
    }
    public int? RecordedBy
    {
        get; set;
    }
}
public sealed class EquipmentItem : IntBaseEntity
{
    public int BranchId
    {
        get; set;
    }
    public string Name { get; set; } = "";
    public bool Rental
    {
        get; set;
    }
    public int Stock
    {
        get; set;
    }
    public int UnitPrice
    {
        get; set;
    }
    public bool Active { get; set; } = true;
}
public sealed class EquipmentOrder : IntBaseEntity
{
    public DateTime? PickupExpiresAtUtc
    {
        get; set;
    }
    public DateTime? OverdueNotifiedAtUtc
    {
        get; set;
    }
    public DateTime? ExpiredAtUtc
    {
        get; set;
    }
    public int ItemId
    {
        get; set;
    }
    public int UserId
    {
        get; set;
    }
    public int Quantity
    {
        get; set;
    }
    public int UnitPrice
    {
        get; set;
    }
    public bool Rental
    {
        get; set;
    }
    public string RequestKey { get; set; } = "";
    public string Status { get; set; } = "Reserved";
    public DateTime DueAtUtc
    {
        get; set;
    }
    public DateTime? ReturnedAtUtc
    {
        get; set;
    }
    public int? HandledBy
    {
        get; set;
    }
}
public sealed class CoachProfile : IntBaseEntity
{
    public int BranchId
    {
        get; set;
    }
    public string Name { get; set; } = "";
    public string Bio { get; set; } = "";
    public bool Active { get; set; } = true;
}
public sealed class CoachSlot : IntBaseEntity
{
    public DateTime? LastCourtCheckedAtUtc
    {
        get; set;
    }
    public int? CourtBookingId
    {
        get; set;
    }
    public int CoachId
    {
        get; set;
    }
    public DateTime StartsAtUtc
    {
        get; set;
    }
    public DateTime EndsAtUtc
    {
        get; set;
    }
    public int Capacity
    {
        get; set;
    }
    public int PricePerPerson
    {
        get; set;
    }
    public bool Cancelled
    {
        get; set;
    }
}
public sealed class CoachingReservation : IntBaseEntity
{
    public int SlotId
    {
        get; set;
    }
    public int UserId
    {
        get; set;
    }
    public int People
    {
        get; set;
    }
    public int Total
    {
        get; set;
    }
    public string RequestKey { get; set; } = "";
    public string Status { get; set; } = "Reserved";
}
public sealed class MiniTournament : IntBaseEntity
{
    public int BranchId
    {
        get; set;
    }
    public int OrganizerId
    {
        get; set;
    }
    public string Name { get; set; } = "";
    public int MaxTeams
    {
        get; set;
    }
    public string Status { get; set; } = "Registration";
}
public sealed class TournamentTeam : IntBaseEntity
{
    public int TournamentId
    {
        get; set;
    }
    public int CaptainId
    {
        get; set;
    }
    public string Name { get; set; } = "";
    public string Players { get; set; } = "";
    public int Pool
    {
        get; set;
    }
}
public sealed class TournamentMatch : IntBaseEntity
{
    public int TournamentId
    {
        get; set;
    }
    public int TeamAId
    {
        get; set;
    }
    public int TeamBId
    {
        get; set;
    }
    public int Pool
    {
        get; set;
    }
    public int Round
    {
        get; set;
    }
    public DateTime? StartsAtUtc
    {
        get; set;
    }
    public DateTime? EndsAtUtc
    {
        get; set;
    }
    public string Location { get; set; } = "";
    public int? ScoreA
    {
        get; set;
    }
    public int? ScoreB
    {
        get; set;
    }
}
public sealed class CalendarSubscription : IntBaseEntity
{
    public DateTime? LastFetchedAtUtc
    {
        get; set;
    }
    public int UserId
    {
        get; set;
    }
    [JsonIgnore] public string TokenHash { get; set; } = "";
    public bool Revoked
    {
        get; set;
    }
}

public sealed class EquipmentConditionReport : IntBaseEntity
{
    public int OrderId
    {
        get; set;
    }
    public int ActorId
    {
        get; set;
    }
    public int UnusableQuantity
    {
        get; set;
    }
    public bool Lost
    {
        get; set;
    }
    public int AssessedAmount
    {
        get; set;
    }
    public string Reason { get; set; } = "";
}
public sealed class TournamentScoreRevision : IntBaseEntity
{
    public int MatchId
    {
        get; set;
    }
    public int ActorId
    {
        get; set;
    }
    public int Revision
    {
        get; set;
    }
    public int? OldScoreA
    {
        get; set;
    }
    public int? OldScoreB
    {
        get; set;
    }
    public int ScoreA
    {
        get; set;
    }
    public int ScoreB
    {
        get; set;
    }
    public string Reason { get; set; } = "";
}
