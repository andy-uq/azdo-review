# azdo-pr TODO

## Done

- [x] Core CLI: export, resolve, reply, comment commands
- [x] Markdown export with per-file breakdowns and code context
- [x] Re-export detection with NEW markers for comments/replies
- [x] `--all` flag to include resolved/fixed threads
- [x] PR title and description in export (1b)
- [x] PR status, draft flag, merge status, reviewers in export (2a)
- [x] Iteration count and latest commit in export (2b)
- [x] Fix 401 error message to include org-specific PAT URL (1a)
- [x] Skill: Phase 0 follow-up workflow with re-export and NEW detection
- [x] Skill: Triage system with tags and effort estimates
- [x] Skill: Assessment persistence to assessment.md (2c)
- [x] Skill: Tag new threads in follow-up with [tag] [effort] (2d)
- [x] Skill: Phase 3 checks for existing assessment.md before re-assessing
- [x] Three skill files synced (skills/, .claude/commands/, ~/.claude/commands/)
the 
## Priority 3: Diff and change awareness

- [x] Show PR diff summary in export — "Changed Files" section with change types (edit, add, delete, rename)
- [x] Fetch and display iteration-level changes — "New in iteration N" subsection shows what changed in latest push
- [x] Per-thread iteration context — thread headings show `(iter N)` from AzDo `iterationContext` field

## Priority 4: Smarter export

- [ ] Export only threads with new activity since last export (skip unchanged threads in follow-up to reduce noise)
- [x] `azdo-pr status <prId>` command — quick PR summary, thread counts, new comments since last export, no file fetches
- [x] `azdo-pr diff <prId>` command — show files changed between iterations, with `--from`/`--to` flags
- [x] Skill follow-up uses `status` for quick check before full re-export, and `diff` for iteration awareness

## Robustness

- [ ] Handle pagination in GetThreadsAsync / GetIterationsAsync (AzDo API paginates at 200+ items)
- [ ] Handle empty/null PR description gracefully in MarkdownWriter (currently writes `## Description` with empty blockquote)
- [ ] Handle PR status string edge cases — what if Status is empty or an unexpected value? (the `char.ToUpper(pr.Status[0])` will throw on empty string)
- [ ] Retry on transient HTTP failures (429, 503) with backoff
- [ ] `azdo-pr export` should report iteration count in console output

## Skill improvements

- [x] ~`/pr-done <prId>` skill~ — superseded by `/pr-status` (quick check) and `/pr-review` follow-up (full re-export + action)
- [ ] Skill should detect when assessment.md is stale (e.g. new threads appeared since it was written) and prompt for re-assessment
- [ ] Skill Phase 4 should update plan.md checkboxes as it works — verify this actually happens in practice
- [ ] Skill follow-up should read assessment.md from previous session to compare what was planned vs what the reviewer responded to

## Tech debt

- [ ] `resolve --file` path matching is exact string comparison — should normalize slashes and leading `/`
- [ ] MarkdownWriter.WriteOutputAsync has 10 parameters — consider a context/options object
- [ ] Export writes all files every time — could skip unchanged per-file .md files for faster re-export
- [ ] Clean up old .nupkg files in `./nupkg/` after successful update
