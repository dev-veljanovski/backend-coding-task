using Claims.Infrastructure.Outbox;
using Claims.Infrastructure.Persistence.Documents;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Driver;

namespace Claims.Infrastructure.Persistence;

internal sealed class MongoDbContext
{
    public const string ClaimsCollection = "claims";
    public const string CoversCollection = "covers";
    public const string OutboxCollection = "outbox_messages";

    static MongoDbContext() => RegisterConventions();

    public MongoDbContext(IMongoClient client, IOptions<MongoOptions> options)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(options);

        Client = client;
        Database = client.GetDatabase(options.Value.DatabaseName);
        Claims = Database.GetCollection<ClaimDocument>(ClaimsCollection);
        Covers = Database.GetCollection<CoverDocument>(CoversCollection);
        OutboxMessages = Database.GetCollection<OutboxMessage>(OutboxCollection);
    }

    public IMongoClient Client { get; }

    public IMongoDatabase Database { get; }

    public IMongoCollection<ClaimDocument> Claims { get; }

    public IMongoCollection<CoverDocument> Covers { get; }

    public IMongoCollection<OutboxMessage> OutboxMessages { get; }

    private static void RegisterConventions()
    {
        var conventions = new ConventionPack
        {
            new CamelCaseElementNameConvention(),
            new EnumRepresentationConvention(BsonType.String),
            new IgnoreExtraElementsConvention(true),
        };

        ConventionRegistry.Register(
            "Claims.Infrastructure",
            conventions,
            type => type.Namespace?.StartsWith("Claims.Infrastructure", StringComparison.Ordinal) == true);
    }
}
