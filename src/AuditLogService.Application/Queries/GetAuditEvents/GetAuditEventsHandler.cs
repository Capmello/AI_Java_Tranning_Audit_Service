using AuditLogService.Application.DTOs;
using AuditLogService.Domain.Entities;
using AuditLogService.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AuditLogService.Application.Queries.GetAuditEvents;

public sealed class GetAuditEventsHandler(
    IAuditEventRepository repository,
    ILogger<GetAuditEventsHandler> logger)
    : IRequestHandler<GetAuditEventsQuery, IReadOnlyList<AuditEventDto>>
{
    public async Task<IReadOnlyList<AuditEventDto>> Handle(GetAuditEventsQuery request, CancellationToken ct)
    {
        logger.LogDebug(
            "Querying audit events. Actor={Actor} Action={Action} Resource={Resource} Page={Page} PageSize={PageSize}",
            request.Actor, request.Action, request.Resource, request.Page, request.PageSize);

        var filter = new AuditEventFilter(
            Actor: request.Actor,
            Action: request.Action,
            Resource: request.Resource,
            CorrelationId: request.CorrelationId,
            FromUtc: request.FromUtc,
            ToUtc: request.ToUtc,
            PageSize: request.PageSize,
            Page: request.Page);

        var events = await repository.QueryAsync(filter, ct);

        logger.LogDebug("Audit-event query returned {Count} rows.", events.Count);

        return events.Select(Map).ToList();
    }

    private static AuditEventDto Map(AuditEvent e) => new(
        e.Id, e.Timestamp, e.Actor, e.Action, e.Resource, e.ResourceId, e.CorrelationId, e.Metadata);
}
