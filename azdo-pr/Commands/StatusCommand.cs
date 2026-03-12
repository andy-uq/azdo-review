using System.CommandLine;

namespace AzdoPr.Commands;

public static class StatusCommand
{
    public static Command Create()
    {
        var prIdArg = new Argument<int>("prId", "Pull request ID");
        var repoOpt = new Option<string?>("--repo", "Repository name");
        var orgOpt = new Option<string?>("--org", "Azure DevOps organization");
        var projectOpt = new Option<string?>("--project", "Azure DevOps project");

        var cmd = new Command("status", "Show PR status summary (no file fetches)")
        {
            prIdArg, repoOpt, orgOpt, projectOpt
        };

        cmd.SetHandler(async (int prId, string? repo, string? org, string? project) =>
        {
            var config = ConfigResolver.Resolve(org, project, repo);
            using var client = new AzdoClient(config.Pat, config.Org, config.Project, config.Repo);

            var pr = await client.GetPullRequestAsync(prId);
            var threads = await client.GetThreadsAsync(prId);
            var iterations = await client.GetIterationsAsync(prId);

            var reviewThreads = threads
                .Where(t => !t.IsDeleted && t.Comments.Any(c => c.CommentType is not "system" and not "2"))
                .ToList();
            var activeCount = reviewThreads.Count(t => t.Status is "active" or "pending" or "");
            var resolvedCount = reviewThreads.Count - activeCount;

            var statusStr = string.IsNullOrEmpty(pr.Status) ? "unknown" : pr.Status;
            if (pr.IsDraft) statusStr += " (draft)";

            Console.WriteLine($"PR #{prId}: {pr.Title}");
            Console.WriteLine($"Status: {statusStr}  Merge: {(string.IsNullOrEmpty(pr.MergeStatus) || pr.MergeStatus == "notSet" ? "n/a" : pr.MergeStatus)}");

            if (pr.Reviewers.Count > 0)
            {
                var reviewerStrs = pr.Reviewers.Select(r => $"{r.DisplayName} ({VoteToString(r.Vote)})");
                Console.WriteLine($"Reviewers: {string.Join(", ", reviewerStrs)}");
            }

            Console.WriteLine($"Threads: {activeCount} active, {resolvedCount} resolved");
            Console.WriteLine($"Iterations: {iterations.Count}");

            if (iterations.Count > 0)
            {
                var latest = iterations[^1];
                var commitShort = latest.SourceRefCommit?.CommitId.Length >= 7
                    ? latest.SourceRefCommit.CommitId[..7] : latest.SourceRefCommit?.CommitId ?? "";
                Console.WriteLine($"Latest push: {latest.CreatedDate:yyyy-MM-dd HH:mm} ({commitShort})");
            }

            // Check for previous export and new comments
            var outputDir = Path.Combine(Directory.GetCurrentDirectory(), ".azdo", "reviews", $"PR-{prId}");
            var indexPath = Path.Combine(outputDir, "index.md");
            if (File.Exists(indexPath))
            {
                var existingIndex = await File.ReadAllTextAsync(indexPath);
                var match = System.Text.RegularExpressions.Regex.Match(
                    existingIndex, @"<!-- generated: (\d{4}-\d{2}-\d{2} \d{2}:\d{2}) -->");
                if (match.Success && DateTime.TryParse(match.Groups[1].Value, out var prev))
                {
                    var newCommentCount = reviewThreads
                        .SelectMany(t => t.Comments)
                        .Count(c => c.CommentType is not "system" and not "2" && c.PublishedDate > prev);
                    Console.WriteLine($"Last export: {prev:yyyy-MM-dd HH:mm}  New comments since: {newCommentCount}");
                }
            }
            else
            {
                Console.WriteLine("No previous export found.");
            }
        }, prIdArg, repoOpt, orgOpt, projectOpt);

        return cmd;
    }

    private static string VoteToString(int vote) => vote switch
    {
        10 => "Approved",
        5 => "Approved w/suggestions",
        -5 => "Waiting for author",
        -10 => "Rejected",
        _ => "No vote",
    };
}
