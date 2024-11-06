using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Npgsql;
using oed_authz.Interfaces;
using oed_authz.Models;
using oed_authz.Settings;

namespace oed_authz.Services;
public class OedRoleRepositoryService : IOedRoleRepositoryService
{
    private readonly ILogger<OedRoleRepositoryService> _logger;
    private readonly NpgsqlDataSourceBuilder _dataSourceBuilder;
    private NpgsqlDataSource? _dataSource;

    public OedRoleRepositoryService(IOptions<Secrets> connectionStrings, ILogger<OedRoleRepositoryService> logger)
    {
        _logger = logger;
        _dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionStrings.Value.PostgreSqlUserConnectionString);
    }

    ~OedRoleRepositoryService()
    {
        _dataSource?.Dispose();
    }

    public async Task<List<RepositoryRoleAssignment>> GetRoleAssignmentsForEstate(string estateSsn, string? filterRecipentSsn = null, string? filterRoleCode = null)
        => await Query(estateSsn, filterRecipentSsn, filterRoleCode);
    
    public async Task<List<RepositoryRoleAssignment>> GetRoleAssignmentsForPerson(string recipientSsn, string? filterEstateSsn = null, string? filterRoleCode = null)
        => await Query(filterEstateSsn, recipientSsn, filterRoleCode);

    public async Task AddRoleAssignment(RepositoryRoleAssignment roleAssignment)
    {
        _logger.LogInformation("Granting role: {RoleAssignment}", JsonSerializer.Serialize(roleAssignment));

        _dataSource ??= _dataSourceBuilder.Build();

        await using var cmd = _dataSource.CreateCommand("INSERT INTO oedauthz.roleassignments (\"estateSsn\", \"recipientSsn\", \"roleCode\", \"heirSsn\", \"created\") VALUES ($1, $2, $3, $4, $5)");
        cmd.Parameters.AddWithValue(roleAssignment.EstateSsn);
        cmd.Parameters.AddWithValue(roleAssignment.RecipientSsn);
        cmd.Parameters.AddWithValue(roleAssignment.RoleCode);
        cmd.Parameters.AddWithValue(roleAssignment.HeirSsn ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue(roleAssignment.Created);

        await cmd.ExecuteNonQueryAsync();
    }

    public async Task RemoveRoleAssignment(RepositoryRoleAssignment roleAssignment)
    {
        _logger.LogInformation("Revoking role: {RoleAssignment}", JsonSerializer.Serialize(roleAssignment));

        _dataSource ??= _dataSourceBuilder.Build();

        // Base query
        StringBuilder query = new StringBuilder("DELETE FROM oedauthz.roleassignments WHERE \"estateSsn\" = $1 AND \"recipientSsn\" = $2 AND \"roleCode\" = $3");

        if (roleAssignment.HeirSsn == null)
        {
            query.Append(" AND \"heirSsn\" IS NULL");
        }
        else
        {
            query.Append(" AND \"heirSsn\" = $4");
        }

        await using var cmd = _dataSource.CreateCommand(query.ToString());
        cmd.Parameters.AddWithValue(roleAssignment.EstateSsn);
        cmd.Parameters.AddWithValue(roleAssignment.RecipientSsn);
        cmd.Parameters.AddWithValue(roleAssignment.RoleCode);

        if (roleAssignment.HeirSsn != null)
        {
            cmd.Parameters.AddWithValue(roleAssignment.HeirSsn);
        }

        await cmd.ExecuteNonQueryAsync();
    }

    private const string BaseSql = $"""
        SELECT "id", "estateSsn", "recipientSsn", "roleCode", "heirSsn", "created"
        FROM oedauthz.roleassignments ra
        WHERE 1=1
        """;

    private async Task<List<RepositoryRoleAssignment>> Query(string? estateSsn, string? recipientSsn, string? roleCode)
    {
        _dataSource ??= _dataSourceBuilder.Build();

        var sqlBuilder = new StringBuilder(BaseSql);
        NpgsqlCommand cmd;

        if (estateSsn != null && recipientSsn != null)
        {
            sqlBuilder.Append(" AND \"estateSsn\" = $1 AND \"recipientSsn\" = $2");
            if (roleCode != null)
            {
                sqlBuilder.Append(" AND \"roleCode\" = $3");
            }
            cmd = _dataSource.CreateCommand(sqlBuilder.ToString());
            cmd.Parameters.AddWithValue(estateSsn);
            cmd.Parameters.AddWithValue(recipientSsn);
            if (roleCode != null)
            {
                cmd.Parameters.AddWithValue(roleCode);
            }
        }
        else if (estateSsn != null)
        {
            sqlBuilder.Append(" AND \"estateSsn\" = $1");
            if (roleCode != null)
            {
                sqlBuilder.Append(" AND \"roleCode\" = $2");
            }
            cmd = _dataSource.CreateCommand(sqlBuilder.ToString());
            cmd.Parameters.AddWithValue(estateSsn);
            if (roleCode != null)
            {
                cmd.Parameters.AddWithValue(roleCode);
            }
        }
        else if (recipientSsn != null)
        {
            sqlBuilder.Append(" AND \"recipientSsn\" = $1");
            if (roleCode != null)
            {
                sqlBuilder.Append(" AND \"roleCode\" = $2");
            }
            cmd = _dataSource.CreateCommand(sqlBuilder.ToString());
            cmd.Parameters.AddWithValue(recipientSsn);
            if (roleCode != null)
            {
                cmd.Parameters.AddWithValue(roleCode);
            }
        }
        else
        {
            throw new ArgumentNullException(nameof(recipientSsn), "Both recipientSsn and estateSsn cannot be null");
        }

        try
        {
            await using var reader = await cmd.ExecuteReaderAsync();

            var roleAssignments = new List<RepositoryRoleAssignment>();
            while (await reader.ReadAsync())
            {
                roleAssignments.Add(new RepositoryRoleAssignment
                {
                    Id = reader.GetInt64(0),
                    EstateSsn = reader.GetString(1),
                    RecipientSsn = reader.GetString(2),
                    RoleCode = reader.GetString(3),
                    HeirSsn = !reader.IsDBNull(4) ? reader.GetString(4) : null,
                    Created = reader.GetDateTime(5)
                });
            }

            return roleAssignments;
        }
        finally
        {
            await cmd.DisposeAsync();
        }
    }
}
