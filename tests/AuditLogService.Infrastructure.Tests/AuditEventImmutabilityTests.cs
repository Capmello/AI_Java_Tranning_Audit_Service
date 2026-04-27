using AuditLogService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Shouldly;
using Xunit;

namespace AuditLogService.Infrastructure.Tests;

[Collection(nameof(PostgresCollection))]
public sealed class AuditEventImmutabilityTests
{
    private readonly PostgresFixture _fx;

    public AuditEventImmutabilityTests(PostgresFixture fx) => _fx = fx;

    [Fact]
    public async Task RawUpdate_OnAuditEvents_IsRejectedByTrigger()
    {
        var actor = $"immut-{Guid.NewGuid()}";
        await using (var seedCtx = _fx.CreateContext())
        {
            var repo = _fx.CreateRepository(seedCtx);
            await repo.AppendAsync(
                AuditEvent.Record(actor, "create", "doc", null, null, null, () => DateTime.UtcNow));
        }

        await using var ctx = _fx.CreateContext();

        var ex = await Should.ThrowAsync<PostgresException>(() =>
            ctx.Database.ExecuteSqlRawAsync(
                "UPDATE audit_events SET actor = 'tampered' WHERE actor = {0}", actor));

        ex.MessageText.ShouldContain("append-only");
    }

    [Fact]
    public async Task PersistedAuditEvent_RoundTripsUnchanged()
    {
        var original = AuditEvent.Record(
            $"actor-{Guid.NewGuid()}", "login", "auth", "res-1", "corr-9",
            new Dictionary<string, string> { ["ip"] = "10.0.0.1" },
            () => DateTime.UtcNow);

        await using (var ctx = _fx.CreateContext())
        {
            await _fx.CreateRepository(ctx).AppendAsync(original);
        }

        await using var read = _fx.CreateContext();
        var loaded = await read.AuditEvents.AsNoTracking().SingleAsync(e => e.Id == original.Id);

        loaded.Actor.ShouldBe(original.Actor);
        loaded.Action.ShouldBe(original.Action);
        loaded.Resource.ShouldBe(original.Resource);
        loaded.ResourceId.ShouldBe(original.ResourceId);
        loaded.CorrelationId.ShouldBe(original.CorrelationId);
        loaded.Metadata["ip"].ShouldBe("10.0.0.1");
        loaded.Timestamp.ShouldBe(original.Timestamp, TimeSpan.FromMilliseconds(1));
    }
}
