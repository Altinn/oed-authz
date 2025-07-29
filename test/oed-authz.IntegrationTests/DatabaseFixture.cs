using Testcontainers.PostgreSql;

namespace oed_authz.IntegrationTests;

public class DatabaseFixture : IAsyncLifetime
{
    private long _ssn;
    private readonly PostgreSqlContainer _container =
        new PostgreSqlBuilder()
            .Build();

    public string ConnectionString => _container.GetConnectionString();

    // Will return a unique fake Ssn for each call, allowing the unittests to be run in parallell with a saparate EstateSsn
    public string NextSsn => Interlocked.Increment(ref _ssn).ToString().PadLeft(11, '0');

    public string ContainerId => $"{_container.Id}";
    public Task InitializeAsync() => _container.StartAsync();
    public Task DisposeAsync() => _container.DisposeAsync().AsTask();
}