# Plan: `azdo-pr` dotnet tool + Claude Code skills for PR review

## Context
We want Claude Code to assist with Azure DevOps PR review fixes. The current LINQPad script exports PR comments but isn't usable from Claude Code skills. We need a CLI tool that Claude Code can invoke, plus skills that orchestrate the workflow.

The LINQPad script (`Dev Ops/Export PR Comments.linq`) stays as-is for ad-hoc use. The new dotnet tool is the agent-facing interface.

## Architecture

```
azdo-pr (dotnet global tool)
  export <prId>       → writes PR-{id}/index.md + files/*.md to working dir
  resolve <prId>      → marks thread(s) as fixed in AzDO
  reply <prId>        → posts a reply (pushback / flag for discussion)

Claude Code skills (in .claude/ or CLAUDE.md)
  /pr-review <prId>   → export + guide agent through the review
  /pr-done <prId>     → summary of what was resolved vs flagged
```

## CLI Commands

### `azdo-pr export <prId>`
- Fetches PR metadata, threads, source file contents
- Writes `PR-{prId}/index.md` + `PR-{prId}/files/{path}.md` (same format as current script)
- Output goes to current working directory (not Desktop)
- `--repo <name>` flag (defaults to config or git remote detection)
- `--org <name>` and `--project <name>` flags (or env vars `AZDO_ORG`, `AZDO_PROJECT`)
- PAT from env var `AZDO_PAT`

### `azdo-pr resolve <prId> --thread <id>`
- Marks a single thread as "fixed" via `PATCH .../threads/{threadId}?api-version=7.1` with `{"status": "fixed"}`
- Outputs confirmation line

### `azdo-pr resolve <prId> --file <path>`
- Resolves ALL active threads on the given file path
- Outputs count + thread IDs resolved

### `azdo-pr reply <prId> --thread <id> --message "..." [--status pending|active]`
- Posts a reply comment to a thread via `POST .../threads/{threadId}/comments`
- Optionally updates thread status (default: leave as-is)
- Use cases:
  - **Pushback**: agent disagrees with comment, explains why → `--message "Intentional: ..." --status active`
  - **Flag for discussion**: agent can't decide, needs human → `--message "Needs human decision: ..." --status pending`

## dotnet tool project structure

```
azdo-pr/
  azdo-pr.csproj          # <PackAsTool>true</PackAsTool>, ToolCommandName=azdo-pr
  Program.cs              # Entry point, command parsing (System.CommandLine)
  AzdoClient.cs           # HTTP client (reuse pattern from LINQPad script)
  Commands/
    ExportCommand.cs      # export logic + markdown writer
    ResolveCommand.cs     # resolve thread(s)
    ReplyCommand.cs       # post reply + optional status change
  Models/
    PrInfo.cs
    PrThread.cs
    PrComment.cs
  MarkdownWriter.cs       # generates index.md + per-file .md files
```

### Key dependencies
- `System.CommandLine` for CLI parsing
- `System.Text.Json` (built-in) for AzDO API
- No other dependencies needed

## Output format (unchanged from current script)

### index.md
```markdown
# PR #1234 Review Comments
<!-- pr_url: https://dev.azure.com/... -->
<!-- generated: 2026-03-06 10:30 -->
<!-- repo: Squirrel.Mortgages -->
<!-- source_branch: feature/xyz -->

## Summary
12 active comments across 4 files

## Files
- [ ] `src/Services/Foo.cs` (3 comments) -> [files/src--Services--Foo.cs.md](files/src--Services--Foo.cs.md)
- [ ] `src/Models/Bar.cs` (2 comments) -> [files/src--Models--Bar.cs.md](files/src--Models--Bar.cs.md)

## All Threads
- [ ] Thread 42 | `src/Services/Foo.cs` line 15 | Should add null check here...
- [ ] Thread 57 | `src/Models/Bar.cs` line 88 | This logic seems wrong...
```

### Per-file .md (e.g. `files/src--Services--Foo.cs.md`)
```markdown
# /src/Services/Foo.cs
<!-- source_path: /src/Services/Foo.cs -->

---
## Thread 42 | Line 15
<!-- thread_id:42 status:active path:/src/Services/Foo.cs line:15 -->
[View in Azure DevOps](https://dev.azure.com/...?discussionId=42)

### Code Context
```csharp
  13: var result = GetValue();
  14: if (result != null)
  15:     DoSomething(result);  // <--
  16:     DoOther();
  17: }
```

### Comment
> Should add null check here
> -- _Jane Smith_ (2026-03-05 09:00)

- [ ] Addressed
```

## Claude Code Skills

### `/pr-review <prId>`
Skill definition (in CLAUDE.md or .claude/skills/):
1. Run `azdo-pr export <prId>` in the repo root
2. Read `PR-{prId}/index.md` to understand scope
3. For each file in the index:
   a. Read the per-file .md
   b. For each thread: read the source file, apply the fix
   c. Run `azdo-pr resolve <prId> --thread <id>` for fixes
   d. Run `azdo-pr reply <prId> --thread <id> --message "..."` for pushback/flags
4. After all files processed, summarize what was done

### `/pr-done <prId>`
1. Re-run `azdo-pr export <prId>` (to see current state)
2. If no active threads remain: "All comments addressed"
3. If threads remain: list them with their status

## AzDO API calls

| Command | API | Method |
|---------|-----|--------|
| export | `pullRequests/{prId}` | GET |
| export | `pullRequests/{prId}/threads` | GET |
| export | `repositories/{repo}/items?path=...&version=...` | GET (per file) |
| resolve | `pullRequests/{prId}/threads/{threadId}` | PATCH |
| reply | `pullRequests/{prId}/threads/{threadId}/comments` | POST |
| reply | `pullRequests/{prId}/threads/{threadId}` | PATCH (if --status) |

## Implementation order
1. Create `azdo-pr` project with `System.CommandLine` scaffolding
2. Port `AzdoClient` from LINQPad script (already written, just extract)
3. Implement `export` command (port markdown writer from LINQPad script)
4. Implement `resolve` command (new, simple PATCH call)
5. Implement `reply` command (new, POST + optional PATCH)
6. Pack as dotnet tool, test with `dotnet tool install --global --add-source ./nupkg`
7. Write Claude Code skill definitions

## Verification
1. `azdo-pr export 1234` → check PR-1234/ folder with index.md + files/*.md
2. `azdo-pr resolve 1234 --thread 42` → verify thread shows as fixed in AzDO web UI
3. `azdo-pr resolve 1234 --file /src/Foo.cs` → verify all threads on that file are fixed
4. `azdo-pr reply 1234 --thread 42 --message "test"` → verify reply appears in AzDO
5. `azdo-pr reply 1234 --thread 42 --message "needs discussion" --status pending` → verify reply + status change
6. Run `/pr-review` skill in Claude Code against a real PR

## Config / Auth
- `AZDO_PAT` env var (required)
- `AZDO_ORG` env var or `--org` flag
- `AZDO_PROJECT` env var or `--project` flag
- Repo name: `--repo` flag, or auto-detect from git remote URL
