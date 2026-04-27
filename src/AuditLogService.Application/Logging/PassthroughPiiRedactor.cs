namespace AuditLogService.Application.Logging;

public sealed class PassthroughPiiRedactor : IPiiRedactor
{
    public string RedactActor(string? actor) => actor ?? string.Empty;
}
