public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken ct = default);

    /// <summary>
    /// Detaches the entity from the change tracker. Used when an insert fails
    /// on a unique constraint and the entity must not be retried on next SaveChanges.
    /// </summary>
    void Detach<T>(T entity) where T : class;
}