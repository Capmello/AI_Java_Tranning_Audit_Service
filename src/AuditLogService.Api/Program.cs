using AuditLogService.Api.Middleware;
using AuditLogService.Application.Commands.RecordAuditEvent;
using AuditLogService.Application.Logging;
using AuditLogService.Infrastructure;
using AuditLogService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((ctx, services, cfg) => cfg
        .ReadFrom.Configuration(ctx.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext());

    builder.Services.AddControllers();
    builder.Services.AddOpenApi();
    builder.Services.AddProblemDetails();
    builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(
        typeof(RecordAuditEventCommand).Assembly));

    builder.Services.AddInfrastructure(builder.Configuration);

    if (builder.Environment.IsDevelopment())
        builder.Services.AddSingleton<IPiiRedactor, PassthroughPiiRedactor>();
    else
        builder.Services.AddSingleton<IPiiRedactor, MaskingPiiRedactor>();

    var app = builder.Build();

    // Auto-apply migrations on startup
    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<AuditDbContext>();
        await db.Database.MigrateAsync();
    }

    if (app.Environment.IsDevelopment())
        app.UseDeveloperExceptionPage();
    else
        app.UseExceptionHandler();

    app.UseStatusCodePages();
    app.UseSerilogRequestLogging();
    app.UseMiddleware<CorrelationIdMiddleware>();

    app.MapOpenApi();
    app.MapScalarApiReference();

    app.UseAuthorization();
    app.MapControllers();

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Host terminated unexpectedly");
    throw;
}
finally
{
    Log.CloseAndFlush();
}

// Partial class for integration-test host access
public partial class Program { }
