using Cortex.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cortex.Infrastructure.Persistence.Configurations;

public class OrganizationInvitationConfiguration : IEntityTypeConfiguration<OrganizationInvitation>
{
    public void Configure(EntityTypeBuilder<OrganizationInvitation> builder)
    {
        builder.ToTable("organization_invitations");

        builder.HasKey(i => i.Id);

        builder.Property(i => i.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(i => i.OrganizationId)
            .HasColumnName("organization_id")
            .IsRequired();

        builder.Property(i => i.Email)
            .HasColumnName("email")
            .HasMaxLength(254)
            .IsRequired();

        builder.HasIndex(i => new { i.OrganizationId, i.Email })
            .IsUnique();

        builder.Property(i => i.Role)
            .HasColumnName("role")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(i => i.TokenHash)
            .HasColumnName("token_hash")
            .HasMaxLength(128)
            .IsRequired();

        builder.HasIndex(i => i.TokenHash)
            .IsUnique();

        builder.Property(i => i.InvitedByUserId)
            .HasColumnName("invited_by_user_id")
            .IsRequired();

        builder.Property(i => i.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(i => i.ExpiresAt)
            .HasColumnName("expires_at")
            .IsRequired();

        // Maps to the xmin system column of PostgreSQL so one link cannot be accepted twice at once.
        builder.Property<uint>("Version")
            .IsRowVersion();

        builder.HasOne(i => i.Organization)
            .WithMany()
            .HasForeignKey(i => i.OrganizationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(i => i.InvitedBy)
            .WithMany()
            .HasForeignKey(i => i.InvitedByUserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
