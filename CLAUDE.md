# AzDoReview

Azure DevOps PR review tool for Claude Code.

## Project structure

- `azdo-pr/` - .NET 9 global tool (`azdo-pr`) for interacting with AzDO PR threads
- `docs/` - Planning documents

## Building

```bash
dotnet build azdo-pr/azdo-pr.csproj
```

## Installing as global tool (for testing)

```bash
dotnet pack azdo-pr/azdo-pr.csproj -o ./nupkg
dotnet tool install --global --add-source ./nupkg azdo-pr
```

## Configuration

- `AZDO_PAT` env var (required) - Azure DevOps personal access token
- `AZDO_ORG` env var or `--org` flag
- `AZDO_PROJECT` env var or `--project` flag
- `--repo` flag or auto-detect from git remote URL
