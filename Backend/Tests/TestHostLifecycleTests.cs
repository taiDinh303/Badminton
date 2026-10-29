using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Repositories.Context;
using Xunit;

namespace Badminton.Tests;

public sealed class TestHostLifecycleTests
{
    [Fact]
    public async Task Schema_setup_has_a_bounded_timeout_without_raising_service_query_timeout()
    {
        var commands = new SchemaCommands();
        await using var h = new TestHost(useMigrations: true,
            configure: services => services.AddSingleton<IInterceptor>(commands));
        Assert.NotEmpty(commands.Timeouts);
        Assert.All(commands.Timeouts, seconds => Assert.Equal(120, seconds));
        await h.RunScopeAsync(async sp =>
        {
            var db = sp.GetRequiredService<BadmintonBookingDbContext>();
            Assert.Null(db.Database.GetCommandTimeout());
            Assert.Equal(2, await db.Branches.CountAsync());
        });
    }

    private sealed class SchemaCommands : DbCommandInterceptor
    {
        public List<int> Timeouts { get; } = [];
        public override InterceptionResult<int> NonQueryExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<int> result)
        {
            if (command.CommandText.Contains("CREATE TABLE", StringComparison.OrdinalIgnoreCase))
                Timeouts.Add(command.CommandTimeout);
            return result;
        }
    }
}
