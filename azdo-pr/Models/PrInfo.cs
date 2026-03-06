namespace AzdoPr.Models;

public sealed class PrInfo
{
    public int PullRequestId { get; set; }
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string SourceRefName { get; set; } = "";
    public string TargetRefName { get; set; } = "";
    public string Url { get; set; } = "";
    public PrRepository? Repository { get; set; }
}

public sealed class PrRepository
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string WebUrl { get; set; } = "";
}
