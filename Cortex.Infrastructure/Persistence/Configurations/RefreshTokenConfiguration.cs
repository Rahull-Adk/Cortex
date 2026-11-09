using Cortex.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cortex.Infrastructure.Persistence.Configurations;

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("refresh_tokens");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(t => t.UserId)
            .HasColumnName("user_id")
            .IsRequired();

        builder.Property(t => t.FamilyId)
            .HasColumnName("family_id")
            .IsRequired();

        builder.Property(t => t.TokenHash)
            .HasColumnName("token_hash")
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(t => t.ExpiresAt)
            .HasColumnName("expires_at")
            .IsRequired();

        builder.Property(t => t.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(t => t.RevokedAt)
            .HasColumnName("revoked_at");

        builder.Property(t => t.ReplacedByTokenId)
            .HasColumnName("replaced_by_token_id");

        // Maps to PostgreSQL's xmin system column for optimistic concurrency.
        builder.Property<uint>("Version")
            .IsRowVersion();

        builder.Ignore(t => t.IsExpired);
        builder.Ignore(t => t.IsRevoked);
        builder.Ignore(t => t.IsActive);

        builder.HasIndex(t => t.TokenHash)
            .IsUnique();

        builder.HasIndex(t => t.FamilyId);

        builder.HasIndex(t => t.UserId);

        builder.HasOne(t => t.User)
            .WithMany()
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
