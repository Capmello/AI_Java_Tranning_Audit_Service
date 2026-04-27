using AuditLogService.Domain.Entities;
using AuditLogService.Infrastructure.Persistence;
using AuditLogService.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace AuditLogService.Infrastructure.Tests;

public sealed class PostgresFixture : IAsyncLifetime
{
    public AuditEventRepository CreateRepository(AuditDbContext ctx) =>
        new(ctx, NullLogger<AuditEventRepository>.Instance);

    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithDatabase("auditlog_test")
        .WithUsername("test")
        .WithPassword("test")
        .Build();

    public NpgsqlDataSource DataSource { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        var dsBuilder = new NpgsqlDataSourceBuilder(_container.GetConnectionString());
        dsBuilder.EnableDynamicJson();
        DataSource = dsBuilder.Build();

        await using var ctx = CreateContext();
        await ctx.Database.MigrateAsync();
    }

    public AuditDbContext CreateContext()
    {
        var opts = new DbContextOptionsBuilder<AuditDbContext>()
            .UseNpgsql(DataSource)
            .Options;
        return new AuditDbContext(opts);
    }

    public async Task DisposeAsync()
    {
        await DataSource.DisposeAsync();
        await _container.DisposeAsync();
    }
}

[CollectionDefinition(nameof(PostgresCollection))]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture> { }
