namespace DAL.Entity;

/// <summary>
/// Contract for single-key entities with zero-allocation compile-time metadata.
/// </summary>
public interface IEntity<TSelf, TKey>
    where TSelf : IEntity<TSelf, TKey>
    where TKey : notnull
{
    TKey Id { get; set; }

    static abstract string TableName { get; }

    // Default implementation: most tables use "Id"
    static virtual string KeyColumnName => "Id";
}