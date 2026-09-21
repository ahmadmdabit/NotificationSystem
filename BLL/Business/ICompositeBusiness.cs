using DAL.Entity;

namespace BLL.Business;

/// <summary>
/// Business logic contract for dual-key composite entities (e.g., junction tables, association records).
/// </summary>
/// <typeparam name="T">Composite entity type implementing ICompositeEntity.</typeparam>
/// <typeparam name="TKey1">Type of the first primary key component.</typeparam>
/// <typeparam name="TKey2">Type of the second primary key component.</typeparam>
public interface ICompositeBusiness<T, TKey1, TKey2>
    where T : class, ICompositeEntity<T, TKey1, TKey2>, new()
    where TKey1 : notnull
    where TKey2 : notnull
{
    /// <summary>
    /// Retrieves a composite record by its dual-part key.
    /// </summary>
    Task<T?> GetAsync(
        TKey1 key1,
        TKey2 key2,
        string include = "*",
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a composite record by its dual-part key.
    /// </summary>
    Task<bool> DeleteAsync(
        TKey1 key1,
        TKey2 key2,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new composite entity.
    /// </summary>
    Task<T> AddAsync(
        T entity,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists all composite records.
    /// </summary>
    Task<IEnumerable<T>> GetAsync(
        string include = "*",
        CancellationToken cancellationToken = default);
}