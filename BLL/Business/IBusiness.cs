using DAL.Entity;

namespace BLL.Business;

/// <summary>
/// Business logic layer contract for entity operations.
/// </summary>
/// <typeparam name="T">Entity type implementing IEntity.</typeparam>
/// <typeparam name="TKey">Primary key type (e.g., long, Guid, int).</typeparam>
public interface IBusiness<T, TKey>
    where T : class, IEntity<T, TKey>, new()
    where TKey : notnull
{
    Task<IEnumerable<T>> GetAsync(
        string include = "*",
        CancellationToken cancellationToken = default);

    Task<T> GetAsync(
        TKey id,
        string include = "*",
        CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(
        TKey id,
        CancellationToken cancellationToken = default);

    Task<T> AddAsync(
        T entity,
        string[]? exclude = null,
        CancellationToken cancellationToken = default);

    Task<int> AddAsync(
        IEnumerable<T> entities,
        CancellationToken cancellationToken = default);

    Task<T> UpdateAsync(
        T entity,
        string[]? exclude = null,
        CancellationToken cancellationToken = default);
}