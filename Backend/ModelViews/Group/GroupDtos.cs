namespace ModelViews.Group
{
    public record CreateGroupRequest(string Name, int MaxMembers = 10);

    public record GroupView(int Id, string Name, int OwnerId, string OwnerName,
        int MaxMembers, int MemberCount, DateTime CreatedAt);

    public record GroupMemberView(int UserId, string FullName, string PhoneNumber,
        bool IsCreator, DateTime JoinedAt);
}
