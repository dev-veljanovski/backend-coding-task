namespace Claims.Domain.Covers;

public interface ICoverRepository
{
    Task<Cover?> GetByIdAsync(CoverId id, CancellationToken cancellationToken);

    void Add(Cover cover);

    void Remove(Cover cover);
}
