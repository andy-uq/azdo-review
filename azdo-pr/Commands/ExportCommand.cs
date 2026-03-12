using System.CommandLine;
using AzdoPr.Models;

namespace AzdoPr.Commands;

public static class ExportCommand
{
    public static Command Create()
    {
        var prIdArg = new Argument<int>("prId", "Pull request ID");
        var repoOpt = new Option<string?>("--repo", "Repository name (or auto-detect from git remote)");
        var orgOpt = new Option<string?>("--org", "Azure DevOps organization");
        var projectOpt = new Option<string?>("--project", "Azure DevOps project");
        var allOpt = new Option<bool>("--all", "Include resolved/fixed threads (not just active/pending)");

        var cmd = new Command("export", "Export PR review comments to markdown")
        {
            prIdArg, repoOpt, orgOpt, projectOpt, allOpt
        };

        cmd.SetHandler(async (int prId, string? repo, string? org, string? project, bool all) =>
        {
            var config = ConfigResolver.Resolve(org, project, repo);
            using var client = new AzdoClient(config.Pat, config.Org, config.Project, config.Repo);

            Console.WriteLine($"Fetching PR #{prId}...");
            var pr = await client.GetPullRequestAsync(prId);
            var threads = await client.GetThreadsAsync(prId);
            var iterations = await client.GetIterationsAsync(prId);

            // Fetch overall file changes (latest iteration vs base)
            List<Models.IterationChange>? allChanges = null;
            List<Models.IterationChange>? latestChanges = null;
            if (iterations.Count > 0)
            {
                allChanges = await client.GetIterationChangesAsync(prId, iterations[^1].Id, 0);
                if (iterations.Count > 1)
                    latestChanges = await client.GetIterationChangesAsync(prId, iterations[^1].Id, iterations[^2].Id);
            }

            var sourceBranch = pr.SourceRefName.Replace("refs/heads/", "");
            var outputDir = Path.Combine(Directory.GetCurrentDirectory(), ".azdo", "reviews", $"PR-{prId}");

            EnsureGitIgnore(Path.Combine(Directory.GetCurrentDirectory(), ".azdo"));

            // Detect previous export timestamp for NEW markers
            DateTime? previousExport = null;
            var indexPath = Path.Combine(outputDir, "index.md");
            if (File.Exists(indexPath))
            {
                var existingIndex = await File.ReadAllTextAsync(indexPath);
                var match = System.Text.RegularExpressions.Regex.Match(
                    existingIndex, @"<!-- generated: (\d{4}-\d{2}-\d{2} \d{2}:\d{2}) -->");
                if (match.Success && DateTime.TryParse(match.Groups[1].Value, out var prev))
                    previousExport = prev;
            }

            Console.WriteLine($"Writing to {outputDir}...");
            if (previousExport.HasValue)
                Console.WriteLine($"Previous export: {previousExport:yyyy-MM-dd HH:mm} — new comments will be marked (NEW)");

            await MarkdownWriter.WriteOutputAsync(
                outputDir, pr, threads,
                (path, version) => client.GetFileContentAsync(path, version),
                sourceBranch, config.Org, config.Project,
                includeAll: all, previousExport: previousExport,
                iterations: iterations, allChanges: allChanges,
                latestIterationChanges: latestChanges,
                prAuthorUniqueName: pr.CreatedBy?.UniqueName);

            var activeCount = threads.Count(t =>
                t.Comments.Any(c => c.CommentType is not "system") &&
                !t.IsDeleted &&
                t.Status is "active" or "pending" or "");

            var totalCount = threads.Count(t =>
                t.Comments.Any(c => c.CommentType is not "system") &&
                !t.IsDeleted);

            if (all)
                Console.WriteLine($"Done. {totalCount} threads exported ({activeCount} active) to {outputDir}");
            else
                Console.WriteLine($"Done. {activeCount} active threads exported to {outputDir}");
        }, prIdArg, repoOpt, orgOpt, projectOpt, allOpt);

        return cmd;
    }

    private static void EnsureGitIgnore(string azdoDir)
    {
        Directory.CreateDirectory(azdoDir);
        var gitIgnorePath = Path.Combine(azdoDir, ".gitignore");
        if (!File.Exists(gitIgnorePath))
        {
            File.WriteAllText(gitIgnorePath, "reviews/\n");
        }
    }
}
