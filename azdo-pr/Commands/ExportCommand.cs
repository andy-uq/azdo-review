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

        var cmd = new Command("export", "Export PR review comments to markdown")
        {
            prIdArg, repoOpt, orgOpt, projectOpt
        };

        cmd.SetHandler(async (int prId, string? repo, string? org, string? project) =>
        {
            var config = ConfigResolver.Resolve(org, project, repo);
            using var client = new AzdoClient(config.Pat, config.Org, config.Project, config.Repo);

            Console.WriteLine($"Fetching PR #{prId}...");
            var pr = await client.GetPullRequestAsync(prId);
            var threads = await client.GetThreadsAsync(prId);

            var sourceBranch = pr.SourceRefName.Replace("refs/heads/", "");
            var outputDir = Path.Combine(Directory.GetCurrentDirectory(), ".azdo", "reviews", $"PR-{prId}");

            EnsureGitIgnore(Path.Combine(Directory.GetCurrentDirectory(), ".azdo"));

            Console.WriteLine($"Writing to {outputDir}...");
            await MarkdownWriter.WriteOutputAsync(
                outputDir, pr, threads,
                (path, version) => client.GetFileContentAsync(path, version),
                sourceBranch, config.Org, config.Project);

            var activeCount = threads.Count(t =>
                t.Comments.Any(c => c.CommentType is not "system") &&
                !t.IsDeleted &&
                t.Status is "active" or "pending" or "");

            Console.WriteLine($"Done. {activeCount} active threads exported to {outputDir}");
        }, prIdArg, repoOpt, orgOpt, projectOpt);

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
