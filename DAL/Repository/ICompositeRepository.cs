using DAL.Entity;

namespace DAL.Repository;

public interface ICompositeRepository<T, in TKey1, in TKey2>
    where T : class, ICompositeEntity<T, TKey1, TKey2>, new()
    where TKey1 : notnull
    where TKey2 : notnull
{
    Task<T?> GetAsync(TKey1 key1, TKey2 key2, string include = "*", CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(TKey1 key1, TKey2 key2, CancellationToken cancellationToken = default);
    Task<T> InsertAsync(T entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists all records (supports the UI grid and gateway list routes).
    /// </summary>
    Task<IEnumerable<T>> GetAsync(string include = "*", CancellationToken cancellationToken = default);
}