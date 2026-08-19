# Handover — autonomous backlog run

**Repo:** `C:\Projects\Deluno` (private). `AGENTS.md` says
`C:\Users\User\Projects\Deluno` — that is wrong.
**Baseline:** `main` @ `39ac8db`. CI green, working tree clean (bar the
long-standing untracked `.tmp-*` files), in sync with origin.
**Scope:** 37 issues open. Two are epics (#78, #106) — they close when their
children do. #158 is a note, not a work item.

---

## The goal

Every issue either **closed with a remediation commit and a closure note**, or
**answered in chat** because it needs a decision only the owner can make.
Nothing left silently open. No narrating progress between issues — work through
them.

---

## The single most important instruction

**Deluno must work at 20,000+ movies and TV shows, and libraries only grow.**

This is the owner's stated top priority and it overrides the old issue ordering.
It does not mean "add a limit" — it means **nothing may be O(library size)**.
Filter, sort and limit in SQL. Paginate. Never load a whole catalogue to pick a
few rows. A cap of 50,000 is the same bug with a bigger number.

Three lessons from the session that produced this handover, all learned the
expensive way:

1. **Matching the issue's spec is not the same as the change being correct.**
   A rate limiter was merged that would have broken the app for anyone with two
   browser tabs open. It passed its own gate. It took the owner asking to catch
   it. Ask what the change does to a *real user with a large library*, not just
   whether the tests are green.

2. **Measure before choosing a fix.** Twice the obvious fix was the wrong one.
   Adding an index did nothing until a `GROUP BY` was removed. Extracting 30
   fields from a metadata blob turned out to be unnecessary because none of them
   could ever be populated. `EXPLAIN QUERY PLAN` and a seeded database settled
   both in minutes.

3. **Ask what happens on the unhappy path.** The metadata backfill change was
   correct for titles the provider matches, and would have been a permanent hot
   loop against the provider for titles it cannot. Nothing in the dev fixtures
   surfaced it, because they all match.

---

## Decisions already made — do not re-ask

| Question | Answer |
|---|---|
| How work lands | **PR per issue, auto-merge on green.** Branch protection stays on. Never merge red. |
| Scale target | **20,000+ items, unbounded growth.** Nothing may scale with library size. |
| Large fixtures | **Not needed to find these bugs** — an unbounded `SELECT` is unbounded at any row count. They *are* needed to prove a speedup or test import. |
| Naming (#149) | Settled. `docs/MEDIA_AUTOMATION_TERMINOLOGY.md` is normative. |
| Breaking the public API | **Allowed.** Update `docs/external-integration-api.md` in the same PR. #142 shipped `/api/v1` versioning, so breaking changes have a home. |
| Genuine blockers | **Ask in chat and wait.** One clear question, options, and a recommendation. |

"Genuine blocker" means: two defensible answers, and picking wrong costs real
rework or changes what a user sees. Everything else — pick, do it, say why.

---

## The gate — after every commit

```powershell
Get-Process -Name "Deluno.Host" -ErrorAction SilentlyContinue | Stop-Process -Force
dotnet build Deluno.slnx
```

Then all seven suites. **Must not drop below:**

| Suite | Count |
|---|---|
| Persistence | **264** |
| Platform | **86** |
| Movies | **30** |
| Series | **24** |
| Integrations | **32** |
| Tray | **3** |
| Worker | **23** |
| Playwright (`npm run test:web`, from repo root) | **189** |

A drop is a regression, not a test that needs updating. The one sanctioned
exception in this repo's history was #121 deliberately moving 20 tests out of
Integrations; it was stated in the PR body.

---

## Per-issue loop

1. `gh issue view <n>` — read it and its comments. Several issues carry a
   "Status: may already be fixed" preamble that is **stale**; verify against
   `main` before believing either the issue or the preamble.
2. Branch: `fix/<n>-<slug>`.
3. Implement. Small commits; move files and change them in separate commits.
4. **Ask the scale question**: what does this do at 20,000 items? What does it
   do when the provider/disk/network says no?
5. Run the gate. Verify live where user-visible — start the app, drive a real
   round trip, check the console and `.deluno/logs/backend.log`.
6. `gh pr create`, then `gh pr merge <n> --auto --squash`.
   Note: `gh pr merge --auto` without the PR number sometimes errors with
   `enablePullRequestAutoMerge`; pass the number explicitly and re-run.
7. Close with a note (format below) once merged. If the PR body said
   `Fixes #n`, GitHub auto-closes it — still add the closure note as a comment.
8. Next issue. Do not report back between issues.

### Closure note format

```
Fixed in <sha>.

**What was wrong:** one or two sentences, concrete.
**What changed:** the actual change, with file references.
**Evidence:** gate numbers, and the live check if user-visible.
**Left open:** anything deliberately not done, and why. Omit if nothing.
```

If an issue turns out to be already fixed or invalid, close it saying so, with
the evidence. That is a legitimate outcome.

---

## Order

The scale work comes first. It is the owner's stated priority and several of
these are P0 in effect even where labelled otherwise.

### 1. Finish #165 — import at 20,000 items ← **start here**

The metadata half is **done** (#168). The import half is not, and it is the
worst remaining defect: a new user with a large library **cannot onboard at
all**.

`ExistingLibraryImportService.ImportLibraryAsync`
(`src/Deluno.Filesystem/ExistingLibraryImportService.cs:30`) does the entire
import inside one HTTP request:

- Full recursive disk scan, **fully materialised** before any writing starts
- `foreach` over every discovered item, one `await` insert each — 20,000
  sequential round-trips
- Returns only when completely finished, so it is an HTTP request running for
  hours; it will hit a timeout and leave a partial database
- **No resume** — dying at item 15,000 restarts from zero
- **No progress** — only a final counts object
- TV multiplies it: a few thousand shows at 50–100 episodes each is a couple of
  hundred thousand episode rows

What good looks like is written up in #165. Shape: a tracked, resumable
background operation with a position marker; discovery streams into work rather
than materialising first; writes batched into transactions; ambiguous matches
set aside for review rather than halting the run; honest progress and estimates.

One thing the current design gets **right** and must be preserved: import does
**not** call the metadata provider. It writes what it can parse from filenames
and lets the (now fixed) backfill fill in the rest. That keeps import disk/DB
bound instead of provider bound.

### 2. #164 — the remaining unbounded queries

Three of the findings are fixed (#166, #167, #168). Still open:

- **`IntakeSyncService.cs:94,101,297,305`** loads the **entire catalogue every
  5 minutes** to build an in-memory dedupe dictionary. Should be a SQL-side
  existence check, or a bulk `WHERE ... IN` over just the incoming batch.
- **`EpisodeImportRecoveryService.cs:17,48`** loads all series.
- **`MoviesEndpointRouteBuilderExtensions.cs:1090`** and
  `SeriesEndpointRouteBuilderExtensions.cs:715` still do
  `(await ListAsync()).Where().OrderBy().Take(take)` and then queue up to 1,000
  individual jobs one at a time inside the request — and silently cover only 5%
  of a 20,000-item library. `ListStaleMetadataCandidatesAsync` already exists
  and is the right replacement.
- **`DelunoPaging.Paginate<T>`** (`src/Deluno.Contracts/DelunoPaging.cs`) takes
  an already-materialised `IReadOnlyList<T>`, so adopting it pages the HTTP
  response while still reading the whole catalogue per page. It is also offset-
  based. **Retire it rather than extend it.**

### 3. The list/UI cluster — #134, #132, #131, plus virtualisation

**Do these as one coupled piece of work, not four.** They all reshape the same
endpoint and the same list UI; done separately the contract changes three or
four times.

- **#134** — pagination protocol. Recommend **keyset (seek)**, not offset:
  stays fast at any depth and is stable when rows are inserted mid-scroll.
  `ORDER BY created_utc DESC, title ASC` already has a supporting index
  (`ix_movie_entries_created_title`, `ix_series_entries_created_title`).
- **#132 / #131** — stop polling; hydrate once and patch via SignalR deltas.
  This is what Radarr/Sonarr actually do. The hard part already exists: #130
  shipped a realtime envelope with sequence numbers, resume and resync, and
  #133 restored transport negotiation.
- **Virtualised rendering** — `apps/web` has **no virtualisation library at
  all**, so a large library builds a DOM node per row. Needs an issue.
- **Slim the rest of the payload** — `overview` is still 22.8% of the list
  response. Removing it needs the detail drawer to fetch on open; that belongs
  here, with pagination.

### 4. #163 — outbound throttling

Deluno only slows down **after** an indexer rate-limits it, which is the event
that gets accounts flagged or banned. There is a reactive circuit breaker
(`SqliteConnectionsRepository.cs:660`) but **nothing proactive**: no minimum
interval per indexer, no per-host budget. `FeedMediaSearchPlanner.cs:29` fans
out to every configured indexer 16-wide. Metadata providers have no protection
at all. #144's signalling made bursts sharper. Decisions needed are listed in
the issue — do not guess the intervals; check what Prowlarr/Jackett use.

### 5. Everything else by label

`frontend` cluster together, `api` together. #78 and #106 epics close last.

---

## Ideas the owner asked for that are not yet issues

Raised in discussion, agreed as direction, **not yet filed**. File them before
or as you do them.

- **Prioritise work instead of doing all of it.** A 1987 film with a file at
  cutoff quality needs checking essentially never; something released last week
  and missing needs checking often. The *arr apps treat both alike, which is why
  "refresh everything" takes hours. Spend effort where it matters and 20,000
  items becomes ~50 that matter today. **This is the single biggest scale idea.**
- **Ask the provider what changed.** TMDB publishes a daily changes feed. One
  request replaces 20,000 "did this change?" lookups, and upkeep costs the same
  at 500 items or 500,000.
- **Make the ongoing work visible.** Users press "Update All" because they
  cannot see the app already handling it. Show what is being worked on, what is
  queued, when the next sweep is. Then the sledgehammer is unnecessary.
- **Bulk actions as one resumable operation** with a position marker, not
  thousands of separate jobs.
- **Deleting a big library, or a series with hundreds of episodes** — must not
  be one enormous locking transaction. Not yet audited.
- **Long operations need progress, pause, cancel and resume** as a general rule.

---

## Traps — all of these cost real time already

- **Stop `Deluno.Host` before any build.** Otherwise the DLL copy fails on a
  file lock and the error is misleading.
- **Playwright kills the dev backend.** After `npm run test:web`, restart with
  `powershell -File scripts\start-local-app.ps1`. A run that dies mid-way can
  leave port 5199 held.
- **`npm run test:web` is run from the repo root**, not `apps/web` (the script
  lives in the root `package.json`). It is occasionally flaky — one failure that
  passes in isolation and on a clean re-run is not a regression, but re-run the
  whole suite to confirm rather than assuming.
- **Python stdout must be forced to UTF-8** when scripting C# edits on Windows
  (`sys.stdout.reconfigure(encoding='utf-8')`), and read/write with
  `newline=''` to preserve CRLF. Em-dashes silently became cp1252 bytes once.
- **`"Deluno.Platform.Secrets"`** in the secret protectors is a cryptographic
  purpose label, not a namespace. Renaming it makes every stored secret
  undecryptable.
- **Migrations:** Platform **18**, Jobs **11**, Movies **9**, Series **9**.
  `MigrationRunnerTests` asserts all of them by count *and* by name.
- **`WebApplication` inserts routing implicitly at the very start of the
  pipeline** unless `app.UseRouting()` is called explicitly. `Program.cs` now
  calls it explicitly — do not remove it, or the `/api/v1` alias silently stops
  affecting dispatch while still rewriting the path.
- **Minimal-API endpoints need `[FromServices]`** on injected repositories.
- **The Dockerfile is gone**, along with the Docker release job. Do not
  reintroduce either. `compose.yaml` still references it; that is known.
- **Untracked `.tmp-*` files** at the repo root predate this work. Leave them.
- **Background automation is ON** (`settings.AutoStartJobs`). The worker skips
  every tick when false — deliberate, do not "fix" it.
- **Dev DB has real fixtures** — Breaking Bad (71 eps), The Simpsons (885 eps),
  Blade Runner, Top Gun ×2, Top Gun: Maverick, two UX fixture libraries. Do not
  reset it. To test at scale, **copy** the database and seed the copy.

---

## Technique

**Prove query behaviour, don't assume it.** `EXPLAIN QUERY PLAN` against a copy
of the dev database settles "is this index used" in seconds.
`USE TEMP B-TREE FOR ORDER BY` means it is still sorting everything.

**Seeding a large database is cheap** — 20,000 rows inserted in one transaction
took under a second. Copy `.deluno/data/movies.db` to a scratch path, seed the
copy, measure there. A reusable fixture generator should be committed as part
of the #165 import work; it was a scratch tool in this session.

**The big files** (`SqlitePlatformSettingsRepository`,
`PlatformEndpointRouteBuilderExtensions`) are still thousands of lines. Do not
hand-count line ranges and do not regex over C#. Write a small member-splitter:
scan for declarations at 4-space indent, track brace depth, skip raw string
literals (`"""`), support list / cut / drop by member name.

## Running it

```powershell
powershell -File scripts\start-local-app.ps1    # API 5099, Vite 5173, admin/admin1234
```

Starting `Deluno.Host.exe` by hand needs `.env.local` loaded into the process
**and** `$env:Storage__DataRoot='C:\Projects\Deluno\.deluno\data'`, or it
silently builds an empty database.

---

## Standards

Verify live, do not just build. For web work: a Playwright script in
`apps/web/scripts/.tmp-*.mjs` (it must live there to resolve `@playwright/test`;
write it with the Write tool — Bash heredocs mangle backslashes), drive a real
round trip, assert against the API, check the console, delete the script.

Say plainly when something is half-done. Do not call unbuilt work a blocker.
When a change is reverted or superseded, say so rather than quietly dropping it.
