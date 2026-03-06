using System.CommandLine;
using AzdoPr.Commands;

var rootCommand = new RootCommand("Azure DevOps PR review tool for Claude Code");

rootCommand.AddCommand(ExportCommand.Create());
rootCommand.AddCommand(ResolveCommand.Create());
rootCommand.AddCommand(ReplyCommand.Create());
rootCommand.AddCommand(CommentCommand.Create());

return await rootCommand.InvokeAsync(args);
