# PR Context

Load Azure DevOps PR review context into this session. Use this after `/clear` or at the start of a session where you need to work on PR review fixes without running the full `/pr-review` workflow.

## Arguments

$ARGUMENTS: PR ID (required). Optionally `--project <name> --repo <name>` if not auto-detected from git remote.

## Instructions

1. Run `azdo-pr status <prId>` to load current PR state.

2. Check for existing review state in `.azdo/reviews/PR-<prId>/`:
   - Read `index.md` if it exists — this has the full thread listing, changed files, and status.
   - Read `plan.md` if it exists — this tracks what's been done and what's remaining. Checked-off items are complete; unchecked items still need work.
   - Read `assessment.md` if it exists — this has the agreed Fix/Pushback/Discuss decisions.
   - Read `triage.md` if it exists — this has thread tags and effort estimates.

3. Summarize what you found:
   - PR title, status, active thread count
   - Plan progress (e.g., "3 of 5 items complete")
   - What's next (e.g., "2 fixes remaining in plan.md, then 1 pushback reply to post")

4. Tell the user you're ready to continue. From here they can ask you to:
   - Continue executing the plan (`plan.md` has the details)
   - Make specific code fixes
   - Post replies or resolve threads on AzDo

## AzDo CLI reference

All AzDo interactions use the `azdo-pr` CLI tool. It is already on PATH — run it exactly as shown below (e.g. `azdo-pr status 1234`):

```
azdo-pr export <prId>                                              # fetch active PR threads to .azdo/reviews/
azdo-pr export <prId> --all                                        # include resolved/fixed threads
azdo-pr status <prId>                                              # quick PR summary (no file fetches)
azdo-pr diff <prId>                                                # files changed in latest iteration
azdo-pr diff <prId> --from <iter> --to <iter>                      # files changed between specific iterations
azdo-pr resolve <prId> --thread <threadId>                         # mark thread as fixed
azdo-pr reply <prId> --thread <id> --message "..." [--status ...]  # reply to a thread
azdo-pr comment <prId> --message "..."                             # new PR-level comment
azdo-pr comment <prId> --message "..." --file <path> [--line <n>]  # new file/line comment
```

Threads tagged `(AWAITING REVIEWER)` are waiting for the reviewer — the PR author posted the last comment. Skip these unless the user specifically asks about them.

## Code fix workflow

When fixing code for a PR thread:

1. Read the thread details in the per-file markdown (`.azdo/reviews/PR-<prId>/files/*.md`)
2. Read the actual source file at the path indicated
3. Make the fix
4. Build: `dotnet build` (or the project's build command)
5. Run tests: `dotnet test` (if applicable)
6. Tell the user what to commit — do NOT run git add, git commit, or git push yourself
7. **STOP and WAIT.** Tell the user exactly what you will do next, including the reply template or message text and which threads will be resolved. Example:

   > Ready for you to commit and push. Once confirmed, I'll:
   > - Reply to thread 75867 with `done` and resolve
   > - Reply to thread 75870: "Extracted helper method and added null check" and resolve

   Do NOT proceed until the user explicitly confirms they have pushed. Do NOT resolve threads, post replies, or take any AzDo action until the user says so.
8. Only after the user confirms the push, resolve the thread:
   - Simple fix: `azdo-pr reply <prId> --thread <id> --message done` then `azdo-pr resolve <prId> --thread <id>`
   - Removed code: `azdo-pr reply <prId> --thread <id> --message deleted` then `azdo-pr resolve <prId> --thread <id>`
   - Involved fix: `azdo-pr reply <prId> --thread <id> --message "<what was done>"` then `azdo-pr resolve <prId> --thread <id>`
9. Check off the item in `plan.md` if one exists

## Posting replies (pushback / discuss)

For reply-only items (no code change needed), show the user the exact message you plan to post and the exact command, then **wait for approval** before sending. Example:

> I'll post this reply to thread 42:
> "The null check is intentional — GetValue() can return null when the cache is cold, see CacheProvider.cs:87."
> `azdo-pr reply 9115 --thread 42 --message "..." --status active`

Do NOT post replies to AzDo without the user confirming the message content first.

- Pushback: `azdo-pr reply <prId> --thread <id> --message "<text>" --status active`
- Discuss: `azdo-pr reply <prId> --thread <id> --message "<text>" --status pending`
