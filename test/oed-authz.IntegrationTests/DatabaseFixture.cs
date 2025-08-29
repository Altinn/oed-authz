using Microsoft.EntityFrameworkCore;
using oed_authz.Infrastructure.Database;
using oed_authz.Infrastructure.Database.Model;
using oed_authz.Services;
using Testcontainers.PostgreSql;

namespace oed_authz.IntegrationTests;

public class DatabaseFixture : IAsyncLifetime
{
    private long _ssn;
    private readonly PostgreSqlContainer _container =
        new PostgreSqlBuilder()
            .Build();

    public readonly string PreSeededEstateSsn = "12345678901";
    public readonly string PreSeededEventType = Events.Oed.CaseStatusUpdateValidated;

    public string ConnectionString => _container.GetConnectionString();

    // Will return a unique fake Ssn for each call, allowing the unittests to be run in parallell with a saparate EstateSsn
    public string NextSsn => Interlocked.Increment(ref _ssn).ToString().PadLeft(11, '0');

    public string ContainerId => $"{_container.Id}";
    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        await using var initDbContext = new OedAuthzDbContext(
            new DbContextOptionsBuilder<OedAuthzDbContext>()
                .UseNpgsql(ConnectionString)
                .Options);

        await initDbContext.Database.MigrateAsync();

        await initDbContext.Set<EventCursor>().AddAsync(
            new EventCursor
            {
                EstateSsn = PreSeededEstateSsn,
                EventType = PreSeededEventType,
                LastTimestampProcessed = new DateTimeOffset(2025, 8, 1, 18, 0, 0, 0, TimeSpan.Zero)
            });

        await initDbContext.SaveChangesAsync();
    }

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();
}