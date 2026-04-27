using AuditLogService.Domain.Entities;
using AuditLogService.Domain.Repositories;

namespace AuditLogService.Application.Tests.Fakes;

internal sealed class FakeAuditEventRepository : IAuditEventRepository
{
    public List<AuditEvent> Stored { get; } = new();
    public AuditEventFilter? LastFilter { get; private set; }
    public Func<AuditEvent, Task>? OnAppend { get; set; }

    public Task AppendAsync(AuditEvent auditEvent, CancellationToken ct = default)
    {
        if (OnAppend is not null)
            return OnAppend(auditEvent);

        Stored.Add(auditEvent);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<AuditEvent>> QueryAsync(AuditEventFilter filter, CancellationToken ct = default)
    {
        LastFilter = filter;
        IReadOnlyList<AuditEvent> snapshot = Stored.ToList();
        return Task.FromResult(snapshot);
    }

    public Task<int> DeleteOlderThanAsync(DateTime cutoffUtc, CancellationToken ct = default)
    {
        var removed = Stored.RemoveAll(e => e.Timestamp < cutoffUtc);
        return Task.FromResult(removed);
    }
}
