using Microsoft.EntityFrameworkCore;
using oed_authz.Infrastructure.Database;
using oed_authz.Infrastructure.Database.Model;
using oed_authz.Interfaces;

namespace oed_authz.Repositories;

public class EventCursorRepository(OedAuthzDbContext dbContext) : IEventCursorRepository
{
    public async Task<EventCursor?> GetEventCursorForUpdate(string estateSsn, string eventType)
    {
        var eventCursor = await dbContext.Set<EventCursor>()
            .FromSql($"""
                      SELECT *
                      FROM oedauthz.eventcursor 
                      WHERE 
                        "estateSsn" = {estateSsn}
                        AND "eventType" = {eventType}
                      FOR UPDATE LIMIT 1
                      """)
            .SingleOrDefaultAsync();

        return eventCursor;
    }

    public async Task AddEventCursor(EventCursor cursor)
    {
        await dbContext.AddAsync(cursor);
    }
}