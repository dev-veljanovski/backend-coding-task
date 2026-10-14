using Claims.Application.Claims;
using Claims.Application.Covers;
using Claims.Domain.Claims;
using Claims.Domain.Covers;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Claims.Infrastructure.Persistence.Documents;

internal sealed class ClaimDocument
{
    [BsonId]
    public required string Id { get; init; }

    public required string CoverId { get; init; }

    public required string Name { get; init; }

    public required ClaimType Type { get; init; }

    [BsonRepresentation(BsonType.Decimal128)]
    public required decimal DamageCost { get; init; }

    public required DateTime Created { get; init; }

    public static ClaimDocument FromDomain(Claim claim) => new()
    {
        Id = claim.Id.Value.ToString(),
        CoverId = claim.CoverId.Value.ToString(),
        Name = claim.Name,
        Type = claim.Type,
        DamageCost = claim.DamageCost.Amount,
        Created = DateMapping.ToStorage(claim.Created),
    };

    public Claim ToDomain() => Claim.Rehydrate(
        new ClaimId(Guid.Parse(Id)),
        new CoverId(Guid.Parse(CoverId)),
        Name,
        Type,
        Domain.Claims.DamageCost.Rehydrate(DamageCost),
        DateMapping.FromStorage(Created));

    public ClaimResponse ToResponse() => new(
        Guid.Parse(Id),
        Guid.Parse(CoverId),
        Name,
        Type,
        DamageCost,
        DateMapping.FromStorage(Created));
}

internal sealed class CoverDocument
{
    [BsonId]
    public required string Id { get; init; }

    public required DateTime StartDate { get; init; }

    public required DateTime EndDate { get; init; }

    public required CoverType Type { get; init; }

    [BsonRepresentation(BsonType.Decimal128)]
    public required decimal Premium { get; init; }

    public static CoverDocument FromDomain(Cover cover) => new()
    {
        Id = cover.Id.Value.ToString(),
        StartDate = DateMapping.ToStorage(cover.Period.StartDate),
        EndDate = DateMapping.ToStorage(cover.Period.EndDate),
        Type = cover.Type,
        Premium = cover.Premium,
    };

    public Cover ToDomain() => Cover.Rehydrate(
        new CoverId(Guid.Parse(Id)),
        InsurancePeriod.Rehydrate(DateMapping.FromStorage(StartDate), DateMapping.FromStorage(EndDate)),
        Type,
        Premium);

    public CoverResponse ToResponse() => new(
        Guid.Parse(Id),
        DateMapping.FromStorage(StartDate),
        DateMapping.FromStorage(EndDate),
        Type,
        Premium);
}

internal static class DateMapping
{
    public static DateTime ToStorage(DateOnly date) =>
        DateTime.SpecifyKind(date.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);

    public static DateOnly FromStorage(DateTime value) =>
        DateOnly.FromDateTime(value.Kind == DateTimeKind.Local ? value.ToUniversalTime() : value);
}
