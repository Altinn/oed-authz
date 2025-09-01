using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using oed_authz.Infrastructure.Database;
using oed_authz.Infrastructure.Database.Model;
using oed_authz.Repositories;
using oed_authz.Services;

namespace oed_authz.IntegrationTests.Repositories;

public class EventCursorRepositoryTests(DatabaseFixture databaseFixture)
    : IClassFixture<DatabaseFixture>, IAsyncLifetime
{
    private readonly OedAuthzDbContext _dbContext = new(
        new DbContextOptionsBuilder<OedAuthzDbContext>()
            .UseNpgsql(databaseFixture.ConnectionString)
            .Options);

    [Fact]
    public async Task EventCursor_EnsureUniqueConstraint()
    {
        // Arrange

        // Creating a cursor with same estateSsn and eventType as the one from seed data in InitializeAsync
        var cursor = new EventCursor
        {
            EstateSsn = "12345678901",
            EventType = Events.Oed.CaseStatusUpdateValidated,
            LastTimestampProcessed = new DateTimeOffset(2025, 8, 1, 19, 0, 0, 0, TimeSpan.Zero)
        };

        // Act
        await _dbContext.Set<EventCursor>().AddAsync(cursor);
        
        //await _dbContext.SaveChangesAsync();
        var act = async () => await _dbContext.SaveChangesAsync();

        // Assert
        (await act.Should().ThrowAsync<DbUpdateException>())
            .WithInnerException<PostgresException>()
            .And.Message.Should().StartWith("23505");
    }


    [Fact]
    public async Task GetEventCursorForUpdate_EnsureRowLock()
    {
        // Arrange
        await using var anotherDbContext = new OedAuthzDbContext(
            new DbContextOptionsBuilder<OedAuthzDbContext>()
                .UseNpgsql(databaseFixture.ConnectionString)
                .Options);

        var anotherRepo = new EventCursorRepository(anotherDbContext);
        
        // Start another transaction and fetch the event cursor. This should cause a row-lock on the given cursor.
        // As long as we do not release this lock by ending the transaction (commit/rollback),
        // we should see a lock timeout when trying to fetch it again in the sut transaction below
        await using var anotherTransaction = await anotherDbContext.Database.BeginTransactionAsync();
        var anotherCursor = await anotherRepo.GetEventCursorForUpdate(
            databaseFixture.PreSeededEstateSsn, 
            databaseFixture.PreSeededEventType);

        // Verify preconditions
        anotherCursor.Should().NotBeNull();

        var sut = new EventCursorRepository(_dbContext);

        // Act
        await using var sutTransaction = await _dbContext.Database.BeginTransactionAsync();
        await _dbContext.Database.ExecuteSqlAsync($"SET LOCAL lock_timeout = '10ms'");

        var act = async () => await sut.GetEventCursorForUpdate(
            databaseFixture.PreSeededEstateSsn,
            databaseFixture.PreSeededEventType);

        // Assert
        (await act.Should().ThrowAsync<InvalidOperationException>())
            .WithInnerException<PostgresException>()
            .And.Message.Should().StartWith("55P03");
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync() => Task.CompletedTask;
}