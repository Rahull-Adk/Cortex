using Cortex.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cortex.Infrastructure.Persistence.Configurations;

public class OrganizationSubscriptionConfiguration : IEntityTypeConfiguration<OrganizationSubscription>
{
    public void Configure(EntityTypeBuilder<OrganizationSubscription> builder)
    {
        builder.ToTable("organization_subscriptions");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(s => s.OrganizationId)
            .HasColumnName("organization_id")
            .IsRequired();

        builder.HasIndex(s => s.OrganizationId)
            .IsUnique();

        builder.Property(s => s.PlanId)
            .HasColumnName("plan_id")
            .IsRequired();

        builder.Property(s => s.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(s => s.BillingCustomerRef)
            .HasColumnName("billing_customer_ref")
            .HasMaxLength(255);

        builder.Property(s => s.BillingSubscriptionRef)
            .HasColumnName("billing_subscription_ref")
            .HasMaxLength(255);

        builder.HasIndex(s => s.BillingSubscriptionRef);

        builder.Property(s => s.CurrentPeriodStart)
            .HasColumnName("current_period_start");

        builder.Property(s => s.CurrentPeriodEnd)
            .HasColumnName("current_period_end");

        builder.Property(s => s.PaidThroughUtc)
            .HasColumnName("paid_through_utc");

        builder.Property(s => s.CancelAtPeriodEnd)
            .HasColumnName("cancel_at_period_end")
            .IsRequired();

        builder.Property(s => s.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(s => s.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        // Maps to PostgreSQL's xmin system column for optimistic concurrency.
        builder.Property<uint>("Version")
            .IsRowVersion();

        builder.Ignore(s => s.IsLive);

        builder.HasOne<Organization>()
            .WithOne()
            .HasForeignKey<OrganizationSubscription>(s => s.OrganizationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(s => s.Plan)
            .WithMany()
            .HasForeignKey(s => s.PlanId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
