using Claims.Domain.Abstractions;
using Claims.Domain.Claims.Events;
using Claims.Domain.Covers;
using Claims.Domain.Results;

namespace Claims.Domain.Claims;

public sealed class Claim : AggregateRoot<ClaimId>
{
    private Claim(ClaimId id, CoverId coverId, string name, ClaimType type, DamageCost damageCost, DateOnly created)
        : base(id)
    {
        CoverId = coverId;
        Name = name;
        Type = type;
        DamageCost = damageCost;
        Created = created;
    }

    public CoverId CoverId { get; }

    public string Name { get; }

    public ClaimType Type { get; }

    public DamageCost DamageCost { get; }

    public DateOnly Created { get; }

    // Task 2: damage cost limit (see DamageCost) and "created must be within the cover period" are checked here.
    public static Result<Claim> File(Cover cover, string name, ClaimType type, decimal damageCost, DateOnly created)
    {
        ArgumentNullException.ThrowIfNull(cover);

        var errors = new List<Error>();

        if (string.IsNullOrWhiteSpace(name))
        {
            errors.Add(ClaimErrors.NameRequired);
        }

        var cost = DamageCost.Create(damageCost);
        if (cost.IsFailure)
        {
            errors.AddRange(cost.Errors);
        }

        if (!cover.IsActiveOn(created))
        {
            errors.Add(ClaimErrors.CreatedOutsideCoverPeriod(cover.Period));
        }

        if (errors.Count > 0)
        {
            return Result<Claim>.Failure(errors);
        }

        var claim = new Claim(ClaimId.New(), cover.Id, name.Trim(), type, cost.Value, created);
        claim.Raise(new ClaimCreatedDomainEvent(claim.Id.Value, cover.Id.Value));
        return claim;
    }

    public static Claim Rehydrate(
        ClaimId id,
        CoverId coverId,
        string name,
        ClaimType type,
        DamageCost damageCost,
        DateOnly created) =>
        new(id, coverId, name, type, damageCost, created);

    public void Delete() => Raise(new ClaimDeletedDomainEvent(Id.Value));
}
