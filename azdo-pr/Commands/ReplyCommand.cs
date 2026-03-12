using System.CommandLine;

namespace AzdoPr.Commands;

public static class ReplyCommand
{
    public static Command Create()
    {
        var prIdArg = new Argument<int>("prId", "Pull request ID");
        var threadOpt = new Option<int>("--thread", "Thread ID to reply to") { IsRequired = true };
        var messageOpt = new Option<string>("--message", "Reply message content") { IsRequired = true };
        var statusOpt = new Option<string?>("--status", "Optionally update thread status (active, pending, fixed, closed)");
        var repoOpt = new Option<string?>("--repo", "Repository name");
        var orgOpt = new Option<string?>("--org", "Azure DevOps organization");
        var projectOpt = new Option<string?>("--project", "Azure DevOps project");

        var cmd = new Command("reply", "Post a reply to a PR thread")
        {
            prIdArg, threadOpt, messageOpt, statusOpt, repoOpt, orgOpt, projectOpt
        };

        cmd.SetHandler(async (int prId, int threadId, string message, string? status, string? repo, string? org, string? project) =>
        {
            var config = ConfigResolver.Resolve(org, project, repo);
            using var client = new AzdoClient(config.Pat, config.Org, config.Project, config.Repo);

            message = ExpandTemplate(message);
            await client.PostReplyAsync(prId, threadId, message);
            Console.WriteLine($"Replied to thread {threadId} on PR #{prId}");

            if (status is not null)
            {
                await client.UpdateThreadStatusAsync(prId, threadId, status);
                Console.WriteLine($"Updated thread {threadId} status to '{status}'");
            }
        }, prIdArg, threadOpt, messageOpt, statusOpt, repoOpt, orgOpt, projectOpt);

        return cmd;
    }

    private static string ExpandTemplate(string message) => message.ToLowerInvariant().Trim() switch
    {
        "done" => "👍",
        "doh" => "🤦",
        "deleted" => "✂️ Removed",
        _ => message,
    };
}
