# AzDoReview

Azure DevOps PR review tool for Claude Code.

## Project structure

- `azdo-pr/` - .NET 9 global tool (`azdo-pr`) — commands: export, status, diff, resolve, reply, comment
- `docs/` - Planning documents and todo
- `skills/` - Project-local skills (Claude Code slash commands): pr-review, pr-resume, pr-status
- `.claude/commands/` - Project-scoped Claude commands (same content as skills/)
- `~/.claude/commands/` - Global Claude commands (same content — works in any repo)

## Building

```bash
dotnet build azdo-pr/azdo-pr.csproj
```

## Installing / updating global tool

```bash
dotnet pack azdo-pr/azdo-pr.csproj -o ./nupkg
dotnet tool update --global --add-source ./nupkg azdo-pr
```

If the tool isn't installed yet, use `dotnet tool install --global --add-source ./nupkg azdo-pr` instead.

**Important:** `dotnet tool update` requires a version bump — it silently no-ops if the version hasn't changed. Always bump `<Version>` in `azdo-pr/azdo-pr.csproj` before packing.

## Deploy checklist for changes

After modifying the tool or skill:

1. **Bump version** in `azdo-pr/azdo-pr.csproj` (`<Version>` tag)
2. **Build**: `dotnet build azdo-pr/azdo-pr.csproj`
3. **Pack + update**: `dotnet pack azdo-pr/azdo-pr.csproj -o ./nupkg && dotnet tool update --global --add-source ./nupkg azdo-pr`
4. **Sync all 3 skill locations** if skills changed — they must stay identical:
   - `skills/`
   - `.claude/commands/`
   - `~/.claude/commands/`

## Configuration

- `AZDO_PAT` env var (required) - Azure DevOps personal access token
- `AZDO_ORG` env var or `--org` flag
- `AZDO_PROJECT` env var or `--project` flag
- `--repo` flag or auto-detect from git remote URL
