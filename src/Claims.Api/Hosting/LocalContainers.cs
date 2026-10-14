using Claims.Infrastructure.Auditing;
using Claims.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Testcontainers.MongoDb;
using Testcontainers.MsSql;

namespace Claims.Api.Hosting;

internal static class LocalContainers
{
    private const string MongoImage = "mongo:8.0";
    private const string SqlServerImage = "mcr.microsoft.com/mssql/server:2022-latest";
    private const string AuditDatabaseName = "ClaimsAudit";

    public static async Task AddLocalContainersAsync(this WebApplicationBuilder builder)
    {
        if (!builder.Environment.IsDevelopment() || !builder.Configuration.GetValue<bool>("LocalContainers:Enabled"))
        {
            return;
        }

        var connectionStrings = new Dictionary<string, string?>();

        if (string.IsNullOrWhiteSpace(builder.Configuration.GetConnectionString(MongoOptions.ConnectionStringName)))
        {
            var mongo = new MongoDbBuilder(MongoImage)
                .WithReplicaSet()
                .Build();
            await mongo.StartAsync().ConfigureAwait(false);
            connectionStrings[$"ConnectionStrings:{MongoOptions.ConnectionStringName}"] = mongo.GetConnectionString();
        }

        if (string.IsNullOrWhiteSpace(builder.Configuration.GetConnectionString(AuditDbContext.ConnectionStringName)))
        {
            var sql = new MsSqlBuilder(SqlServerImage)
                .Build();
            await sql.StartAsync().ConfigureAwait(false);
            connectionStrings[$"ConnectionStrings:{AuditDbContext.ConnectionStringName}"] =
                new SqlConnectionStringBuilder(sql.GetConnectionString()) { InitialCatalog = AuditDatabaseName }.ConnectionString;
        }

        builder.Configuration.AddInMemoryCollection(connectionStrings);
    }
}
