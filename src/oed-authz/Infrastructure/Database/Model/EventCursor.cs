namespace oed_authz.Infrastructure.Database.Model;

public class EventCursor
{
    public string EstateSsn { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public DateTimeOffset LastTimestampProcessed { get; set; } 
}