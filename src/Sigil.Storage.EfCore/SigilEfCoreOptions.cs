namespace Sigil.Storage.EfCore;

public sealed class SigilEfCoreOptions
{
    public const string SectionName = "Storage:EfCore";

    public string ConnectionString { get; set; } = "";
}
