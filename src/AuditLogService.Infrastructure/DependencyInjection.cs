using AuditLogService.Domain.Repositories;
using AuditLogService.Infrastructure.BackgroundJobs;
using AuditLogService.Infrastructure.Persistence;
using AuditLogService.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace AuditLogService.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddSingleton<NpgsqlDataSource>(sp =>
        {
            var cs = sp.GetRequiredService<IConfiguration>().GetConnectionString("Default");
            var builder = new NpgsqlDataSourceBuilder(cs);
            builder.EnableDynamicJson();
            return builder.Build();
        });

        services.AddDbContext<AuditDbContext>((sp, opts) =>
            opts.UseNpgsql(sp.GetRequiredService<NpgsqlDataSource>()));

        services.AddScoped<IAuditEventRepository, AuditEventRepository>();
        services.AddHostedService<RetentionArchivalJob>();

        return services;
    }
}
