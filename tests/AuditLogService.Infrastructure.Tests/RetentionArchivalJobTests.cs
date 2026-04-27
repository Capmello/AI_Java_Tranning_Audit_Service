using AuditLogService.Domain.Entities;
using AuditLogService.Domain.Repositories;
using AuditLogService.Infrastructure.BackgroundJobs;
using AuditLogService.Infrastructure.Persistence.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Xunit;

namespace AuditLogService.Infrastructure.Tests;

[Collection(nameof(PostgresCollection))]
public sealed class RetentionArchivalJobTests
{
    private readonly PostgresFixture _fx;

    public RetentionArchivalJobTests(PostgresFixture fx) => _fx = fx;

    [Fact]
    public async Task Job_DeletesEventsOlderThanRetention()
    {
        var actor = $"retention-{Guid.NewGuid()}";

        await using (var ctx = _fx.CreateContext())
        {
            var repo = _fx.CreateRepository(ctx);
            await repo.AppendAsync(MakeEvent(actor, DateTime.UtcNow.AddDays(-100)));
            await repo.AppendAsync(MakeEvent(actor, DateTime.UtcNow.AddDays(-95)));
            await repo.AppendAsync(MakeEvent(actor, DateTime.UtcNow.AddDays(-1)));
        }

        var services = new ServiceCollection();
        services.AddScoped<IAuditEventRepository>(_ => _fx.CreateRepository(_fx.CreateContext()));
        var sp = services.BuildServiceProvider();

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["RetentionDays"] = "90" })
            .Build();

        var job = new RetentionArchivalJob(
            sp.GetRequiredService<IServiceScopeFactory>(),
            config,
            NullLogger<RetentionArchivalJob>.Instance);

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromSeconds(2));

        // Job runs forever; we trigger one pass then cancel via the Task.Delay loop
        var run = job.StartAsync(cts.Token);
        await Task.Delay(TimeSpan.FromMilliseconds(500));
        await job.StopAsync(CancellationToken.None);
        await run;

        await using var verify = _fx.CreateContext();
        var remaining = await _fx.CreateRepository(verify)
            .QueryAsync(new AuditEventFilter(Actor: actor));
        remaining.Count.ShouldBe(1);
    }

    private static AuditEvent MakeEvent(string actor, DateTime at) =>
        AuditEvent.Record(actor, "act", "res", null, null, null, () => at);
}
