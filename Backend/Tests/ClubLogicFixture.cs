using System.Linq.Expressions;
using System.Reflection;
using Badminton.Core.Base;
using Badminton.Core.Time;
using Contract.Repositories.Entity;
using Contract.Repositories.UnitOfWork;
using Contract.Services.Operations;

namespace Badminton.Tests;

// Deliberately no database, EF provider, HTTP host or wall-clock dependency.
// This double does NOT simulate transaction rollback or prove database locking.
internal sealed class ClubLogicFixture : IUnitOfWork, IBusinessClock
{
    private readonly Dictionary<Type, object> repositories = new();
    public DateTime UtcNow { get; set; } = new(2026, 10, 27, 3, 0, 0, DateTimeKind.Utc);
    public DateTime LocalNow => UtcNow.AddHours(7);
    public DateOnly Today => DateOnly.FromDateTime(LocalNow);
    public int Saves { get; private set; }
    public List<string> Locks { get; } = [];
    public Action<string>? AfterLock { get; set; }
    public List<(int UserId, string EventKey)> Messages { get; } = [];
    public int Reconciliations { get; private set; }
    public int ReconciledChanges { get; set; }
    public bool DenyStaff { get; set; }
    public int? RequestedBranch { get; private set; }
    public User Actor { get; } = new() { Id = 7, IsVerified = true };
    public MemoryRepository<T> Repo<T>() where T : class
    {
        if (!repositories.TryGetValue(typeof(T), out var repo))
            repositories[typeof(T)] = repo = new MemoryRepository<T>();
        return (MemoryRepository<T>)repo;
    }
    public T Seed<T>(T entity) where T : IntBaseEntity
    {
        Repo<T>().AddAsync(entity).GetAwaiter().GetResult();
        return entity;
    }
    public IOperationsAccess Access => LogicProxy.Create<IOperationsAccess>((method, args) => method switch
    {
        "get_ActorId" => Actor.Id,
        "ActorAsync" => Task.FromResult(Actor),
        "StaffBranchAsync" => CheckBranch((int?)args![0]),
        _ => throw new NotSupportedException(method)
    });
    private Task<int?> CheckBranch(int? branch)
    {
        RequestedBranch = branch;
        return DenyStaff ? Task.FromException<int?>(new UnauthorizedAccessException("Fixture branch denied")) : Task.FromResult(branch);
    }
    public IInboxService Inbox => LogicProxy.Create<IInboxService>((method, args) =>
    {
        if (method != "PublishAsync") throw new NotSupportedException(method);
        Messages.Add(((int)args![0]!, (string)args[2]!));
        return Task.CompletedTask;
    });
    public ICoachCourtService Courts => LogicProxy.Create<ICoachCourtService>((method, _) =>
    {
        if (method != "ReconcileAsync") throw new NotSupportedException(method);
        Reconciliations++;
        return Task.FromResult(ReconciledChanges);
    });
    public IGenericRepository<T> GetRepository<T>() where T : class => Repo<T>();
    public Task<int> SaveChangesAsync(CancellationToken ct = default) { Saves++; return Task.FromResult(1); }
    public Task RunInTransactionAsync(Func<Task> action, CancellationToken ct = default) => action();
    public Task LockNotificationAsync(string key, CancellationToken ct = default)
    {
        Locks.Add(key);
        AfterLock?.Invoke(key);
        return Task.CompletedTask;
    }
    public IBookingCommands Bookings => throw new NotSupportedException();
    public IPaymentCommands Payments => throw new NotSupportedException();
    public IVoucherCommands Vouchers => throw new NotSupportedException();
    public Task LockCourtDateAsync(int courtId, DateOnly date, CancellationToken ct = default) => throw new NotSupportedException();
    public Task LockVoucherUserAsync(int userId, CancellationToken ct = default) => throw new NotSupportedException();
    public Task LockPointsUserAsync(int userId, CancellationToken ct = default) => throw new NotSupportedException();
    public Task LockBookingRequestAsync(string key, CancellationToken ct = default) => throw new NotSupportedException();
    public Task LockTimeSlotsAsync(CancellationToken ct = default) => throw new NotSupportedException();
    public Task LockGroupAsync(int groupId, CancellationToken ct = default) => throw new NotSupportedException();
}

internal sealed class MemoryRepository<T> : IGenericRepository<T> where T : class
{
    public List<T> Rows { get; } = [];
    private static int Id(T row) => ((IntBaseEntity)(object)row).Id;
    public Task<T?> GetByIdAsync(int id, CancellationToken ct = default) => Task.FromResult(Rows.SingleOrDefault(x => Id(x) == id));
    public Task<T?> GetFreshByIdAsync(int id, CancellationToken ct = default) => GetByIdAsync(id, ct);
    public Task<IReadOnlyList<T>> GetAllAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<T>>(Rows.ToArray());
    public Task<IReadOnlyList<T>> GetByConditionAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<T>>(Rows.Where(predicate.Compile()).ToArray());
    public Task<int> CountByConditionAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default) => Task.FromResult(Rows.Count(predicate.Compile()));
    public Task<int> SumIntByConditionAsync(Expression<Func<T, bool>> predicate, Expression<Func<T, int>> selector, CancellationToken ct = default) => Task.FromResult(Rows.Where(predicate.Compile()).Sum(selector.Compile()));
    public Task<(IReadOnlyList<T> Items, int TotalCount)> GetPagedAsync(Expression<Func<T, bool>> predicate, Expression<Func<T, DateTime>> orderBy, OrderDirection direction, Page page, CancellationToken ct = default)
    {
        var rows = Rows.Where(predicate.Compile()).ToArray();
        var sorted = direction == OrderDirection.Asc ? rows.OrderBy(orderBy.Compile()) : rows.OrderByDescending(orderBy.Compile());
        return Task.FromResult<(IReadOnlyList<T>, int)>((sorted.Skip(page.Skip).Take(page.Size).ToArray(), rows.Length));
    }
    public Task AddAsync(T entity, CancellationToken ct = default)
    {
        var row = (IntBaseEntity)(object)entity;
        if (row.Id == 0) row.Id = Rows.Count == 0 ? 1 : Rows.Max(Id) + 1;
        Rows.Add(entity);
        return Task.CompletedTask;
    }
    public async Task AddRangeAsync(IEnumerable<T> entities, CancellationToken ct = default) { foreach (var row in entities) await AddAsync(row, ct); }
    public void Update(T entity) { if (!Rows.Contains(entity)) throw new InvalidOperationException("Unknown fixture row"); }
    public void UpdateProperties(T entity, params string[] properties) => Update(entity);
    public Task RemoveAsync(T entity, CancellationToken ct = default) { Rows.Remove(entity); return Task.CompletedTask; }
}

// Fail on unexpected calls instead of silently returning a successful default.
public class LogicProxy : DispatchProxy
{
    private Func<string, object?[]?, object?> handler = null!;
    internal static T Create<T>(Func<string, object?[]?, object?> handler) where T : class
    {
        var proxy = Create<T, LogicProxy>();
        ((LogicProxy)(object)proxy).handler = handler;
        return proxy;
    }
    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => handler(targetMethod!.Name, args);
}
