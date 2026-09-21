using DAL.Entity;
using DAL.Repository;

namespace BLL.Business;

public abstract class BaseBusiness<T, TKey> : IBusiness<T, TKey>
    where T : class, IEntity<T, TKey>, new()
    where TKey : notnull
{
    protected readonly IRepository<T, TKey> Repository;

    protected BaseBusiness(IRepository<T, TKey> repository)
    {
        Repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public virtual async Task<IEnumerable<T>> GetAsync(
        string include = "*",
        CancellationToken cancellationToken = default)
    {
        return await Repository.GetAsync(include, cancellationToken).ConfigureAwait(false);
    }

    public virtual async Task<T> GetAsync(
        TKey id,
        string include = "*",
        CancellationToken cancellationToken = default)
    {
        return await Repository.GetAsync(id, include, cancellationToken).ConfigureAwait(false);
    }

    public virtual async Task<bool> DeleteAsync(
        TKey id,
        CancellationToken cancellationToken = default)
    {
        return await Repository.DeleteAsync(id, cancellationToken).ConfigureAwait(false);
    }

    public virtual async Task<T> AddAsync(
        T entity,
        string[]? exclude = null,
        CancellationToken cancellationToken = default)
    {
        return await Repository.InsertAsync(entity, exclude, cancellationToken).ConfigureAwait(false);
    }

    public virtual async Task<int> AddAsync(
        IEnumerable<T> entities,
        CancellationToken cancellationToken = default)
    {
        // Maps to the specialized batch method (which omits SCOPE_IDENTITY)
        return await Repository.InsertBatchAsync(entities, cancellationToken).ConfigureAwait(false);
    }

    public virtual async Task<T> UpdateAsync(
        T entity,
        string[]? exclude = null,
        CancellationToken cancellationToken = default)
    {
        return await Repository.UpdateAsync(entity, exclude, cancellationToken).ConfigureAwait(false);
    }
}