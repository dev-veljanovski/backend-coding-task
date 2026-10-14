namespace Claims.Domain.Claims;

public readonly record struct ClaimId(Guid Value)
{
    public static ClaimId New() => new(Guid.CreateVersion7());

    public override string ToString() => Value.ToString();
}
