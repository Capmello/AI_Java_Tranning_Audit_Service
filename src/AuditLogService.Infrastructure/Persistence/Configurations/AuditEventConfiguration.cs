using AuditLogService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AuditLogService.Infrastructure.Persistence.Configurations;

public sealed class AuditEventConfiguration : IEntityTypeConfiguration<AuditEvent>
{
    public void Configure(EntityTypeBuilder<AuditEvent> builder)
    {
        builder.ToTable("audit_events");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(e => e.Timestamp)
            .HasColumnName("timestamp")
            .IsRequired();

        builder.Property(e => e.Actor)
            .HasColumnName("actor")
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(e => e.Action)
            .HasColumnName("action")
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(e => e.Resource)
            .HasColumnName("resource")
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(e => e.ResourceId)
            .HasColumnName("resource_id")
            .HasMaxLength(256);

        builder.Property(e => e.CorrelationId)
            .HasColumnName("correlation_id")
            .HasMaxLength(128);

        builder.Property(e => e.Metadata)
            .HasColumnName("metadata")
            .HasColumnType("jsonb")
            .IsRequired();

        // Optimized for append-only reads by timestamp descending
        builder.HasIndex(e => e.Timestamp).HasDatabaseName("ix_audit_events_timestamp");
        builder.HasIndex(e => e.Actor).HasDatabaseName("ix_audit_events_actor");
        builder.HasIndex(e => e.CorrelationId).HasDatabaseName("ix_audit_events_correlation_id");
    }
}
