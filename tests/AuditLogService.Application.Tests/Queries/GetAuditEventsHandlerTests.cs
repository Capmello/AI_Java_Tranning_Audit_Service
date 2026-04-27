using AuditLogService.Application.Queries.GetAuditEvents;
using AuditLogService.Application.Tests.Fakes;
using AuditLogService.Domain.Entities;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Xunit;

namespace AuditLogService.Application.Tests.Queries;

public sealed class GetAuditEventsHandlerTests
{
    [Fact]
    public async Task Handle_PassesFilterValuesToRepository()
    {
        var repo = new FakeAuditEventRepository();
        var handler = new GetAuditEventsHandler(repo, NullLogger<GetAuditEventsHandler>.Instance);

        var from = DateTime.UtcNow.AddDays(-1);
        var to = DateTime.UtcNow;
        var query = new GetAuditEventsQuery("alice", "login", "auth", "corr-1", from, to, 25, 2);

        await handler.Handle(query, CancellationToken.None);

        var f = repo.LastFilter.ShouldNotBeNull();
        f.Actor.ShouldBe("alice");
        f.Action.ShouldBe("login");
        f.Resource.ShouldBe("auth");
        f.CorrelationId.ShouldBe("corr-1");
        f.FromUtc.ShouldBe(from);
        f.ToUtc.ShouldBe(to);
        f.PageSize.ShouldBe(25);
        f.Page.ShouldBe(2);
    }

    [Fact]
    public async Task Handle_MapsRepositoryResultsToDtos()
    {
        var repo = new FakeAuditEventRepository();
        var evt = AuditEvent.Record("a", "b", "c", null, null, null, () => DateTime.UtcNow);
        repo.Stored.Add(evt);

        var handler = new GetAuditEventsHandler(repo, NullLogger<GetAuditEventsHandler>.Instance);
        var result = await handler.Handle(new GetAuditEventsQuery(), CancellationToken.None);

        result.Count.ShouldBe(1);
        result[0].Id.ShouldBe(evt.Id);
        result[0].Actor.ShouldBe("a");
    }

    [Fact]
    public async Task Handle_EmptyResult_ReturnsEmptyList()
    {
        var repo = new FakeAuditEventRepository();
        var handler = new GetAuditEventsHandler(repo, NullLogger<GetAuditEventsHandler>.Instance);

        var result = await handler.Handle(new GetAuditEventsQuery(), CancellationToken.None);

        result.ShouldBeEmpty();
    }
}
