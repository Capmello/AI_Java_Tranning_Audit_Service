using AuditLogService.Application.Commands.RecordAuditEvent;
using AuditLogService.Application.Logging;
using AuditLogService.Application.Tests.Fakes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Xunit;

namespace AuditLogService.Application.Tests.Commands;

public sealed class RecordAuditEventHandlerTests
{
    [Fact]
    public async Task Handle_AppendsEventToRepository()
    {
        var repo = new FakeAuditEventRepository();
        var handler = new RecordAuditEventHandler(repo, new PassthroughPiiRedactor(), NullLogger<RecordAuditEventHandler>.Instance);

        var command = new RecordAuditEventCommand("user1", "create", "order", "ord-1", "corr-1", null);
        var dto = await handler.Handle(command, CancellationToken.None);

        repo.Stored.Count.ShouldBe(1);
        repo.Stored[0].Actor.ShouldBe("user1");
        dto.Id.ShouldBe(repo.Stored[0].Id);
    }

    [Fact]
    public async Task Handle_TimestampIsUtcAndServerAssigned()
    {
        var repo = new FakeAuditEventRepository();
        var handler = new RecordAuditEventHandler(repo, new PassthroughPiiRedactor(), NullLogger<RecordAuditEventHandler>.Instance);

        var before = DateTime.UtcNow.AddSeconds(-1);
        var command = new RecordAuditEventCommand("a", "b", "c", null, null, null);
        var dto = await handler.Handle(command, CancellationToken.None);

        dto.Timestamp.Kind.ShouldBe(DateTimeKind.Utc);
        dto.Timestamp.ShouldBeGreaterThan(before);
        dto.Timestamp.ShouldBeLessThan(DateTime.UtcNow.AddSeconds(5));
    }

    [Fact]
    public async Task Handle_PropagatesMetadata()
    {
        var repo = new FakeAuditEventRepository();
        var handler = new RecordAuditEventHandler(repo, new PassthroughPiiRedactor(), NullLogger<RecordAuditEventHandler>.Instance);

        var meta = new Dictionary<string, string> { ["ip"] = "10.0.0.1" };
        var command = new RecordAuditEventCommand("a", "b", "c", null, null, meta);
        var dto = await handler.Handle(command, CancellationToken.None);

        dto.Metadata.ShouldContainKeyAndValue("ip", "10.0.0.1");
    }

    [Fact]
    public async Task Handle_LogsErrorAndRethrows_WhenAppendFails()
    {
        var repo = new FakeAuditEventRepository
        {
            OnAppend = _ => throw new InvalidOperationException("db down")
        };
        var logger = new RecordingLogger<RecordAuditEventHandler>();
        var handler = new RecordAuditEventHandler(repo, new PassthroughPiiRedactor(), logger);

        var command = new RecordAuditEventCommand("a", "b", "c", null, "corr-x", null);

        await Should.ThrowAsync<InvalidOperationException>(() =>
            handler.Handle(command, CancellationToken.None));

        logger.Entries.ShouldContain(e =>
            e.Level == LogLevel.Error &&
            e.Exception is InvalidOperationException);
    }

    [Fact]
    public async Task Handle_ThrowsOnBlankActor()
    {
        var repo = new FakeAuditEventRepository();
        var handler = new RecordAuditEventHandler(repo, new PassthroughPiiRedactor(), NullLogger<RecordAuditEventHandler>.Instance);

        var command = new RecordAuditEventCommand("", "create", "order", null, null, null);

        await Should.ThrowAsync<ArgumentException>(() =>
            handler.Handle(command, CancellationToken.None));

        repo.Stored.ShouldBeEmpty();
    }

    [Fact]
    public async Task Handle_RedactsActorInSuccessLog_WhenMaskingRedactorUsed()
    {
        var repo = new FakeAuditEventRepository();
        var logger = new RecordingLogger<RecordAuditEventHandler>();
        var handler = new RecordAuditEventHandler(repo, new MaskingPiiRedactor(), logger);

        await handler.Handle(
            new RecordAuditEventCommand("alice@example.com", "login", "auth", null, null, null),
            CancellationToken.None);

        var info = logger.Entries.First(e => e.Level == LogLevel.Information);
        info.Message.ShouldNotContain("alice@example.com");
        info.Message.ShouldContain("a***@e***");
    }
}
