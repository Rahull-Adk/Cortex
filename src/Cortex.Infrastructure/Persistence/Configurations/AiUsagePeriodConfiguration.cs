using Cortex.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cortex.Infrastructure.Persistence.Configurations;

public class AiUsagePeriodConfiguration : IEntityTypeConfiguration<AiUsagePeriod>
{
    public void Configure(EntityTypeBuilder<AiUsagePeriod> builder)
    {
        builder.ToTable("ai_usage_periods");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(p => p.OrganizationId)
            .HasColumnName("organization_id")
            .IsRequired();

        builder.Property(p => p.PeriodStart)
            .HasColumnName("period_start")
            .IsRequired();

        builder.HasIndex(p => new { p.OrganizationId, p.PeriodStart })
            .IsUnique();

        builder.Property(p => p.PeriodEnd)
            .HasColumnName("period_end")
            .IsRequired();

        builder.Property(p => p.AnswersUsed)
            .HasColumnName("answers_used")
            .IsRequired();

        builder.HasOne<Organization>()
            .WithMany()
            .HasForeignKey(p => p.OrganizationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
