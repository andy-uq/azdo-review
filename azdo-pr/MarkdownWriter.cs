using System.Text;
using AzdoPr.Models;

namespace AzdoPr;

public static class MarkdownWriter
{
    public static async Task WriteOutputAsync(
        string outputDir, PrInfo pr, List<PrThread> threads,
        Func<string, string, Task<string>> getFileContent, string sourceBranch,
        string org, string project)
    {
        var activeThreads = threads
            .Where(t => IsReviewThread(t) && IsActive(t))
            .ToList();

        var threadsByFile = activeThreads
            .Where(t => t.ThreadContext?.FilePath is not null)
            .GroupBy(t => t.ThreadContext!.FilePath)
            .OrderBy(g => g.Key)
            .ToList();

        Directory.CreateDirectory(outputDir);
        Directory.CreateDirectory(Path.Combine(outputDir, "files"));

        var repoName = pr.Repository?.Name ?? "unknown";
        var prUrl = $"https://dev.azure.com/{org}/{project}/_git/{repoName}/pullrequest/{pr.PullRequestId}";

        // Write index.md
        var sb = new StringBuilder();
        sb.AppendLine($"# PR #{pr.PullRequestId} Review Comments");
        sb.AppendLine($"<!-- pr_url: {prUrl} -->");
        sb.AppendLine($"<!-- generated: {DateTime.Now:yyyy-MM-dd HH:mm} -->");
        sb.AppendLine($"<!-- repo: {repoName} -->");
        sb.AppendLine($"<!-- source_branch: {sourceBranch} -->");
        sb.AppendLine();
        sb.AppendLine("## Summary");
        sb.AppendLine($"{activeThreads.Count} active comments across {threadsByFile.Count} files");
        sb.AppendLine();

        if (threadsByFile.Count > 0)
        {
            sb.AppendLine("## Files");
            foreach (var group in threadsByFile)
            {
                var safeName = group.Key.Replace("/", "--").TrimStart('-');
                sb.AppendLine($"- [ ] `{group.Key}` ({group.Count()} comments) -> [files/{safeName}.md](files/{safeName}.md)");
            }
            sb.AppendLine();
        }

        sb.AppendLine("## All Threads");
        foreach (var thread in activeThreads.Where(t => t.ThreadContext?.FilePath is not null))
        {
            var line = thread.ThreadContext!.RightFileStart?.Line
                    ?? thread.ThreadContext.LeftFileStart?.Line;
            var firstComment = thread.Comments.FirstOrDefault(c => !IsSystemComment(c))?.Content ?? "";
            var preview = Truncate(firstComment.ReplaceLineEndings(" "), 80);
            var lineStr = line.HasValue ? $" line {line}" : "";
            sb.AppendLine($"- [ ] Thread {thread.Id} | `{thread.ThreadContext.FilePath}`{lineStr} | {preview}");
        }

        // Threads without file context (PR-level comments)
        foreach (var thread in activeThreads.Where(t => t.ThreadContext?.FilePath is null))
        {
            var firstComment = thread.Comments.FirstOrDefault(c => !IsSystemComment(c))?.Content ?? "";
            var preview = Truncate(firstComment.ReplaceLineEndings(" "), 80);
            sb.AppendLine($"- [ ] Thread {thread.Id} | (PR-level) | {preview}");
        }

        await File.WriteAllTextAsync(Path.Combine(outputDir, "index.md"), sb.ToString());

        // Write per-file .md files
        foreach (var group in threadsByFile)
        {
            await WriteFileMarkdownAsync(
                outputDir, group.Key, group.ToList(),
                getFileContent, sourceBranch, prUrl);
        }
    }

    private static async Task WriteFileMarkdownAsync(
        string outputDir, string filePath, List<PrThread> threads,
        Func<string, string, Task<string>> getFileContent, string sourceBranch, string prUrl)
    {
        var safeName = filePath.Replace("/", "--").TrimStart('-');
        var sb = new StringBuilder();

        sb.AppendLine($"# {filePath}");
        sb.AppendLine($"<!-- source_path: {filePath} -->");
        sb.AppendLine();

        // Try to fetch source content for code context
        string? sourceContent = null;
        try
        {
            sourceContent = await getFileContent(filePath, sourceBranch);
        }
        catch
        {
            // File might not exist or be inaccessible
        }

        var sourceLines = sourceContent?.Split('\n') ?? [];
        var extension = Path.GetExtension(filePath).TrimStart('.');

        foreach (var thread in threads.OrderBy(t => t.ThreadContext?.RightFileStart?.Line ?? 0))
        {
            var line = thread.ThreadContext?.RightFileStart?.Line
                    ?? thread.ThreadContext?.LeftFileStart?.Line;
            var lineStr = line.HasValue ? $" | Line {line}" : "";

            sb.AppendLine("---");
            sb.AppendLine($"## Thread {thread.Id}{lineStr}");
            sb.AppendLine($"<!-- thread_id:{thread.Id} status:{thread.Status} path:{filePath} line:{line} -->");
            sb.AppendLine($"[View in Azure DevOps]({prUrl}?discussionId={thread.Id})");
            sb.AppendLine();

            // Code context
            if (line.HasValue && sourceLines.Length > 0)
            {
                var start = Math.Max(0, line.Value - 3);
                var end = Math.Min(sourceLines.Length - 1, line.Value + 2);
                sb.AppendLine("### Code Context");
                sb.AppendLine($"```{extension}");
                for (var i = start; i <= end; i++)
                {
                    var marker = (i + 1 == line.Value) ? " // <--" : "";
                    sb.AppendLine($"  {i + 1}: {sourceLines[i].TrimEnd()}{marker}");
                }
                sb.AppendLine("```");
                sb.AppendLine();
            }

            // Comments
            sb.AppendLine("### Comment");
            foreach (var comment in thread.Comments.Where(c => !IsSystemComment(c)))
            {
                var author = comment.Author?.DisplayName ?? "Unknown";
                var date = comment.PublishedDate.ToString("yyyy-MM-dd HH:mm");
                foreach (var commentLine in comment.Content.Split('\n'))
                {
                    sb.AppendLine($"> {commentLine.TrimEnd()}");
                }
                sb.AppendLine($"> -- _{author}_ ({date})");
                sb.AppendLine();
            }

            sb.AppendLine("- [ ] Addressed");
            sb.AppendLine();
        }

        await File.WriteAllTextAsync(
            Path.Combine(outputDir, "files", $"{safeName}.md"), sb.ToString());
    }

    private static bool IsReviewThread(PrThread thread)
    {
        // Exclude system threads (votes, status updates, etc.)
        if (thread.IsDeleted) return false;
        if (thread.Comments.Count == 0) return false;
        // System comments have commentType = 2
        return thread.Comments.Any(c => !IsSystemComment(c));
    }

    private static bool IsActive(PrThread thread)
    {
        return thread.Status is "active" or "pending" or "";
    }

    private static bool IsSystemComment(PrComment c) =>
        c.CommentType is "system" or "2";

    private static string Truncate(string text, int maxLength)
    {
        if (text.Length <= maxLength) return text;
        return text[..(maxLength - 3)] + "...";
    }
}
