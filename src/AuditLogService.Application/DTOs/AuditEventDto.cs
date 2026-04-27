namespace AuditLogService.Application.DTOs;

public sealed record AuditEventDto(
    Guid Id,
    DateTime Timestamp,
    string Actor,
    string Action,
    string Resource,
    string? ResourceId,
    string? CorrelationId,
    IReadOnlyDictionary<string, string> Metadata);
