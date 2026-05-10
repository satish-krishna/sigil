namespace Sigil.Storage.EfCore;

public sealed class SigilEfCoreOptions
{
    public const string SectionName = "Storage:EfCore";

    public string ConnectionString { get; set; } = "";

    // When true, AddSigilEfCore applies pending migrations on startup.
    // Off by default — production deployments should run migrations as a
    // separate step.
    public bool MigrateOnStartup { get; set; }
}
