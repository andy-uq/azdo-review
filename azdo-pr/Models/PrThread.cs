using System.Text.Json;

namespace AzdoPr.Models;

public sealed class PrThread
{
    public int Id { get; set; }
    public string Status { get; set; } = "";
    public ThreadContext? ThreadContext { get; set; }
    public List<PrComment> Comments { get; set; } = [];
    public Dictionary<string, JsonElement>? Properties { get; set; }
    public bool IsDeleted { get; set; }
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
