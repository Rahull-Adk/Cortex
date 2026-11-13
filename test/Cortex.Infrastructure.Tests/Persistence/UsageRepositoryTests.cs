using Cortex.Domain.Entities;
using Cortex.Infrastructure.Persistence;
using Cortex.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace Cortex.Infrastructure.Tests.Persistence;

// Needs a running Docker daemon: the atomic quota updates are only meaningful against real PostgreSQL.
public class UsageRepositoryTests : IAsyncLifetime
{
    private static readonly DateTime PeriodStart = new(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime PeriodEnd = new(2026, 12, 1, 0, 0, 0, DateTimeKind.Utc);

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();
    private Guid _organizationId;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        await using var db = NewContext();
        await db.Database.MigrateAsync();

        var owner = User.Create("Owner", "owner@example.com");
        var organization = Organization.Create("Shwe Store", "shwe-store", owner.Id);
        db.Users.Add(owner);
        db.Organizations.Add(organization);
        await db.SaveChangesAsync();

        _organizationId = organization.Id;
    }

    public Task DisposeAsync() => _postgres.DisposeAsync().AsTask();

    [Fact]
    public async Task Migrations_SeedPlanCatalog()
    {
        await using var db = NewContext();

        var plans = await db.Plans.OrderBy(p => p.SortOrder).ToListAsync();

        Assert.Equal(["free", "starter", "growth"], plans.Select(p => p.Code));
        Assert.Equal([0L, 99_000L, 249_000L], plans.Select(p => p.PriceMinor));
        Assert.Equal([0, 500, 2_500], plans.Select(p => p.AiAnswersPerPeriod));
        Assert.Equal([0, 100, 500], plans.Select(p => p.MaxDocuments));
    }

    [Fact]
    public async Task TryConsumeAiAnswerAsync_ConcurrentRequests_NeverExceedAllowance()
    {
        const int limit = 20;

        var results = await Task.WhenAll(Enumerable.Range(0, 60).Select(_ => Task.Run(async () =>
        {
            await using var db = NewContext();
            return await new UsageRepository(db).TryConsumeAiAnswerAsync(_organizationId, PeriodStart, PeriodEnd, limit);
        })));

        await using var verify = NewContext();
        Assert.Equal(limit, results.Count(granted => granted));
        Assert.Equal(limit, await new UsageRepository(verify).GetAiAnswersUsedAsync(_organizationId, PeriodStart));
    }

    [Fact]
    public async Task TryConsumeAiAnswerAsync_NewPeriod_StartsFromZero()
    {
        await using var db = NewContext();
        var usage = new UsageRepository(db);
        Assert.True(await usage.TryConsumeAiAnswerAsync(_organizationId, PeriodStart, PeriodEnd, 1));
        Assert.False(await usage.TryConsumeAiAnswerAsync(_organizationId, PeriodStart, PeriodEnd, 1));

        Assert.True(await usage.TryConsumeAiAnswerAsync(_organizationId, PeriodEnd, PeriodEnd.AddMonths(1), 1));
        Assert.Equal(1, await usage.GetAiAnswersUsedAsync(_organizationId, PeriodStart));
    }

    [Fact]
    public async Task RefundAiAnswerAsync_ReturnsUnitAndStopsAtZero()
    {
        await using var db = NewContext();
        var usage = new UsageRepository(db);
        await usage.TryConsumeAiAnswerAsync(_organizationId, PeriodStart, PeriodEnd, 5);

        await usage.RefundAiAnswerAsync(_organizationId, PeriodStart);
        await usage.RefundAiAnswerAsync(_organizationId, PeriodStart);

        Assert.Equal(0, await usage.GetAiAnswersUsedAsync(_organizationId, PeriodStart));
    }

    [Fact]
    public async Task TryReserveStorageAsync_ConcurrentUploads_NeverExceedDocumentLimit()
    {
        const int maxDocuments = 10;

        var results = await Task.WhenAll(Enumerable.Range(0, 40).Select(_ => Task.Run(async () =>
        {
            await using var db = NewContext();
            return await new UsageRepository(db).TryReserveStorageAsync(_organizationId, 100, maxDocuments, 1_000_000);
        })));

        await using var verify = NewContext();
        var stored = await new UsageRepository(verify).GetStorageAsync(_organizationId);
        Assert.Equal(maxDocuments, results.Count(granted => granted));
        Assert.Equal(maxDocuments, stored.DocumentCount);
        Assert.Equal(1_000L, stored.StorageBytes);
    }

    [Fact]
    public async Task TryReserveStorageAsync_RejectsUploadPastByteLimit_ThenReleaseFreesSpace()
    {
        await using var db = NewContext();
        var usage = new UsageRepository(db);
        Assert.True(await usage.TryReserveStorageAsync(_organizationId, 900, 10, 1_000));
        Assert.False(await usage.TryReserveStorageAsync(_organizationId, 101, 10, 1_000));

        await usage.ReleaseStorageAsync(_organizationId, 900);

        Assert.True(await usage.TryReserveStorageAsync(_organizationId, 1_000, 10, 1_000));
        var stored = await usage.GetStorageAsync(_organizationId);
        Assert.Equal(1, stored.DocumentCount);
        Assert.Equal(1_000L, stored.StorageBytes);
    }

    private CortexDbContext NewContext() =>
        new(new DbContextOptionsBuilder<CortexDbContext>().UseNpgsql(_postgres.GetConnectionString()).Options);
}
