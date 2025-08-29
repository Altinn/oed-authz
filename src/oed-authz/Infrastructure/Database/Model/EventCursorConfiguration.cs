using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace oed_authz.Infrastructure.Database.Model;

public class EventCursorConfiguration : IEntityTypeConfiguration<EventCursor>
{
    public void Configure(EntityTypeBuilder<EventCursor> builder)
    {
        builder.ToTable("eventcursor", "oedauthz");

        builder
            .HasKey(e => new { e.EstateSsn, e.EventType })
            .HasName("eventcursor_pkey");

        builder.Property(e => e.EstateSsn)
            .HasMaxLength(11)
            .IsFixedLength()
            .HasColumnName("estateSsn");

        builder.Property(e => e.EventType)
            .HasMaxLength(255)
            .HasColumnName("eventType");

        builder.Property(e => e.LastTimestampProcessed)
            .HasColumnName("lastTimestampProcessed");
    }
}