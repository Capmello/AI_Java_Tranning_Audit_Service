namespace AuditLogService.Domain.Repositories;

public sealed record AuditEventFilter(
    string? Actor = null,
    string? Action = null,
    string? Resource = null,
    string? CorrelationId = null,
    DateTime? FromUtc = null,
    DateTime? ToUtc = null,
    int PageSize = 50,
    int Page = 1);
