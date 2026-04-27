using AuditLogService.Domain.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;

namespace AuditLogService.Infrastructure.BackgroundJobs;

/// <summary>
/// Runs daily and deletes audit events older than the configured retention period.
/// </summary>
public sealed class RetentionArchivalJob(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<RetentionArchivalJob> logger) : BackgroundService
{
    private readonly int _retentionDays = int.TryParse(configuration["RetentionDays"], out var d) ? d : 90;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("RetentionArchivalJob started. RetentionDays={RetentionDays}", _retentionDays);

        while (!stoppingToken.IsCancellationRequested)
        {
            await RunRetentionAsync(stoppingToken);
            await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
        }
    }

    private async Task RunRetentionAsync(CancellationToken ct)
    {
        try
        {
            var cutoff = DateTime.UtcNow.AddDays(-_retentionDays);
            logger.LogInformation("Running retention archival. Cutoff={Cutoff:O}", cutoff);

            await using var scope = scopeFactory.CreateAsyncScope();
            var repository = scope.ServiceProvider.GetRequiredService<IAuditEventRepository>();
            var deleted = await repository.DeleteOlderThanAsync(cutoff, ct);

            logger.LogInformation("Retention archival complete. Deleted={Deleted}", deleted);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Retention archival job failed.");
        }
    }
}
