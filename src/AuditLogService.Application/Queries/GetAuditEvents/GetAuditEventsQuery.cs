using AuditLogService.Application.DTOs;
using MediatR;

namespace AuditLogService.Application.Queries.GetAuditEvents;

public sealed record GetAuditEventsQuery(
    string? Actor = null,
    string? Action = null,
    string? Resource = null,
    string? CorrelationId = null,
    DateTime? FromUtc = null,
    DateTime? ToUtc = null,
    int PageSize = 50,
    int Page = 1) : IRequest<IReadOnlyList<AuditEventDto>>;
