namespace Claims.Domain.Covers;

public readonly record struct CoverId(Guid Value)
{
    public static CoverId New() => new(Guid.CreateVersion7());

    public override string ToString() => Value.ToString();
}
