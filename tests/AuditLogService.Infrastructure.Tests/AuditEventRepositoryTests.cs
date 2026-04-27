using AuditLogService.Domain.Entities;
using AuditLogService.Domain.Repositories;
using AuditLogService.Infrastructure.Persistence.Repositories;
using Shouldly;
using Xunit;

namespace AuditLogService.Infrastructure.Tests;

[Collection(nameof(PostgresCollection))]
public sealed class AuditEventRepositoryTests
{
    private readonly PostgresFixture _fx;

    public AuditEventRepositoryTests(PostgresFixture fx) => _fx = fx;

    private static AuditEvent NewEvent(string actor, string action = "act", string resource = "res",
        DateTime? at = null, string? correlationId = null)
        => AuditEvent.Record(actor, action, resource, null, correlationId, null,
            () => at ?? DateTime.UtcNow);

    [Fact]
    public async Task Append_PersistsEvent()
    {
        await using var ctx = _fx.CreateContext();
        var repo = _fx.CreateRepository(ctx);

        var evt = NewEvent($"a-{Guid.NewGuid()}");
        await repo.AppendAsync(evt);

        await using var verify = _fx.CreateContext();
        var found = await verify.AuditEvents.FindAsync(evt.Id);
        found.ShouldNotBeNull();
        found.Actor.ShouldBe(evt.Actor);
    }

    [Fact]
    public async Task Query_FiltersByActor()
    {
        var actor = $"actor-{Guid.NewGuid()}";
        await using (var ctx = _fx.CreateContext())
        {
            var repo = _fx.CreateRepository(ctx);
            await repo.AppendAsync(NewEvent(actor));
            await repo.AppendAsync(NewEvent(actor));
            await repo.AppendAsync(NewEvent("someone-else"));
        }

        await using var read = _fx.CreateContext();
        var readRepo = _fx.CreateRepository(read);
        var results = await readRepo.QueryAsync(new AuditEventFilter(Actor: actor));

        results.Count.ShouldBe(2);
        results.ShouldAllBe(e => e.Actor == actor);
    }

    [Fact]
    public async Task Query_PaginatesAndOrdersByTimestampDesc()
    {
        var actor = $"actor-{Guid.NewGuid()}";
        var t0 = DateTime.UtcNow.AddMinutes(-30);

        await using (var ctx = _fx.CreateContext())
        {
            var repo = _fx.CreateRepository(ctx);
            for (var i = 0; i < 5; i++)
                await repo.AppendAsync(NewEvent(actor, at: t0.AddMinutes(i)));
        }

        await using var read = _fx.CreateContext();
        var readRepo = _fx.CreateRepository(read);

        var page1 = await readRepo.QueryAsync(new AuditEventFilter(Actor: actor, PageSize: 2, Page: 1));
        var page2 = await readRepo.QueryAsync(new AuditEventFilter(Actor: actor, PageSize: 2, Page: 2));

        page1.Count.ShouldBe(2);
        page2.Count.ShouldBe(2);
        page1[0].Timestamp.ShouldBeGreaterThan(page1[1].Timestamp);
        page1[1].Timestamp.ShouldBeGreaterThan(page2[0].Timestamp);
    }

    [Fact]
    public async Task DeleteOlderThan_RemovesEventsBeforeCutoff()
    {
        var actor = $"actor-{Guid.NewGuid()}";
        var oldAt = DateTime.UtcNow.AddDays(-10);
        var freshAt = DateTime.UtcNow.AddMinutes(-1);

        await using (var ctx = _fx.CreateContext())
        {
            var repo = _fx.CreateRepository(ctx);
            await repo.AppendAsync(NewEvent(actor, at: oldAt));
            await repo.AppendAsync(NewEvent(actor, at: oldAt));
            await repo.AppendAsync(NewEvent(actor, at: freshAt));
        }

        var cutoff = DateTime.UtcNow.AddDays(-1);
        int deleted;
        await using (var ctx = _fx.CreateContext())
        {
            deleted = await _fx.CreateRepository(ctx).DeleteOlderThanAsync(cutoff);
        }

        deleted.ShouldBe(2);

        await using var verify = _fx.CreateContext();
        var remaining = await _fx.CreateRepository(verify)
            .QueryAsync(new AuditEventFilter(Actor: actor));
        remaining.Count.ShouldBe(1);
        remaining[0].Timestamp.ShouldBeGreaterThan(cutoff);
    }
}
