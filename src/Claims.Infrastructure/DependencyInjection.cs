using Claims.Application.Abstractions.Auditing;
using Claims.Application.Abstractions.Persistence;
using Claims.Application.Claims;
using Claims.Application.Covers;
using Claims.Domain.Claims;
using Claims.Domain.Covers;
using Claims.Infrastructure.Auditing;
using Claims.Infrastructure.HealthChecks;
using Claims.Infrastructure.Messaging;
using Claims.Infrastructure.Messaging.AzureServiceBus;
using Claims.Infrastructure.Outbox;
using Claims.Infrastructure.Persistence;
using Claims.Infrastructure.Persistence.ReadServices;
using Claims.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace Claims.Infrastructure;

public static class DependencyInjection
{
    public const string ReadinessTag = "ready";

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.TryAddSingleton(TimeProvider.System);

        return services
            .AddMongoPersistence(configuration)
            .AddAuditStore(configuration)
            .AddOutbox(configuration)
            .AddMessaging(configuration)
            .AddInfrastructureHealthChecks();
    }

    private static IServiceCollection AddMongoPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<MongoOptions>()
            .Bind(configuration.GetSection(MongoOptions.SectionName))
            .Configure(options => options.ConnectionString =
                configuration.GetConnectionString(MongoOptions.ConnectionStringName) ?? options.ConnectionString)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<IMongoClient>(provider =>
            new MongoClient(provider.GetRequiredService<IOptions<MongoOptions>>().Value.ConnectionString));
        services.AddSingleton<MongoDbContext>();

        services.AddScoped<MongoUnitOfWork>();
        services.AddScoped<IUnitOfWork>(provider => provider.GetRequiredService<MongoUnitOfWork>());

        services.AddScoped<IClaimRepository, MongoClaimRepository>();
        services.AddScoped<ICoverRepository, MongoCoverRepository>();
        services.AddScoped<IClaimReadService, MongoClaimReadService>();
        services.AddScoped<ICoverReadService, MongoCoverReadService>();

        services.AddHostedService<MongoIndexInitializer>();
        return services;
    }

    private static IServiceCollection AddOutbox(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<OutboxOptions>()
            .Bind(configuration.GetSection(OutboxOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<DomainEventSerializer>();
        services.AddSingleton<IOutboxSignal, OutboxSignal>();
        services.AddHostedService<OutboxProcessor>();
        return services;
    }

    private static IServiceCollection AddMessaging(this IServiceCollection services, IConfiguration configuration)
    {
        var section = configuration.GetSection(MessagingOptions.SectionName);
        services.AddOptions<MessagingOptions>()
            .Bind(section)
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<MessagingOptions>, MessagingOptionsValidator>();

        services.AddScoped<IncomingMessageDispatcher>();

        var transport = section.GetValue<MessagingTransport?>(nameof(MessagingOptions.Transport)) ?? MessagingTransport.InProcess;
        switch (transport)
        {
            case MessagingTransport.AzureServiceBus:
                services.AddSingleton(provider => ServiceBusClientFactory.Create(provider.GetRequiredService<IOptions<MessagingOptions>>()));
                services.AddSingleton<IMessageBus, AzureServiceBusMessageBus>();
                services.AddHostedService<AzureServiceBusConsumer>();
                break;

            default:
                services.AddSingleton<IMessageBus, InProcessMessageBus>();
                break;
        }

        return services;
    }

    private static IServiceCollection AddAuditStore(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<AuditDatabaseOptions>()
            .Bind(configuration.GetSection(AuditDatabaseOptions.SectionName));

        services.AddDbContext<AuditDbContext>(options =>
            options.UseSqlServer(
                configuration.GetConnectionString(AuditDbContext.ConnectionStringName),
                sql => sql.EnableRetryOnFailure()));

        services.AddScoped<IAuditTrail, SqlAuditTrail>();

        services.AddHostedService<AuditDatabaseInitializer>();
        return services;
    }

    private static IServiceCollection AddInfrastructureHealthChecks(this IServiceCollection services)
    {
        services.AddHealthChecks()
            .AddCheck<MongoHealthCheck>("mongodb", tags: [ReadinessTag])
            .AddCheck<AuditDatabaseHealthCheck>("audit-database", tags: [ReadinessTag])
            .AddCheck<OutboxHealthCheck>("audit-outbox", tags: [ReadinessTag]);

        return services;
    }
}
