using Badminton.Core.Base;
using Badminton.Core.Enums;
using Badminton.Core.Policies;
using Contract.Repositories.Entity;
using Contract.Repositories.UnitOfWork;
using Contract.Services.Audit;
using Contract.Services.Operations;
using ModelViews.Operations;
namespace Services.Operations;

public sealed class MediaService(IUnitOfWork uow, IOperationsAccess access, IMediaStore store, IAuditService audit) : IMediaService
{
    public async Task<IReadOnlyList<MediaView>> ListAsync(int? courtId, int? reviewId)
    {
        await access.ActorAsync();
        var (rows, _) = await uow.GetRepository<StoredMedia>().GetPagedAsync(m => m.DeletedAt == null
            && (courtId != null ? m.CourtId == courtId && m.IsPublic : reviewId != null ? m.ReviewId == reviewId && m.IsPublic : m.OwnerId == access.ActorId && m.CourtId == null && m.ReviewId == null),
            m => m.CreatedAt, OrderDirection.Desc, new Page(1, 100));
        return rows.Select(m => new MediaView(m.Id, $"/api/v1/media/{m.Id}", m.ContentType, m.Length)).ToArray();
    }
    public async Task<MediaView> UploadAsync(byte[] bytes, int? courtId, int? reviewId, CancellationToken ct)
    {
        await access.ActorAsync();
        var type = RasterUploadPolicy.Validate(bytes);
        if (courtId.HasValue && reviewId.HasValue)
            throw new BadRequestException("Chọn một nơi gắn ảnh.");
        if (courtId is { } cid)
        {
            var court = await uow.GetRepository<Court>().GetByIdAsync(cid) ?? throw new NotFoundException("Court", cid);
            await access.StaffBranchAsync(court.BranchId);
        }
        if (reviewId is { } rid)
        {
            var review = await uow.GetRepository<Review>().GetByIdAsync(rid) ?? throw new NotFoundException("Review", rid);
            if (review.UserId != access.ActorId)
                throw new ConflictException("Chỉ chủ đánh giá được thêm ảnh.");
        }
        var key = Guid.NewGuid().ToString("N");
        await store.WriteAsync(key, bytes, ct);
        try
        {
            var row = new StoredMedia { OwnerId = access.ActorId, Key = key, ContentType = type, Length = bytes.Length, CourtId = courtId, ReviewId = reviewId, IsPublic = courtId.HasValue || reviewId.HasValue };
            await uow.RunInTransactionAsync(async () =>
            {
                await uow.LockBookingRequestAsync($"media-owner:{access.ActorId}");
                if (await uow.GetRepository<StoredMedia>().CountByConditionAsync(m => m.OwnerId == access.ActorId && m.DeletedAt == null) >= 100)
                    throw new ConflictException("Tối đa 100 ảnh đang lưu cho mỗi tài khoản.");
                if (reviewId.HasValue && await uow.GetRepository<StoredMedia>().CountByConditionAsync(m => m.ReviewId == reviewId && m.DeletedAt == null) >= 6)
                    throw new ConflictException("Mỗi đánh giá tối đa 6 ảnh.");
                await uow.GetRepository<StoredMedia>().AddAsync(row);
                await uow.SaveChangesAsync();
                await audit.LogAsync(access.ActorId, "media.uploaded", "StoredMedia", row.Id.ToString(), null, new
                {
                    row.Length,
                    row.ContentType
                });
            });
            return new(row.Id, $"/api/v1/media/{row.Id}", row.ContentType, row.Length);
        }
        catch { await store.DeleteAsync(key, CancellationToken.None); throw; }
    }
    public async Task<(byte[] Bytes, string ContentType)> ReadAsync(int id, CancellationToken ct)
    {
        var row = await uow.GetRepository<StoredMedia>().GetByIdAsync(id);
        if (row is null || row.DeletedAt.HasValue)
            throw new NotFoundException("StoredMedia", id);
        if (!row.IsPublic && row.OwnerId != access.ActorId)
            throw new NotFoundException("StoredMedia", id);
        if (row.ReviewId is { } reviewId)
        {
            var review = await uow.GetRepository<Review>().GetByIdAsync(reviewId);
            if (review is null || review.IsHidden)
                throw new NotFoundException("StoredMedia", id);
        }
        try { return (await store.ReadAsync(row.Key, ct), row.ContentType); }
        catch (FileNotFoundException) { throw new NotFoundException("StoredMedia", id); }
    }
    public async Task DeleteAsync(int id, CancellationToken ct)
    {
        await access.ActorAsync();
        var repo = uow.GetRepository<StoredMedia>();
        var row = await repo.GetByIdAsync(id) ?? throw new NotFoundException("StoredMedia", id);
        if (row.OwnerId != access.ActorId)
        {
            if (row.CourtId is not { } cid)
                throw new ConflictException("Chỉ chủ ảnh được xóa.");
            var court = await uow.GetRepository<Court>().GetByIdAsync(cid) ?? throw new NotFoundException("Court", cid);
            await access.StaffBranchAsync(court.BranchId);
        }
        await uow.RunInTransactionAsync(async () =>
        {
            await uow.LockBookingRequestAsync($"media-owner:{row.OwnerId}");
            var snapshot = (await uow.GetRepository<User>().GetByConditionAsync(u => u.Id == row.OwnerId)).SingleOrDefault();
            if (snapshot?.AvatarUrl == $"/api/v1/media/{id}") {
                var owner = (await uow.GetRepository<User>().GetByIdAsync(row.OwnerId))!;
                owner.AvatarUrl = null;
                uow.GetRepository<User>().UpdateProperties(owner, nameof(User.AvatarUrl));
            }
            row.DeletedAt = DateTime.UtcNow;
            repo.UpdateProperties(row, nameof(StoredMedia.DeletedAt));
            await uow.SaveChangesAsync();
            await audit.LogAsync(access.ActorId, "media.deleted", "StoredMedia", id.ToString(), null, null);
        });
        await store.DeleteAsync(row.Key, ct);
    }
    public async Task SetAvatarAsync(int id)
    {
        var actor = await access.ActorAsync();
        await uow.RunInTransactionAsync(async () =>
        {
            await uow.LockBookingRequestAsync($"media-owner:{actor.Id}");
            var row = await uow.GetRepository<StoredMedia>().GetByIdAsync(id);
            if (row is null || row.OwnerId != actor.Id || row.DeletedAt.HasValue || row.CourtId.HasValue || row.ReviewId.HasValue)
                throw new ConflictException("Chọn ảnh cá nhân đã tải lên.");
            // Avatars are public only after this explicit publishing operation.
            row.IsPublic = true;
            uow.GetRepository<StoredMedia>().UpdateProperties(row, nameof(StoredMedia.IsPublic));
            actor.AvatarUrl = $"/api/v1/media/{id}";
            uow.GetRepository<User>().UpdateProperties(actor, nameof(User.AvatarUrl));
            await uow.SaveChangesAsync();
        });
    }
}
