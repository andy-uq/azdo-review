namespace AzdoPr.Models;

public sealed class IterationChange
{
    public int ChangeTrackingId { get; set; }
    public string ChangeType { get; set; } = ""; // edit, add, delete, rename, ...
    public ChangeItem? Item { get; set; }
    public string? OriginalPath { get; set; } // populated on renames
}

public sealed class ChangeItem
{
    public string Path { get; set; } = "";
}

public sealed class IterationChanges
{
    public List<IterationChange> ChangeEntries { get; set; } = [];
}
