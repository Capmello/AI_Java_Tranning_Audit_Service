namespace AuditLogService.Domain.Entities;

public sealed class AuditEvent
{
    public Guid Id { get; private set; }
    public DateTime Timestamp { get; private set; }   // always UTC, server-assigned
    public string Actor { get; private set; }
    public string Action { get; private set; }
    public string Resource { get; private set; }
    public string? ResourceId { get; private set; }
    public string? CorrelationId { get; private set; }
    public IReadOnlyDictionary<string, string> Metadata { get; private set; }

#pragma warning disable CS8618  // EF Core materialisation — properties set via object initialiser in Record()
    private AuditEvent() { }
#pragma warning restore CS8618

    public static AuditEvent Record(
        string actor,
        string action,
        string resource,
        string? resourceId,
        string? correlationId,
        Dictionary<string, string>? metadata,
        Func<DateTime> clock)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actor);
        ArgumentException.ThrowIfNullOrWhiteSpace(action);
        ArgumentException.ThrowIfNullOrWhiteSpace(resource);

        return new AuditEvent
        {
            Id            = Guid.NewGuid(),
            Timestamp     = DateTime.SpecifyKind(clock(), DateTimeKind.Utc),
            Actor         = actor,
            Action        = action,
            Resource      = resource,
            ResourceId    = resourceId,
            CorrelationId = correlationId,
            Metadata      = metadata ?? new Dictionary<string, string>()
        };
    }
}
