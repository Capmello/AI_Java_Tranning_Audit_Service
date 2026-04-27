namespace AuditLogService.Application.Logging;

public sealed class MaskingPiiRedactor : IPiiRedactor
{
    public string RedactActor(string? actor)
    {
        if (string.IsNullOrWhiteSpace(actor))
            return "***";

        var atIdx = actor.IndexOf('@');
        if (atIdx > 0)
        {
            var user = actor[..atIdx];
            var domain = actor[(atIdx + 1)..];
            return $"{Mask(user)}@{Mask(domain)}";
        }

        return Mask(actor);
    }

    private static string Mask(string value) =>
        value.Length <= 1 ? "***" : $"{value[0]}***";
}
