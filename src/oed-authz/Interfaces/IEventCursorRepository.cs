using oed_authz.Infrastructure.Database.Model;

namespace oed_authz.Interfaces;

public interface IEventCursorRepository
{
    /// <summary>
    /// Will fetch cursor and lock the row for update
    /// </summary>
    /// <param name="estateSsn"></param>
    /// <param name="eventType"></param>
    /// <returns></returns>
    public Task<EventCursor?> GetEventCursorForUpdate(string estateSsn, string eventType);
    Task AddEventCursor(EventCursor cursor);
}