# PR Status Check

Quick status check on an Azure DevOps pull request.

## Arguments

$ARGUMENTS: PR ID (required). Optionally `--project <name> --repo <name>` if not auto-detected from git remote.

## Instructions

1. Run `azdo-pr status <prId>` (pass through any `--project`/`--repo` flags).

2. If there's a previous export, also check if `.azdo/reviews/PR-<prId>/plan.md` exists. If it does, read it and report how many items are checked off vs remaining.

3. Present the status concisely. If there are new comments since the last export, suggest running `/pr-review <prId>` to handle them.
