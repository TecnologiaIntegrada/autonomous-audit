using System.Data;
using AutonomousAudit.Application.Data;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace AutonomousAudit.Infrastructure;

public sealed class NpgsqlSqlConnectionFactory : ISqlConnectionFactory
{
    private readonly string _connectionString;

    public NpgsqlSqlConnectionFactory(IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "ConnectionStrings:Default nao configurada. Use user-secrets, appsettings.Development.json, ConnectionStrings__Default ou o Secret api-secret.");
        }

        _connectionString = connectionString;
    }

    public IDbConnection Create()
    {
        var connection = new NpgsqlConnection(_connectionString);
        connection.Open();
        return connection;
    }
}
