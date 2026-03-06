namespace AzdoPr.Models;

public sealed class PrComment
{
    public int Id { get; set; }
    public int ParentCommentId { get; set; }
    public string Content { get; set; } = "";
    public CommentAuthor? Author { get; set; }
    public DateTime PublishedDate { get; set; }
    public string CommentType { get; set; } = "";
}

public sealed class CommentAuthor
{
    public string DisplayName { get; set; } = "";
    public string UniqueName { get; set; } = "";
}
