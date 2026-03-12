using System.Text.Json;

namespace AzdoPr.Models;

public sealed class PrThread
{
    public int Id { get; set; }
    public string Status { get; set; } = "";
    public ThreadContext? ThreadContext { get; set; }
    public PrThreadIterationContext? IterationContext { get; set; }
    public List<PrComment> Comments { get; set; } = [];
    public Dictionary<string, JsonElement>? Properties { get; set; }
    public bool IsDeleted { get; set; }
}

public sealed class PrThreadIterationContext
{
    public int FirstComparingIteration { get; set; }
    public int SecondComparingIteration { get; set; }
}

public sealed class ThreadContext
{
    public string FilePath { get; set; } = "";
    public LineRange? RightFileStart { get; set; }
    public LineRange? RightFileEnd { get; set; }
    public LineRange? LeftFileStart { get; set; }
    public LineRange? LeftFileEnd { get; set; }
}

public sealed class LineRange
{
    public int Line { get; set; }
    public int Offset { get; set; }
}

public sealed class PrIteration
{
    public int Id { get; set; }
    public DateTime CreatedDate { get; set; }
    public string Description { get; set; } = "";
    public CommitRef? SourceRefCommit { get; set; }
}

public sealed class CommitRef
{
    public string CommitId { get; set; } = "";
}
