using System.Text;
using AzdoPr.Models;

namespace AzdoPr;

public static class MarkdownWriter
{
    public static async Task WriteOutputAsync(
        string outputDir, PrInfo pr, List<PrThread> threads,
        Func<string, string, Task<string>> getFileContent, string sourceBranch,
        string org, string project,
        bool includeAll = false, DateTime? previousExport = null,
        List<PrIteration>? iterations = null,
        List<IterationChange>? allChanges = null,
        List<IterationChange>? latestIterationChanges = null,
        string? prAuthorUniqueName = null)
    {
        var reviewThreads = threads.Where(t => IsReviewThread(t)).ToList();

        var activeThreads = reviewThreads.Where(IsActive).ToList();
        var resolvedThreads = reviewThreads.Where(t => !IsActive(t)).ToList();

        var visibleThreads = includeAll ? reviewThreads : activeThreads;

        var threadsByFile = visibleThreads
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
        sb.AppendLine($"# PR #{pr.PullRequestId}: {pr.Title}");
        sb.AppendLine($"<!-- pr_url: {prUrl} -->");
        sb.AppendLine($"<!-- generated: {DateTime.Now:yyyy-MM-dd HH:mm} -->");
        sb.AppendLine($"<!-- repo: {repoName} -->");
        sb.AppendLine($"<!-- source_branch: {sourceBranch} -->");
        if (previousExport.HasValue)
            sb.AppendLine($"<!-- previous_export: {previousExport:yyyy-MM-dd HH:mm} -->");
        sb.AppendLine();

        if (!string.IsNullOrWhiteSpace(pr.Description))
        {
            sb.AppendLine("## Description");
            foreach (var descLine in pr.Description.Split('\n'))
                sb.AppendLine($"> {descLine.TrimEnd()}");
            sb.AppendLine();
        }

        sb.AppendLine("## Status");
        var statusStr = char.ToUpper(pr.Status[0]) + pr.Status[1..];
        if (pr.IsDraft) statusStr += " (Draft)";
        sb.AppendLine($"- **PR Status:** {statusStr}");
        if (!string.IsNullOrEmpty(pr.MergeStatus) && pr.MergeStatus != "notSet")
            sb.AppendLine($"- **Merge:** {char.ToUpper(pr.MergeStatus[0]) + pr.MergeStatus[1..]}");
        if (pr.Reviewers.Count > 0)
        {
            var reviewerStrs = pr.Reviewers.Select(r => $"{r.DisplayName} ({VoteToString(r.Vote)})");
            sb.AppendLine($"- **Reviewers:** {string.Join(", ", reviewerStrs)}");
        }
        if (iterations is { Count: > 0 })
        {
            var latest = iterations[^1];
            var commitShort = latest.SourceRefCommit?.CommitId.Length >= 7
                ? latest.SourceRefCommit.CommitId[..7]
                : latest.SourceRefCommit?.CommitId ?? "";
            sb.AppendLine($"- **Iterations:** {iterations.Count} (latest: {latest.CreatedDate:yyyy-MM-dd}, {commitShort})");
        }
        sb.AppendLine();

        if (allChanges is { Count: > 0 })
        {
            sb.AppendLine("## Changed Files");
            var byType = allChanges
                .Where(c => c.Item?.Path is not null)
                .OrderBy(c => c.Item!.Path)
                .GroupBy(c => c.ChangeType);
            foreach (var group in byType.OrderBy(g => g.Key))
            {
                foreach (var change in group)
                {
                    var rename = change.OriginalPath is not null
                        ? $" (was `{change.OriginalPath}`)" : "";
                    sb.AppendLine($"- `{change.Item!.Path}` ({change.ChangeType}){rename}");
                }
            }
            sb.AppendLine();
            if (latestIterationChanges is { Count: > 0 } && iterations is { Count: > 1 })
            {
                sb.AppendLine($"### New in iteration {iterations[^1].Id} ({latestIterationChanges.Count} files)");
                foreach (var change in latestIterationChanges.Where(c => c.Item?.Path is not null).OrderBy(c => c.Item!.Path))
                {
                    sb.AppendLine($"- `{change.Item!.Path}` ({change.ChangeType})");
                }
                sb.AppendLine();
            }
        }

        sb.AppendLine("## Summary");
        sb.AppendLine($"{activeThreads.Count} active threads, {resolvedThreads.Count} resolved threads");
        if (previousExport.HasValue)
        {
            var newCommentCount = visibleThreads
                .SelectMany(t => t.Comments)
                .Count(c => !IsSystemComment(c) && c.PublishedDate > previousExport.Value);
            sb.AppendLine($"**{newCommentCount} new comments since {previousExport:yyyy-MM-dd HH:mm}**");
        }
        sb.AppendLine();

        if (threadsByFile.Count > 0)
        {
            sb.AppendLine("## Files");
            foreach (var group in threadsByFile)
            {
                var safeName = group.Key.Replace("/", "--").TrimStart('-');
                var hasNew = previousExport.HasValue && group.Any(t =>
                    t.Comments.Any(c => !IsSystemComment(c) && c.PublishedDate > previousExport.Value));
                var newMarker = hasNew ? " **(NEW)**" : "";
                sb.AppendLine($"- [ ] `{group.Key}` ({group.Count()} comments){newMarker} -> [files/{safeName}.md](files/{safeName}.md)");
            }
            sb.AppendLine();
        }

        sb.AppendLine("## All Threads");
        foreach (var thread in visibleThreads.Where(t => t.ThreadContext?.FilePath is not null))
        {
            var line = thread.ThreadContext!.RightFileStart?.Line
                    ?? thread.ThreadContext.LeftFileStart?.Line;
            var firstComment = thread.Comments.FirstOrDefault(c => !IsSystemComment(c))?.Content ?? "";
            var preview = Truncate(firstComment.ReplaceLineEndings(" "), 80);
            var lineStr = line.HasValue ? $" line {line}" : "";
            var statusTag = !IsActive(thread) ? $" [{thread.Status}]" : "";
            var hasNew = previousExport.HasValue &&
                thread.Comments.Any(c => !IsSystemComment(c) && c.PublishedDate > previousExport.Value);
            var newMarker = hasNew ? " **(NEW)**" : "";
            var awaitingReviewer = IsActive(thread) && IsAuthorLastCommenter(thread, prAuthorUniqueName)
                ? " **(AWAITING REVIEWER)**" : "";
            sb.AppendLine($"- [ ] Thread {thread.Id}{statusTag} | `{thread.ThreadContext.FilePath}`{lineStr} | {preview}{newMarker}{awaitingReviewer}");
        }

        // Threads without file context (PR-level comments)
        foreach (var thread in visibleThreads.Where(t => t.ThreadContext?.FilePath is null))
        {
            var firstComment = thread.Comments.FirstOrDefault(c => !IsSystemComment(c))?.Content ?? "";
            var preview = Truncate(firstComment.ReplaceLineEndings(" "), 80);
            var statusTag = !IsActive(thread) ? $" [{thread.Status}]" : "";
            var hasNew = previousExport.HasValue &&
                thread.Comments.Any(c => !IsSystemComment(c) && c.PublishedDate > previousExport.Value);
            var newMarker = hasNew ? " **(NEW)**" : "";
            var awaitingReviewer = IsActive(thread) && IsAuthorLastCommenter(thread, prAuthorUniqueName)
                ? " **(AWAITING REVIEWER)**" : "";
            sb.AppendLine($"- [ ] Thread {thread.Id}{statusTag} | (PR-level) | {preview}{newMarker}{awaitingReviewer}");
        }

        await File.WriteAllTextAsync(Path.Combine(outputDir, "index.md"), sb.ToString());

        // Write per-file .md files
        foreach (var group in threadsByFile)
        {
            await WriteFileMarkdownAsync(
                outputDir, group.Key, group.ToList(),
                getFileContent, sourceBranch, prUrl, previousExport, prAuthorUniqueName);
        }
    }

    private static async Task WriteFileMarkdownAsync(
        string outputDir, string filePath, List<PrThread> threads,
        Func<string, string, Task<string>> getFileContent, string sourceBranch, string prUrl,
        DateTime? previousExport, string? prAuthorUniqueName = null)
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
            var statusTag = !IsActive(thread) ? $" [{thread.Status}]" : "";

            var threadHasNew = previousExport.HasValue &&
                thread.Comments.Any(c => !IsSystemComment(c) && c.PublishedDate > previousExport.Value);
            var threadNewMarker = threadHasNew ? " (NEW REPLIES)" : "";

            var iterTag = thread.IterationContext?.SecondComparingIteration is > 0
                ? $" (iter {thread.IterationContext.SecondComparingIteration})" : "";

            var awaitingReviewer = IsActive(thread) && IsAuthorLastCommenter(thread, prAuthorUniqueName)
                ? " (AWAITING REVIEWER)" : "";

            sb.AppendLine("---");
            sb.AppendLine($"## Thread {thread.Id}{statusTag}{lineStr}{iterTag}{threadNewMarker}{awaitingReviewer}");
            sb.AppendLine($"<!-- thread_id:{thread.Id} status:{thread.Status} path:{filePath} line:{line} iteration:{thread.IterationContext?.SecondComparingIteration} -->");
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

            // Comments — distinguish original vs replies
            var nonSystemComments = thread.Comments.Where(c => !IsSystemComment(c)).ToList();
            var isFirstComment = true;
            foreach (var comment in nonSystemComments)
            {
                var isReply = comment.ParentCommentId != 0;
                var isNew = previousExport.HasValue && comment.PublishedDate > previousExport.Value;

                if (isFirstComment)
                    sb.AppendLine("### Comment");
                else if (isReply)
                    sb.AppendLine("### Reply" + (isNew ? " (NEW)" : ""));
                else
                    sb.AppendLine("### Comment" + (isNew ? " (NEW)" : ""));

                if (isNew && !isFirstComment)
                    sb.AppendLine("<!-- new_since_previous_export -->");

                var author = comment.Author?.DisplayName ?? "Unknown";
                var date = comment.PublishedDate.ToString("yyyy-MM-dd HH:mm");
                foreach (var commentLine in comment.Content.Split('\n'))
                {
                    sb.AppendLine($"> {commentLine.TrimEnd()}");
                }
                sb.AppendLine($"> -- _{author}_ ({date})");
                sb.AppendLine();

                isFirstComment = false;
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

    private static string VoteToString(int vote) => vote switch
    {
        10 => "Approved",
        5 => "Approved with suggestions",
        -5 => "Waiting for author",
        -10 => "Rejected",
        _ => "No vote",
    };

    private static bool IsAuthorLastCommenter(PrThread thread, string? authorUniqueName)
    {
        if (authorUniqueName is null) return false;
        var lastComment = thread.Comments
            .Where(c => !IsSystemComment(c))
            .OrderByDescending(c => c.PublishedDate)
            .FirstOrDefault();
        return lastComment?.Author?.UniqueName?.Equals(authorUniqueName, StringComparison.OrdinalIgnoreCase) == true;
    }

    private static string Truncate(string text, int maxLength)
    {
        if (text.Length <= maxLength) return text;
        return text[..(maxLength - 3)] + "...";
    }
}
