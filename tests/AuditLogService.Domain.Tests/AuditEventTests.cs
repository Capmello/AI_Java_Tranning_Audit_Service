using AuditLogService.Domain.Entities;
using Shouldly;
using Xunit;

namespace AuditLogService.Domain.Tests;

public sealed class AuditEventTests
{
    private static readonly DateTime FixedUtc = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
    private static DateTime Clock() => FixedUtc;

    [Fact]
    public void Record_AssignsServerTimestamp()
    {
        var evt = AuditEvent.Record("user1", "login", "auth", null, null, null, Clock);

        evt.Timestamp.ShouldBe(FixedUtc);
        evt.Timestamp.Kind.ShouldBe(DateTimeKind.Utc);
    }

    [Fact]
    public void Record_AssignsNewId()
    {
        var evt = AuditEvent.Record("user1", "login", "auth", null, null, null, Clock);

        evt.Id.ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public void Record_SetsRequiredFields()
    {
        var evt = AuditEvent.Record("user1", "delete", "order", "ord-42", "corr-1", null, Clock);

        evt.Actor.ShouldBe("user1");
        evt.Action.ShouldBe("delete");
        evt.Resource.ShouldBe("order");
        evt.ResourceId.ShouldBe("ord-42");
        evt.CorrelationId.ShouldBe("corr-1");
    }

    [Fact]
    public void Record_EmptyMetadata_WhenNullPassed()
    {
        var evt = AuditEvent.Record("user1", "login", "auth", null, null, null, Clock);

        evt.Metadata.ShouldBeEmpty();
    }

    [Fact]
    public void Record_StoresMetadata()
    {
        var meta = new Dictionary<string, string> { ["ip"] = "1.2.3.4" };
        var evt = AuditEvent.Record("user1", "login", "auth", null, null, meta, Clock);

        evt.Metadata.ShouldContainKeyAndValue("ip", "1.2.3.4");
    }

    [Theory]
    [InlineData("", "action", "resource")]
    [InlineData("actor", "", "resource")]
    [InlineData("actor", "action", "")]
    public void Record_ThrowsOnBlankRequiredFields(string actor, string action, string resource)
    {
        Should.Throw<ArgumentException>(() =>
            AuditEvent.Record(actor, action, resource, null, null, null, Clock));
    }

    [Fact]
    public void TwoEvents_HaveDifferentIds()
    {
        var a = AuditEvent.Record("user1", "login", "auth", null, null, null, Clock);
        var b = AuditEvent.Record("user1", "login", "auth", null, null, null, Clock);

        a.Id.ShouldNotBe(b.Id);
    }
}
