namespace AzdoPr.Models;

public sealed class PrInfo
{
    public int PullRequestId { get; set; }
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string SourceRefName { get; set; } = "";
    public string TargetRefName { get; set; } = "";
    public string Url { get; set; } = "";
    public string Status { get; set; } = "";
    public bool IsDraft { get; set; }
    public string MergeStatus { get; set; } = "";
    public PrAuthor? CreatedBy { get; set; }
    public List<PrReviewer> Reviewers { get; set; } = [];
    public PrRepository? Repository { get; set; }
}

public sealed class PrRepository
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string WebUrl { get; set; } = "";
}

public sealed class PrAuthor
{
    public string DisplayName { get; set; } = "";
    public string UniqueName { get; set; } = "";
}

public sealed class PrReviewer
{
    public string DisplayName { get; set; } = "";
    public string UniqueName { get; set; } = "";
    public int Vote { get; set; }
}
