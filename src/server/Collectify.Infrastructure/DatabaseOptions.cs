namespace Collectify.Infrastructure;

/// <summary>
/// Database provider selection. Bound from <c>Collectify:Database</c> config
/// section (env vars: <c>Collectify__Database__*</c>).
/// </summary>
public static class DatabaseOptions
{
    public const string SectionName = "Collectify:Database";
    public const string ProviderKey = SectionName + ":Provider";
    public const string ConnectionStringKey = SectionName + ":ConnectionString";
    public const string AdminConnectionStringKey = SectionName + ":AdminConnectionString";
    public const string AdminDatabaseKey = SectionName + ":AdminDatabase";
    public const string BackupRetentionKey = SectionName + ":BackupRetention";
    public const int DefaultBackupRetention = 10;

    public const string SqliteProvider = "sqlite";
    public const string PostgresProvider = "postgres";
    public const string DefaultProvider = SqliteProvider;

    public const string SqliteMigrationsAssembly = "Collectify.Infrastructure";
    public const string PostgresMigrationsAssembly = "Collectify.PostgresMigrations";
    public const string PostgresSchema = "public";
    public const string MigrationsHistoryTable = "__EFMigrationsHistory";
    public const string DefaultPostgresAdminDatabase = "postgres";

    public static readonly HashSet<string> Providers = new(StringComparer.OrdinalIgnoreCase)
    {
        SqliteProvider,
        PostgresProvider,
    };
}
