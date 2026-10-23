using Contract.Repositories.Entity;
using Contract.Repositories.UnitOfWork;
using Contract.Services.Contact;
using ModelViews.Contact;

namespace Services;

public sealed class ContactService(IUnitOfWork unitOfWork) : IContactService
{
    public async Task CreateAsync(ContactRequest request, CancellationToken ct)
    {
        // Honeypot submissions never enter the inbox. No personal details in logs.
        if (!string.IsNullOrWhiteSpace(request.Website)) return;
        await unitOfWork.GetRepository<ContactInquiry>().AddAsync(new ContactInquiry
        {
            FullName = request.FullName.Trim(), Phone = request.Phone.Trim(),
            Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim(),
            Message = request.Message?.Trim() ?? "",
        }, ct);
        await unitOfWork.SaveChangesAsync(ct);
    }

    public Task<(IReadOnlyList<ContactInquiry> Items, int TotalCount)> ListAsync(int page, CancellationToken ct) =>
        unitOfWork.GetRepository<ContactInquiry>().GetPagedAsync(_ => true,
            x => x.CreatedAt, OrderDirection.Desc, new Page(Math.Clamp(page, 1, 100000), 20), ct);

    public async Task<bool> MarkHandledAsync(int id, CancellationToken ct)
    {
        var repository = unitOfWork.GetRepository<ContactInquiry>();
        var inquiry = await repository.GetByIdAsync(id, ct);
        if (inquiry is null) return false;
        if (inquiry.HandledAt is not null) return true;
        inquiry.HandledAt = DateTime.UtcNow;
        repository.Update(inquiry);
        await unitOfWork.SaveChangesAsync(ct);
        return true;
    }
}
