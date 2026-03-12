# PR Review Fix Guide

Guide the user through fixing Azure DevOps PR review comments.

## How to interact with Azure DevOps

All AzDO interactions use the `azdo-pr` CLI tool via Bash. It is already on PATH — run it exactly as shown below (e.g. `azdo-pr export 1234`). Do NOT use MCP tools, APIs, or web UIs.

```
azdo-pr export <prId>                                              # fetch active PR threads to .azdo/reviews/
azdo-pr export <prId> --all                                        # fetch ALL threads (including resolved/fixed)
azdo-pr status <prId>                                              # quick PR summary (no file fetches)
azdo-pr diff <prId>                                                # files changed in latest iteration
azdo-pr diff <prId> --from <iter> --to <iter>                      # files changed between specific iterations
azdo-pr resolve <prId> --thread <threadId>                         # mark thread as fixed
azdo-pr reply <prId> --thread <id> --message "..." [--status ...]  # reply to a thread
azdo-pr comment <prId> --message "..."                             # new PR-level comment
azdo-pr comment <prId> --message "..." --file <path> [--line <n>]  # new file/line comment
```

When re-exporting a PR that was previously exported, the tool automatically detects the previous export timestamp and marks new comments with `(NEW)` and new replies with `### Reply (NEW)`. Threads with new replies get `(NEW REPLIES)` in their heading. The `--all` flag includes resolved/fixed threads so you can see replies on threads that were resolved after the previous session.

Threads where the PR author posted the last comment are tagged `(AWAITING REVIEWER)`. These threads are waiting for the reviewer to respond — skip them during follow-up unless the reviewer has since replied (the tag will be absent if new reviewer activity exists).

Config comes from env vars (`AZDO_PAT`, `AZDO_ORG`) and git remote auto-detection.

## Arguments

$ARGUMENTS: PR ID (required). Optionally `--project <name> --repo <name>` if not auto-detected from git remote. Pass these through to all `azdo-pr` commands.

## Phase 0: Detect follow-up

Before starting, check if `.azdo/reviews/PR-<prId>/index.md` already exists.

**If NO previous export exists** — this is a fresh review. Skip to Phase 1.

**If a previous export exists** — this is a follow-up session. Run the follow-up workflow instead of Phases 1–6:

### Follow-up workflow

1. Run `azdo-pr status <prId>` first for a quick check. If it shows 0 new comments since last export, tell the user "No new activity on this PR since the last export" and stop.

2. Run `azdo-pr diff <prId>` to see what files changed in the latest iteration. This tells you what code the author pushed since your last review — focus new thread assessment on these files.

3. Run `azdo-pr export <prId> --all` to re-export with all threads (including resolved). The `--all` flag is critical for follow-up — reviewers often reply and resolve in the same action, and without `--all` you'd miss their reply.

4. Read `.azdo/reviews/PR-<prId>/index.md` — the "Changed Files" section shows what was modified, and "New in iteration N" highlights files that changed in the latest push. The summary line shows new comment count since previous export.

5. Read ALL per-file markdown files. Focus on sections marked with `(NEW)` or `(NEW REPLIES)` — these are the threads with new activity. Thread headings now show which iteration they were created on (e.g. `(iter 3)`).

6. Also check `.azdo/reviews/PR-<prId>/plan.md` if it exists — it tracks what was already completed in previous sessions.

7. For each thread with new replies, categorise the reply:

   - **Reviewer agreed / approved the fix** — no action needed. Note it.
   - **Reviewer asked a follow-up question** — assess and draft a response.
   - **Reviewer disagreed with pushback** — re-read the code and reassess. Either:
     - Concede: mark as Fix and plan the change.
     - Counter: draft a follow-up reply with additional reasoning.
     - Escalate to user: mark as Discuss.
   - **Reviewer reactivated a resolved thread** — the fix wasn't sufficient. Reassess.
   - **New threads appeared** (not replies to existing ones) — treat as fresh review items. For new threads, apply the same tagging system from Phase 1: assign a `[tag]` and `[effort]` estimate. If there are more than 5 new threads, present a mini-triage before the follow-up assessment.

8. Present the follow-up assessment:

   ```
   ## PR #<id> Follow-up — <n> threads with new activity

   ### Acknowledged (no action needed)
   Thread <id>: Reviewer approved fix — "<brief quote>"

   ### Needs response
   Thread <id> (line <n>): Reviewer asked: "<brief quote>"
   -> Reply: <draft response>

   Thread <id> (line <n>): Reviewer disagreed with pushback: "<brief quote>"
   -> Fix: <what to change> (conceding)
   OR
   -> Reply: <counter-argument> (continuing pushback)
   OR
   -> Discuss: <why this needs human input>

   ### Reactivated
   Thread <id> (line <n>): Reviewer reopened — "<brief quote>"
   -> Fix: <what to change differently>

   ### New threads
   Thread <id> (line <n>): <summary>
   -> Fix / Pushback / Discuss: <assessment>
   ```

9. Wait for the user to confirm or override decisions.

10. For any items requiring code changes, follow the Phase 3 (Plan) and Phase 4 (Execute) workflow as normal — write a plan, get approval, implement, commit, resolve.

11. For reply-only items, draft the replies and post them after user approval:
   - `azdo-pr reply <prId> --thread <id> --message "<text>"` (for follow-up responses)
   - `azdo-pr reply <prId> --thread <id> --message "<text>" --status active` (continuing a disagreement)

12. End with a follow-up summary:

    ```
    ## PR #<id> Follow-up Summary

    New comments reviewed: <count>
    Acknowledged (no action): <count>
    Fixed: <count>
    Replied: <count>
    Escalated to discuss: <count>

    ### Details
    - Thread <id>: <action> — <one-line summary>
    ...
    ```

After the follow-up workflow completes, stop. Do NOT fall through to Phase 1.

---

## Phase 1: Export and triage (fresh review only)

1. Run `azdo-pr export <prId>` to fetch the PR threads.

2. Read `.azdo/reviews/PR-<prId>/index.md` and ALL per-file markdown files to understand every thread.

3. Count the total active threads.

   **5 or fewer threads** — skip tagging, go to Phase 2.

   **More than 5 threads** — categorise each thread with a tag and effort estimate:

   Tags (pick one per thread):
   `[critical]` `[security]` `[architecture]` `[structural]` `[database]` `[ef-core]` `[performance]` `[medium]` `[trivial]` `[typo]` `[naming]` `[style]` `[docs]` `[test]` `[config]` `[discuss]`

   Use `[discuss]` when the reviewer is asking a question rather than requesting a change, or when the code agent can't determine the right action without human input. These threads should not be auto-fixed — they require conversation.

   Effort (pick one per thread):
   - **low**: one-liner or obvious change, no risk of side effects
   - **med**: a few lines, may need to check callers or related code
   - **high**: significant rework, cross-cutting change, or needs careful design thought

   Present the triage:

   ```
   ## PR #<id> Triage — <total> threads

   Thread <id> [tag] [effort] <file>:<line> — <one-line summary>
   Thread <id> [tag] [effort] <file>:<line> — <one-line summary>
   ...

   ### By tag
   [critical]: 2  [medium]: 3  [trivial]: 1  [docs]: 1

   ### By effort
   [low]: 4  [med]: 2  [high]: 1
   ```

   This helps the user decide when to `/clear` context and start fresh sessions — high-effort threads may warrant their own session.

   Tell the user: "Triage written to `.azdo/reviews/PR-<prId>/triage.md` — update tags/effort if you like, then tell me to proceed."
   Stop and wait. If we've hit the triage threshold this is a large or problematic PR — give the user time to read and digest before continuing.

4. After triage is confirmed, write `.azdo/reviews/PR-<prId>/triage.md` with the full triage output (so it persists if the session is interrupted). Also update each per-file markdown by inserting the tag into the thread heading lines — change `## Thread <id>` to `## Thread <id> [tag] [effort]`. This ensures the tags are visible when re-reading files in later phases.

## Phase 2: Assess

Work through threads grouped by tag priority (critical → security → architecture → structural → database → ef-core → performance → medium → trivial → typo → naming → style → docs → test → config). Within the same priority, group by file. For untagged PRs, work file by file.

For each group of threads:

1. Read the actual source files at the paths indicated in the thread metadata.

2. Assess each thread against the code. Decide one of:
   - **Fix**: The comment is valid — you will change the source code.
   - **Pushback**: The current code is intentional, the comment is incorrect, or the suggested change would make things worse. You are explicitly encouraged to push back when you believe the reviewer is wrong — but pushback is always a discussion, never a dismissal. The reviewer took time to find and articulate the concern, so your pushback must show equal or greater rigour: explain your reasoning clearly, reference the specific code, and acknowledge what the reviewer was trying to achieve. The user will review your pushback message carefully before it's posted.
   - **Discuss**: Needs human judgement — either the reviewer asked a question, or you've read the code and genuinely can't determine the right call. A thread tagged `[discuss]` in triage will usually land here, but any thread can become Discuss during assessment if the code turns out to be more nuanced than the triage suggested.

3. For threads assessed as **Fix**, consider whether a test can expose the flaw the reviewer identified. If the issue is testable (logic error, missing validation, incorrect behaviour, edge case, etc.):
   - Write a test that **fails against the current code**, proving the reviewer's concern is real.
   - The test should **pass after the fix** is applied.
   - Note the test in the assessment with `+test`.
   - Not everything is testable — skip tests for pure style, naming, typo, docs, config, and structural/refactoring changes.

4. Present your assessment to the user:

   ```
   ## <file path>

   Thread <id> [tag] [effort] (line <n>): <one-line summary of reviewer's comment>
   -> Fix +test: <what you will change> / <what the test verifies>

   Thread <id> [tag] [effort] (line <n>): <one-line summary of reviewer's comment>
   -> Fix: <what you will change> (no test — style/structural change)

   Thread <id> [tag] [effort] (line <n>): <one-line summary of reviewer's comment>
   -> Pushback: <why the current code is correct>

   Thread <id> [tag] [effort] (line <n>): <one-line summary of reviewer's comment>
   -> Discuss: <why this needs human input>
   ```

4. Wait for the user to confirm or override any decision.

5. After the user confirms the assessment, write it to `.azdo/reviews/PR-<prId>/assessment.md`:

   ```markdown
   # PR #<id> Assessment
   <!-- generated: YYYY-MM-DD HH:MM -->

   ## <file path>
   - Thread <id> [tag] [effort]: **Fix** +test — <summary>
   - Thread <id> [tag] [effort]: **Pushback** — <summary>
   - Thread <id> [tag] [effort]: **Discuss** — <summary>
   ...
   ```

   This ensures a new session can read the assessment if interrupted between Phase 2 and Phase 3.

## Phase 3: Plan

Before writing a new plan, check if `.azdo/reviews/PR-<prId>/assessment.md` exists. If it does, read it and use it as the basis for the plan instead of re-assessing.

Before touching any code or posting to AzDO, write the full execution plan to `.azdo/reviews/PR-<prId>/plan.md`. This file serves two purposes: the user approves it before you proceed, and if the session is interrupted or `/clear`ed, a new session can read the plan and pick up where it left off.

The plan should contain:

```markdown
# PR #<id> Execution Plan
<!-- status: in-progress -->
<!-- last-completed: none -->

## Commits

### Commit 1: "PR #<id>: <description>"
- [ ] Thread <id> [tag] [effort]: fix <file>:<line> — <what changes>
- [ ] Thread <id> [tag] [effort]: fix <file>:<line> — <what changes>

### Commit 2: "PR #<id>: <description>"
- [ ] Thread <id> [tag] [effort]: fix <file>:<line> — <what changes>

## Replies

- [ ] Thread <id> [tag]: reply (pushback)
  > <exact message to post>

- [ ] Thread <id> [tag]: reply (discuss)
  > <exact message to post>

## New comments

- [ ] <file>:<line>
  > <exact message to post>

## PR-level threads

- [ ] Thread <id>: <pending user decision>
```

Tell the user: "Plan written to `.azdo/reviews/PR-<prId>/plan.md` — review and edit, then tell me to proceed."
Stop and wait for approval.

As you complete each item during Phase 4, check off the boxes (`- [x]`) and update `<!-- last-completed: ... -->` so progress is tracked. If a new session picks up this plan, it should read the file, skip checked items, and continue from where the previous session stopped.

### Commit grouping rules

- **`[critical]`, `[security]`, `[architecture]`, `[structural]`, `[database]`, `[ef-core]`**: One commit per thread. These are significant changes the reviewer inspects individually.
- **`[trivial]`, `[typo]`, `[naming]`, `[style]`, `[docs]`, `[config]`**: Batch all of the same tag into one commit.
- **`[medium]`, `[performance]`, `[test]`**: Batch if same file or closely related, otherwise separate.
- **Untagged (5 or fewer threads)**: One commit per file, or one total if changes are small.

Commit message format:
- Single thread: `PR #<prId>: <brief description>`
- Batched: `PR #<prId>: <tag> fixes — <brief summary>`

## Phase 4: Execute

For each planned group of changes, in order:

1. For threads marked `+test`: write the test first. Run it and confirm it **fails** against the current code (red). If it passes, the test doesn't prove the flaw — reconsider the test or the fix.
2. Edit the source files to apply the fix.
3. Run the test again and confirm it **passes** (green). If it doesn't, iterate on the fix.
4. Verify the project builds: `dotnet build` (or the appropriate build command). If the build breaks, fix it before continuing. If you can't fix it, stop and tell the user.
5. Run the full test suite (`dotnet test`) to check for regressions. If tests fail, fix before continuing.
6. Tell the user what to commit. List the changed files and suggest a commit message. **Do NOT run git add, git commit, or git push** — the user will do this themselves.
7. Wait for the user to confirm they've committed and pushed before resolving threads on AzDO. Do NOT resolve threads until the code is pushed — otherwise the reviewer sees "fixed" without the fix.
8. Resolve the corresponding threads on AzDO using reply templates:
   - Simple fix: `azdo-pr reply <prId> --thread <id> --message done` then `azdo-pr resolve <prId> --thread <id>`
   - Removed code: `azdo-pr reply <prId> --thread <id> --message deleted` then `azdo-pr resolve <prId> --thread <id>`
   - Involved fix: `azdo-pr reply <prId> --thread <id> --message "<what was done>"` then `azdo-pr resolve <prId> --thread <id>`

Then post any replies and new comments (these don't need a commit):
- `azdo-pr reply <prId> --thread <id> --message "<text>" --status active` (pushback)
- `azdo-pr reply <prId> --thread <id> --message "<text>" --status pending` (discuss)
- `azdo-pr comment <prId> --message "<text>" [--file <path> --line <n>]`

## Phase 5: PR-level threads

After all file threads are handled, check for PR-level threads (those without a file path in the index). Present each one to the user with the comment text and ask how to handle it: resolve, reply, or skip.

## Phase 6: Summary

```
## PR #<id> Review Summary

Resolved: <count> threads
Replied (pushback): <count> threads
Flagged for discussion: <count> threads
Skipped: <count> threads
Commits pushed: <count>

### By tag (if triaged)
[critical]: 2 (all resolved)  [medium]: 3 (2 resolved, 1 pushback)  [trivial]: 1 (resolved)

### Details
- Thread <id> [tag]: <action> — <one-line summary>
...
```
