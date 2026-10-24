using Contract.Repositories.Entity;
using ModelViews.Contact;

namespace Contract.Services.Contact;

public interface IContactService
{
    Task CreateAsync(ContactRequest request, CancellationToken ct);
    Task<(IReadOnlyList<ContactInquiry> Items, int TotalCount)> ListAsync(int page, CancellationToken ct);
    Task<bool> MarkHandledAsync(int id, CancellationToken ct);
}
