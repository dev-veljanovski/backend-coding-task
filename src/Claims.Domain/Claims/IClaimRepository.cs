namespace Claims.Domain.Claims;

public interface IClaimRepository
{
    Task<Claim?> GetByIdAsync(ClaimId id, CancellationToken cancellationToken);

    void Add(Claim claim);

    void Remove(Claim claim);
}
