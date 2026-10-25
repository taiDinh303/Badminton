namespace ModelViews.Group
{
    public record JoinGroupRequest(string? InviteCode);
    public record RenameGroupRequest(string Name);
    public record TransferGroupRequest(int UserId);
    public record CreateGroupRequest(string Name, int MaxMembers = 10);

    public record GroupView(int Id, string Name, int OwnerId, string OwnerName,
        int MaxMembers, int MemberCount, DateTime CreatedAt, string? InviteCode = null);

    public record GroupMemberView(int UserId, string FullName, string PhoneNumber,
        bool IsCreator, DateTime JoinedAt);
}
