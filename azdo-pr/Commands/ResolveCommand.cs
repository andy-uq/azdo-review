using System.CommandLine;

namespace AzdoPr.Commands;

public static class ResolveCommand
{
    public static Command Create()
    {
        var prIdArg = new Argument<int>("prId", "Pull request ID");
        var threadOpt = new Option<int?>("--thread", "Thread ID to resolve");
        var fileOpt = new Option<string?>("--file", "Resolve all threads on this file path");
        var repoOpt = new Option<string?>("--repo", "Repository name");
        var orgOpt = new Option<string?>("--org", "Azure DevOps organization");
        var projectOpt = new Option<string?>("--project", "Azure DevOps project");

        var cmd = new Command("resolve", "Mark PR thread(s) as fixed")
        {
            prIdArg, threadOpt, fileOpt, repoOpt, orgOpt, projectOpt
        };

        cmd.SetHandler(async (int prId, int? threadId, string? filePath, string? repo, string? org, string? project) =>
        {
            if (threadId is null && filePath is null)
            {
                Console.Error.WriteLine("Error: Must specify --thread or --file");
                Environment.ExitCode = 1;
                return;
            }

            var config = ConfigResolver.Resolve(org, project, repo);
            using var client = new AzdoClient(config.Pat, config.Org, config.Project, config.Repo);

            if (threadId.HasValue)
            {
                await client.ResolveThreadAsync(prId, threadId.Value);
                Console.WriteLine($"Resolved thread {threadId.Value} on PR #{prId}");
            }
            else if (filePath is not null)
            {
                var threads = await client.GetThreadsAsync(prId);
                var matching = threads
                    .Where(t => t.ThreadContext?.FilePath == filePath
                        && !t.IsDeleted
                        && t.Status is "active" or "pending" or "")
                    .ToList();

                if (matching.Count == 0)
                {
                    Console.WriteLine($"No active threads found on {filePath}");
                    return;
                }

                foreach (var thread in matching)
                {
                    await client.ResolveThreadAsync(prId, thread.Id);
                }

                var ids = string.Join(", ", matching.Select(t => t.Id));
                Console.WriteLine($"Resolved {matching.Count} threads on {filePath}: {ids}");
            }
        }, prIdArg, threadOpt, fileOpt, repoOpt, orgOpt, projectOpt);

        return cmd;
    }
}
