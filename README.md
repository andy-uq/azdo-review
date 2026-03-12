# AzDoReview

A CLI tool and Claude Code skill set for reviewing Azure DevOps pull requests from the terminal.

`azdo-pr` exports PR threads as markdown, then Claude Code skills guide you through triaging, fixing, and resolving review comments — all without leaving your editor.

## How it works

1. **`azdo-pr`** (a .NET global tool) talks to the Azure DevOps API and gives you commands to export threads, post replies, resolve comments, and more.
2. **Claude Code skills** (slash commands) orchestrate the review workflow — triage, assess, plan, fix, reply, resolve — by calling `azdo-pr` under the hood.

## Setup

### Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- [Claude Code](https://claude.com/claude-code) CLI
- An Azure DevOps Personal Access Token (PAT) with **Code (Read & Write)** scope

### Install the CLI tool

```bash
# From the repo root
dotnet pack azdo-pr/azdo-pr.csproj -o ./nupkg
dotnet tool install --global --add-source ./nupkg azdo-pr
```

### Configure environment

```bash
export AZDO_PAT="your-personal-access-token"
export AZDO_ORG="your-org-name"        # or pass --org to each command
export AZDO_PROJECT="your-project"      # or pass --project, or let it auto-detect from git remote
```

## CLI commands

```
azdo-pr export <prId>                                    # export active PR threads as markdown
azdo-pr export <prId> --all                              # include resolved/fixed threads
azdo-pr status <prId>                                    # quick PR summary (thread counts, new activity)
azdo-pr diff <prId>                                      # files changed in the latest iteration
azdo-pr diff <prId> --from <iter> --to <iter>            # files changed between specific iterations
azdo-pr resolve <prId> --thread <threadId>               # mark a thread as fixed
azdo-pr reply <prId> --thread <id> --message "..."       # reply to a thread
azdo-pr reply <prId> --thread <id> --message "..." --status active  # reply and set status
azdo-pr comment <prId> --message "..."                   # new PR-level comment
azdo-pr comment <prId> --message "..." --file <path> --line <n>     # new file-level comment
```

Exported threads are written to `.azdo/reviews/PR-<prId>/` as markdown files — one index plus one file per changed file with threads.

Re-exporting a previously exported PR marks new comments with `(NEW)` and new replies with `(NEW REPLIES)` so you can spot what changed.

## Skills (Claude Code slash commands)

Skills are markdown prompt templates that Claude Code loads when you type a slash command. They define multi-step, interactive workflows.

### Available skills

| Command | Description |
|---------|-------------|
| `/pr-review <prId>` | Full review workflow — export, triage, assess, plan, fix, resolve |
| `/pr-resume <prId>` | Reload context from a previous session (after `/clear` or new conversation) |
| `/pr-status <prId>` | Quick status check — thread counts, plan progress |

All skills accept optional `--project <name>` and `--repo <name>` flags if auto-detection from git remote doesn't work.

### `/pr-review` workflow

The main skill. Handles both fresh reviews and follow-ups automatically.

**Fresh review** goes through six phases:

1. **Export & Triage** — fetch threads, tag each by category (`[critical]`, `[security]`, `[medium]`, `[trivial]`, etc.) and effort (`low` / `med` / `high`)
2. **Assess** — read the actual source code, decide **Fix**, **Pushback**, or **Discuss** for each thread
3. **Plan** — write an execution plan (`plan.md`) with commit grouping and exact reply messages
4. **Execute** — implement fixes, build, test, then resolve threads on AzDo after you push
5. **PR-level threads** — handle threads not tied to a specific file
6. **Summary** — final report of what was resolved, replied to, or flagged

**Follow-up** (when a previous export exists) re-exports with `--all`, identifies new reviewer activity, categorises responses (agreed, follow-up question, disagreed, reactivated), and handles each appropriately.

The skill pauses for your confirmation at every decision point — triage, assessment, plan approval, commit, and before posting any reply to AzDo.

### `/pr-resume`

Loads existing review state (`index.md`, `plan.md`, `assessment.md`, `triage.md`) so you can pick up where you left off without re-running the full workflow.

### `/pr-status`

Runs `azdo-pr status` and checks plan progress. Suggests `/pr-review` if new comments are detected since the last export.

### Skill file locations

Claude Code discovers skills from specific directories. This project keeps three identical copies in sync:

| Path | Scope |
|------|-------|
| `skills/` | Source of truth (checked into repo) |
| `.claude/commands/` | Project-scoped (auto-discovered in this repo) |
| `~/.claude/commands/` | Global (available in any repo) |

After editing a skill in `skills/`, copy it to the other two locations.

## Project structure

```
azdo-pr/          .NET 9 global tool source
skills/           Skill definitions (source of truth)
.claude/commands/ Project-scoped skill copies
docs/             Planning docs and TODO
CLAUDE.md         Claude Code project instructions
```

## Updating the tool

After making changes to `azdo-pr/`:

1. Bump `<Version>` in `azdo-pr/azdo-pr.csproj` (required — `dotnet tool update` no-ops without a version bump)
2. Build: `dotnet build azdo-pr/azdo-pr.csproj`
3. Pack and update: `dotnet pack azdo-pr/azdo-pr.csproj -o ./nupkg && dotnet tool update --global --add-source ./nupkg azdo-pr`
