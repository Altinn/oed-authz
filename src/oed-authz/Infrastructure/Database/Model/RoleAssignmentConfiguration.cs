using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using oed_authz.Models;

namespace oed_authz.Infrastructure.Database.Model;

public class RoleAssignmentConfiguration : IEntityTypeConfiguration<RoleAssignment>
{
    public void Configure(EntityTypeBuilder<RoleAssignment> builder)
    {
        builder.ToTable("roleassignments", "oedauthz");

        builder.HasKey(e => e.Id)
            .HasName("roleassignments_pkey");

        builder.HasIndex(e => new { e.EstateSsn, e.RecipientSsn, e.RoleCode, e.HeirSsn }, "constraint_roleassignment")
            .IsUnique();

        builder.HasIndex(e => e.Created, "idx_created");
        builder.HasIndex(e => e.EstateSsn, "idx_estatessn");
        builder.HasIndex(e => e.RecipientSsn, "idx_recipientSsn");
        builder.HasIndex(e => e.RoleCode, "idx_roleCode");

        builder.HasIndex(e => new { e.EstateSsn, e.RecipientSsn, e.RoleCode }, "idx_unique_collective_roleassignments")
            .IsUnique()
            .HasFilter("(\"heirSsn\" IS NULL)");

        builder.Property(e => e.Id)
            .HasColumnName("id")
            .HasColumnType("bigint");

        builder.Property(e => e.Created)
            .HasColumnName("created")
            .HasColumnType("timestamp with time zone");

        builder.Property(e => e.EstateSsn)
            .HasMaxLength(11)
            .IsFixedLength()
            .HasColumnName("estateSsn");

        builder.Property(e => e.HeirSsn)
            .HasMaxLength(11)
            .IsFixedLength()
            .HasColumnName("heirSsn");

        builder.Property(e => e.RecipientSsn)
            .HasMaxLength(11)
            .IsFixedLength()
            .HasColumnName("recipientSsn");

        builder.Property(e => e.RoleCode)
            .HasMaxLength(60)
            .HasColumnName("roleCode");

        builder.Property(e => e.Justification)
            .IsRequired(false)
            .HasMaxLength(255)
            .HasColumnName("justification");
    }
}