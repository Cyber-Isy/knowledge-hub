namespace KnowledgeHub.Core.Interfaces.Services;

/// <summary>
/// Provides database seed data for development and demo purposes.
/// </summary>
public interface IDataSeeder
{
    /// <summary>
    /// Seeds the database with demo users, documents, conversations, and messages.
    /// This operation is idempotent and will skip records that already exist.
    /// </summary>
    Task SeedAsync();
}
