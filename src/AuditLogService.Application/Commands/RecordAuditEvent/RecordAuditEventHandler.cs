using AuditLogService.Application.DTOs;
using AuditLogService.Application.Logging;
using AuditLogService.Domain.Entities;
using AuditLogService.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AuditLogService.Application.Commands.RecordAuditEvent;

public sealed class RecordAuditEventHandler(
    IAuditEventRepository repository,
    IPiiRedactor redactor,
    ILogger<RecordAuditEventHandler> logger) : IRequestHandler<RecordAuditEventCommand, AuditEventDto>
{
    public async Task<AuditEventDto> Handle(RecordAuditEventCommand request, CancellationToken ct)
    {
        logger.LogDebug(
            "Recording audit event. Action={Action} Resource={Resource} HasMetadata={HasMetadata}",
            request.Action, request.Resource, request.Metadata is { Count: > 0 });

        var auditEvent = AuditEvent.Record(
            actor:         request.Actor,
            action:        request.Action,
            resource:      request.Resource,
            resourceId:    request.ResourceId,
            correlationId: request.CorrelationId,
            metadata:      request.Metadata,
            clock:         () => DateTime.UtcNow);

        try
        {
            await repository.AppendAsync(auditEvent, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Audit event ingestion failed. Actor={Actor} Action={Action} Resource={Resource} CorrelationId={CorrelationId}",
                redactor.RedactActor(auditEvent.Actor), auditEvent.Action, auditEvent.Resource, auditEvent.CorrelationId);
            throw;
        }

        logger.LogInformation(
            "Audit event recorded. Id={Id} Actor={Actor} Action={Action} Resource={Resource} CorrelationId={CorrelationId}",
            auditEvent.Id, redactor.RedactActor(auditEvent.Actor), auditEvent.Action, auditEvent.Resource, auditEvent.CorrelationId);

        return Map(auditEvent);
    }

    private static AuditEventDto Map(AuditEvent e) => new(
        e.Id, e.Timestamp, e.Actor, e.Action, e.Resource, e.ResourceId, e.CorrelationId, e.Metadata);
}
