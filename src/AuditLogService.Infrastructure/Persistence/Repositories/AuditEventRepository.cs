using AuditLogService.Domain.Entities;
using AuditLogService.Domain.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AuditLogService.Infrastructure.Persistence.Repositories;

public sealed class AuditEventRepository(
    AuditDbContext db,
    ILogger<AuditEventRepository> logger) : IAuditEventRepository
{
    public async Task AppendAsync(AuditEvent auditEvent, CancellationToken ct = default)
    {
        logger.LogDebug("Appending audit event {Id}.", auditEvent.Id);
        db.AuditEvents.Add(auditEvent);
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<AuditEvent>> QueryAsync(AuditEventFilter filter, CancellationToken ct = default)
    {
        logger.LogDebug(
            "Repository query. Page={Page} PageSize={PageSize} HasActorFilter={HasActor}",
            filter.Page, filter.PageSize, filter.Actor is not null);

        var query = db.AuditEvents.AsNoTracking();

        if (filter.Actor is not null)
            query = query.Where(e => e.Actor == filter.Actor);

        if (filter.Action is not null)
            query = query.Where(e => e.Action == filter.Action);

        if (filter.Resource is not null)
            query = query.Where(e => e.Resource == filter.Resource);

        if (filter.CorrelationId is not null)
            query = query.Where(e => e.CorrelationId == filter.CorrelationId);

        if (filter.FromUtc.HasValue)
            query = query.Where(e => e.Timestamp >= filter.FromUtc.Value);

        if (filter.ToUtc.HasValue)
            query = query.Where(e => e.Timestamp <= filter.ToUtc.Value);

        return await query
            .OrderByDescending(e => e.Timestamp)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync(ct);
    }

    public async Task<int> DeleteOlderThanAsync(DateTime cutoffUtc, CancellationToken ct = default)
    {
        logger.LogDebug("Deleting audit events older than {Cutoff:O}.", cutoffUtc);
        return await db.AuditEvents
            .Where(e => e.Timestamp < cutoffUtc)
            .ExecuteDeleteAsync(ct);
    }
}
