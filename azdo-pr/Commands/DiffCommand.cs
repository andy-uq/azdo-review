using System.CommandLine;

namespace AzdoPr.Commands;

public static class DiffCommand
{
    public static Command Create()
    {
        var prIdArg = new Argument<int>("prId", "Pull request ID");
        var fromOpt = new Option<int?>("--from", "Base iteration to compare from (default: previous iteration, or 0 for base branch)");
        var toOpt = new Option<int?>("--to", "Target iteration to compare to (default: latest)");
        var repoOpt = new Option<string?>("--repo", "Repository name");
        var orgOpt = new Option<string?>("--org", "Azure DevOps organization");
        var projectOpt = new Option<string?>("--project", "Azure DevOps project");

        var cmd = new Command("diff", "Show files changed between PR iterations")
        {
            prIdArg, fromOpt, toOpt, repoOpt, orgOpt, projectOpt
        };

        cmd.SetHandler(async (int prId, int? from, int? to, string? repo, string? org, string? project) =>
        {
            var config = ConfigResolver.Resolve(org, project, repo);
            using var client = new AzdoClient(config.Pat, config.Org, config.Project, config.Repo);

            var iterations = await client.GetIterationsAsync(prId);
            if (iterations.Count == 0)
            {
                Console.WriteLine("No iterations found.");
                return;
            }

            var targetIteration = to ?? iterations[^1].Id;
            var baseIteration = from ?? (iterations.Count > 1 ? iterations[^2].Id : 0);

            var targetIter = iterations.FirstOrDefault(i => i.Id == targetIteration);
            var commitShort = targetIter?.SourceRefCommit?.CommitId.Length >= 7
                ? targetIter.SourceRefCommit.CommitId[..7] : targetIter?.SourceRefCommit?.CommitId ?? "";

            Console.WriteLine($"Changes in iteration {targetIteration} (compared to {(baseIteration == 0 ? "base branch" : $"iteration {baseIteration}")})");
            if (targetIter != null)
                Console.WriteLine($"Pushed: {targetIter.CreatedDate:yyyy-MM-dd HH:mm}  Commit: {commitShort}");
            Console.WriteLine();

            var changes = await client.GetIterationChangesAsync(prId, targetIteration, baseIteration);

            if (changes.Count == 0)
            {
                Console.WriteLine("No file changes.");
                return;
            }

            // Group by change type
            var grouped = changes
                .Where(c => c.Item?.Path is not null)
                .OrderBy(c => c.Item!.Path)
                .GroupBy(c => c.ChangeType)
                .OrderBy(g => g.Key);

            foreach (var group in grouped)
            {
                Console.WriteLine($"  {char.ToUpper(group.Key[0]) + group.Key[1..]}:");
                foreach (var change in group)
                {
                    var rename = change.OriginalPath is not null
                        ? $" (was {change.OriginalPath})" : "";
                    Console.WriteLine($"    {change.Item!.Path}{rename}");
                }
            }

            Console.WriteLine();
            Console.WriteLine($"{changes.Count} file(s) changed");

            // Also show overall PR diff if comparing to base
            if (baseIteration == 0 || from == null)
            {
                // Show the full diff too for context
                var fullChanges = await client.GetIterationChangesAsync(prId, iterations[^1].Id, 0);
                if (fullChanges.Count != changes.Count || baseIteration != 0)
                {
                    Console.WriteLine();
                    Console.WriteLine($"Overall PR: {fullChanges.Count} file(s) changed across {iterations.Count} iteration(s)");
                }
            }
        }, prIdArg, fromOpt, toOpt, repoOpt, orgOpt, projectOpt);

        return cmd;
    }
}
