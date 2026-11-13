using Cortex.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cortex.Infrastructure.Persistence.Configurations;

public class OrganizationStorageUsageConfiguration : IEntityTypeConfiguration<OrganizationStorageUsage>
{
    public void Configure(EntityTypeBuilder<OrganizationStorageUsage> builder)
    {
        builder.ToTable("organization_storage_usage");

        builder.HasKey(u => u.OrganizationId);

        builder.Property(u => u.OrganizationId)
            .HasColumnName("organization_id")
            .ValueGeneratedNever();

        builder.Property(u => u.DocumentCount)
            .HasColumnName("document_count")
            .IsRequired();

        builder.Property(u => u.StorageBytes)
            .HasColumnName("storage_bytes")
            .IsRequired();

        builder.HasOne<Organization>()
            .WithOne()
            .HasForeignKey<OrganizationStorageUsage>(u => u.OrganizationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
