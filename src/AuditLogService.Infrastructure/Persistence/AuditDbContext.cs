using AuditLogService.Domain.Entities;
using AuditLogService.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;

namespace AuditLogService.Infrastructure.Persistence;

public sealed class AuditDbContext(DbContextOptions<AuditDbContext> options) : DbContext(options)
{
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new AuditEventConfiguration());
    }
}
