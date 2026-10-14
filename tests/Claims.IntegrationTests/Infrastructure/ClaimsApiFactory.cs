using Claims.Infrastructure.Auditing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.MongoDb;
using Testcontainers.MsSql;

namespace Claims.IntegrationTests.Infrastructure;

public sealed class ClaimsApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly MongoDbContainer _mongo = new MongoDbBuilder("mongo:8.0")
        .WithReplicaSet()
        .Build();

    private readonly MsSqlContainer _sql = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest")
        .Build();

    public async ValueTask InitializeAsync()
    {
        await Task.WhenAll(_mongo.StartAsync(), _sql.StartAsync());

        _ = Services;
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await _mongo.DisposeAsync();
        await _sql.DisposeAsync();
    }

    public async Task<T> QueryAuditAsync<T>(Func<AuditDbContext, Task<T>> query)
    {
        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AuditDbContext>();
        db.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
        return await query(db);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:ClaimsDatabase", _mongo.GetConnectionString());
        builder.UseSetting(
            "ConnectionStrings:AuditDatabase",
            new SqlConnectionStringBuilder(_sql.GetConnectionString()) { InitialCatalog = "ClaimsAuditTests" }.ConnectionString);
        builder.UseSetting("MongoDb:DatabaseName", "ClaimsTests");
        builder.UseSetting("Outbox:PollingInterval", "00:00:00.200");
        builder.UseSetting("RateLimiting:PermitLimit", "100000");
    }
}
