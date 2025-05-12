using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace oed_authz.Infrastructure.Database.Model
{
    public class RoleAssignmentLogConfiguration : IEntityTypeConfiguration<RoleAssignmentLog>
    {
        public void Configure(EntityTypeBuilder<RoleAssignmentLog> builder)
        {
            builder.ToTable("roleassignments_log", "oedauthz");

            builder.HasKey(e => e.Id)
                .HasName("roleassignments_log_pkey");

            builder.HasIndex(e => e.EstateSsn, "idx_roleassignments_log_estatessn");
            builder.HasIndex(e => e.RecipientSsn, "idx_roleassignments_log_recipientssn");
            builder.HasIndex(e => e.Timestamp, "idx_roleassignments_log_timestamp");

            builder.Property(e => e.Id)
                .HasColumnName("id")
                .HasColumnType("bigint");

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

            builder.Property(e => e.Timestamp)
                .HasColumnName("timestamp")
                .HasColumnType("timestamp with time zone");

            builder.Property(e => e.Action)
                .HasColumnName("action")
                .HasColumnType("oedauthz.roleassignments_action");

            builder.Property(e => e.Justification)
                .IsRequired(false)
                .HasMaxLength(255)
                .HasColumnName("justification");
        }
    }
}
