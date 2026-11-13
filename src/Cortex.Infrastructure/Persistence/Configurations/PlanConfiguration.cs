using Cortex.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cortex.Infrastructure.Persistence.Configurations;

public class PlanConfiguration : IEntityTypeConfiguration<Plan>
{
    private const long Gigabyte = 1024L * 1024 * 1024;

    public void Configure(EntityTypeBuilder<Plan> builder)
    {
        builder.ToTable("plans");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(p => p.Code)
            .HasColumnName("code")
            .HasMaxLength(50)
            .IsRequired();

        builder.HasIndex(p => p.Code)
            .IsUnique();

        builder.Property(p => p.Name)
            .HasColumnName("name")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(p => p.PriceMinor)
            .HasColumnName("price_minor")
            .IsRequired();

        builder.Property(p => p.Currency)
            .HasColumnName("currency")
            .HasMaxLength(3)
            .IsRequired();

        builder.Property(p => p.AiAnswersPerPeriod)
            .HasColumnName("ai_answers_per_period")
            .IsRequired();

        builder.Property(p => p.MaxDocuments)
            .HasColumnName("max_documents")
            .IsRequired();

        builder.Property(p => p.MaxStorageBytes)
            .HasColumnName("max_storage_bytes")
            .IsRequired();

        builder.Property(p => p.SortOrder)
            .HasColumnName("sort_order")
            .IsRequired();

        builder.Property(p => p.IsActive)
            .HasColumnName("is_active")
            .IsRequired()
            .HasDefaultValue(true);

        builder.Ignore(p => p.IsPaid);

        // Prices are in satang. Limits can be edited in the table without a code change.
        builder.HasData(
            Seed("7b1f6f0e-5c0a-4b7e-9d53-0d7c5a1e0001", Plan.FreeCode, "Free", 0, 0, 0, 0, 0),
            Seed("7b1f6f0e-5c0a-4b7e-9d53-0d7c5a1e0002", "starter", "Starter", 99_000, 500, 100, 2 * Gigabyte, 1),
            Seed("7b1f6f0e-5c0a-4b7e-9d53-0d7c5a1e0003", "growth", "Growth", 249_000, 2_500, 500, 10 * Gigabyte, 2));
    }

    private static object Seed(
        string id, string code, string name, long priceMinor, int aiAnswers, int maxDocuments, long maxStorageBytes, int sortOrder) =>
        new
        {
            Id = Guid.Parse(id),
            Code = code,
            Name = name,
            PriceMinor = priceMinor,
            Currency = "thb",
            AiAnswersPerPeriod = aiAnswers,
            MaxDocuments = maxDocuments,
            MaxStorageBytes = maxStorageBytes,
            SortOrder = sortOrder,
            IsActive = true,
        };
}
