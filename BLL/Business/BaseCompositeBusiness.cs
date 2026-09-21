using DAL.Entity;
using DAL.Repository;

namespace BLL.Business;

public abstract class BaseCompositeBusiness<T, TKey1, TKey2> : ICompositeBusiness<T, TKey1, TKey2>
    where T : class, ICompositeEntity<T, TKey1, TKey2>, new()
    where TKey1 : notnull
    where TKey2 : notnull
{
    protected readonly ICompositeRepository<T, TKey1, TKey2> Repository;

    protected BaseCompositeBusiness(ICompositeRepository<T, TKey1, TKey2> repository)
    {
        Repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public virtual async Task<T?> GetAsync(
        TKey1 key1,
        TKey2 key2,
        string include = "*",
        CancellationToken cancellationToken = default)
    {
        return await Repository.GetAsync(key1, key2, include, cancellationToken).ConfigureAwait(false);
    }

    public virtual async Task<bool> DeleteAsync(
        TKey1 key1,
        TKey2 key2,
        CancellationToken cancellationToken = default)
    {
        return await Repository.DeleteAsync(key1, key2, cancellationToken).ConfigureAwait(false);
    }

    public virtual async Task<T> AddAsync(
        T entity,
        CancellationToken cancellationToken = default)
    {
        return await Repository.InsertAsync(entity, cancellationToken).ConfigureAwait(false);
    }

    public virtual async Task<IEnumerable<T>> GetAsync(
        string include = "*",
        CancellationToken cancellationToken = default)
    {
        return await Repository.GetAsync(include, cancellationToken).ConfigureAwait(false);
    }
}