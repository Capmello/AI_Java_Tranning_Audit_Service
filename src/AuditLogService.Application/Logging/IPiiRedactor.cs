namespace AuditLogService.Application.Logging;

/// <summary>
/// Masks values that may contain PII before they reach operational logs.
/// Implementations are environment-aware: a no-op in Development, masking in Production.
/// </summary>
public interface IPiiRedactor
{
    string RedactActor(string? actor);
}
