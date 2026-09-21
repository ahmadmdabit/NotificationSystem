namespace DAL.Entity;

/// <summary>
/// Contract for 2-column composite key entities (e.g., junction tables, association records).
/// </summary>
public interface ICompositeEntity<TSelf, TKey1, TKey2>
    where TSelf : ICompositeEntity<TSelf, TKey1, TKey2>
    where TKey1 : notnull
    where TKey2 : notnull
{
    static abstract string TableName { get; }
    static abstract string Key1ColumnName { get; }
    static abstract string Key2ColumnName { get; }

    TKey1 Key1 { get; }
    TKey2 Key2 { get; }
}