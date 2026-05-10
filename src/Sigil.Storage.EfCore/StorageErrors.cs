namespace Sigil.Storage.EfCore;

public static class StorageErrors
{
    public const string EtagMismatch = "etag-mismatch";
    public const string NotFound = "not-found";
    public const string DuplicateAgent = "duplicate-agent";
    public const string DuplicateJob = "duplicate-job";
    public const string ValidationSkillName = "validation/skill-name-empty";
    public const string ValidationSkillDuplicate = "validation/skill-duplicate";
    public const string ValidationSkillRequiresUnknownTool = "validation/skill-requires-unknown-tool";
    public const string ValidationToolNameDuplicate = "validation/tool-duplicate";
    public const string ValidationAllowedToolUnknown = "validation/allowed-tool-unknown";
}
