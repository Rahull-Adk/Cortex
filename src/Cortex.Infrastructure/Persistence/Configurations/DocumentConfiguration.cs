using Cortex.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cortex.Infrastructure.Persistence.Configurations;

public class DocumentConfiguration : IEntityTypeConfiguration<Document>
{
    public void Configure(EntityTypeBuilder<Document> builder)
    {
        builder.ToTable("documents");

        builder.HasKey(d => d.Id);

        builder.Property(d => d.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(d => d.OrganizationId)
            .HasColumnName("organization_id")
            .IsRequired();

        builder.Property(d => d.UploadedByUserId)
            .HasColumnName("uploaded_by_user_id")
            .IsRequired();

        builder.Property(d => d.FileName)
            .HasColumnName("file_name")
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(d => d.ContentType)
            .HasColumnName("content_type")
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(d => d.SizeBytes)
            .HasColumnName("size_bytes")
            .IsRequired();

        builder.Property(d => d.S3Key)
            .HasColumnName("s3_key")
            .HasMaxLength(1024)
            .IsRequired();

        builder.HasIndex(d => d.S3Key)
            .IsUnique();

        builder.Property(d => d.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(d => d.Error)
            .HasColumnName("error")
            .HasMaxLength(2000);

        builder.Property(d => d.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(d => d.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        builder.HasIndex(d => new { d.OrganizationId, d.CreatedAt });

        // Lets the cleanup job find stale AwaitingUpload rows without scanning every tenant.
        builder.HasIndex(d => new { d.Status, d.CreatedAt });

        // Maps to xmin so a user delete and an ingestion result cannot overwrite each other.
        builder.Property<uint>("Version")
            .IsRowVersion();

        builder.HasOne<Organization>()
            .WithMany()
            .HasForeignKey(d => d.OrganizationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(d => d.UploadedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
