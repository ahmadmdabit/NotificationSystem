using System.Data;

using Common.Helpers;

using DAL.Entity;

using Dapper;

namespace DAL.Repository;

/// <summary>
/// Defines the asynchronous CRUD and execution contract for an entity repository.
/// </summary>
/// <typeparam name="T">The entity type implementing IEntity.</typeparam>
/// <typeparam name="TKey">The primary key type (e.g., long, Guid, int).</typeparam>
public interface IRepository<T, TKey>
    where T : class, IEntity<T, TKey>, new()
    where TKey : notnull
{
    /// <summary>
    /// Retrieves all entities with optional column projection.
    /// </summary>
    Task<IEnumerable<T>> GetAsync(
        string include = "*",
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves entities filtering by a single validated property/column.
    /// </summary>
    Task<IEnumerable<T>> GetAsync(
        string prop,
        object value,
        string include = "*",
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves an entity by its primary key. Throws <see cref="KeyNotFoundException"/> if not found.
    /// </summary>
    Task<T> GetAsync(
        TKey id,
        string include = "*",
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes an entity by its primary key.
    /// </summary>
    Task<bool> DeleteAsync(
        TKey id,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Inserts a single entity and returns the inserted entity populated with generated values.
    /// </summary>
    Task<T> InsertAsync(
        T entity,
        string[]? exclude = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Inserts a collection of entities in a single batch command without round-tripping generated keys.
    /// </summary>
    Task<int> InsertBatchAsync(
        IEnumerable<T> list,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing entity and returns the refreshed record.
    /// </summary>
    Task<T> UpdateAsync(
        T entity,
        string[]? exclude = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a custom SQL statement or stored procedure returning a structured <see cref="SpResult"/>.
    /// </summary>
    Task<SpResult> QueryAsync(
        string sql,
        DynamicParameters? parameters = null,
        CommandType commandType = CommandType.StoredProcedure,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a command returning multiple result grids into an aggregated <see cref="SpResult"/>.
    /// </summary>
    Task<SpResult> QueryMultipleAsync(
        string sql,
        DynamicParameters? parameters = null,
        CommandType commandType = CommandType.StoredProcedure,
        CancellationToken cancellationToken = default);
}