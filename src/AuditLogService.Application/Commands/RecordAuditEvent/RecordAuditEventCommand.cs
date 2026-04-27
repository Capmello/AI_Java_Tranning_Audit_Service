using AuditLogService.Application.DTOs;
using MediatR;

namespace AuditLogService.Application.Commands.RecordAuditEvent;

public sealed record RecordAuditEventCommand(
    string Actor,
    string Action,
    string Resource,
    string? ResourceId,
    string? CorrelationId,
    Dictionary<string, string>? Metadata) : IRequest<AuditEventDto>;
