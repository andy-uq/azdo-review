using System.CommandLine;

namespace AzdoPr.Commands;

public static class CommentCommand
{
    public static Command Create()
    {
        var prIdArg = new Argument<int>("prId", "Pull request ID");
        var messageOpt = new Option<string>("--message", "Comment text") { IsRequired = true };
        var fileOpt = new Option<string?>("--file", "File path to comment on (omit for PR-level comment)");
        var lineOpt = new Option<int?>("--line", "Line number in the file");
        var repoOpt = new Option<string?>("--repo", "Repository name");
        var orgOpt = new Option<string?>("--org", "Azure DevOps organization");
        var projectOpt = new Option<string?>("--project", "Azure DevOps project");

        var cmd = new Command("comment", "Create a new comment thread on a PR")
        {
            prIdArg, messageOpt, fileOpt, lineOpt, repoOpt, orgOpt, projectOpt
        };

        cmd.SetHandler(async (int prId, string message, string? file, int? line, string? repo, string? org, string? project) =>
        {
            if (line.HasValue && file is null)
            {
                Console.Error.WriteLine("Error: --line requires --file");
                Environment.ExitCode = 1;
                return;
            }

            var config = ConfigResolver.Resolve(org, project, repo);
            using var client = new AzdoClient(config.Pat, config.Org, config.Project, config.Repo);

            var threadId = await client.CreateThreadAsync(prId, message, file, line);

            if (file is not null)
            {
                var lineStr = line.HasValue ? $" line {line}" : "";
                Console.WriteLine($"Created thread {threadId} on {file}{lineStr} in PR #{prId}");
            }
            else
            {
                Console.WriteLine($"Created PR-level thread {threadId} on PR #{prId}");
            }
        }, prIdArg, messageOpt, fileOpt, lineOpt, repoOpt, orgOpt, projectOpt);

        return cmd;
    }
}
