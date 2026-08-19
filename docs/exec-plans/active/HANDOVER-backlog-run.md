# Handover — autonomous backlog run

**Repo:** `C:\Projects\Deluno` (private). `AGENTS.md` says
`C:\Users\User\Projects\Deluno` — that is wrong.
**Baseline:** `main` @ `d03e682`. CI green, Release skipped (tag-gated), working
tree clean, in sync with origin.
**Scope:** close all 42 open GitHub issues with real remediation and closure
notes. Two are epics (#78, #106) — they close when their children do.

---

## The goal

Every issue either **closed with a remediation commit and a closure note**, or
**answered in this chat** because it needs a decision only the owner can make.
Nothing left silently open. No narrating progress between issues — work through
them.

---

## Decisions already made — do not re-ask

| Question | Answer |
|---|---|
| How work lands | **PR per issue, auto-merge on green.** Branch protection stays on. Never merge red. |
| Naming (#149) | **Decide from the UI + north star.** The interface is the source of truth. Write the agreed vocabulary into `docs/MEDIA_AUTOMATION_TERMINOLOGY.md` as a normative table, then rename to match. |
| Breaking the public API | **Allowed.** It is early and nothing external depends on it yet. Rename routes, reshape payloads, add pagination envelopes. Update `docs/external-integration-api.md` in the same PR. |
| Genuine blockers | **Ask in chat and wait.** Do not guess on a question that changes user-visible behaviour or data. One clear question, with the options and a recommendation. |

"Genuine blocker" means: two defensible answers, and picking wrong costs real
rework or changes what a user sees. Everything else — pick, do it, say why in
the commit.

---

## The gate — after every commit

```powershell
Get-Process -Name "Deluno.Host" -ErrorAction SilentlyContinue | Stop-Process -Force
dotnet build Deluno.slnx
dotnet test tests/Deluno.Persistence.Tests/Deluno.Persistence.Tests.csproj
dotnet test tests/Deluno.Platform.Tests/Deluno.Platform.Tests.csproj
dotnet test tests/Deluno.Movies.Tests/Deluno.Movies.Tests.csproj
dotnet test tests/Deluno.Integrations.Tests/Deluno.Integrations.Tests.csproj
```

**Must not drop:** Persistence 235, Platform 86, Movies 18, Integrations 52,
Tray 3, Playwright 189. A drop is a regression, not a test that needs updating.

Frontend work also runs `npm run test:web` (189 across chromium + mobile).

---

## Per-issue loop

1. `gh issue view <n>` — read it and its comments.
2. Branch: `fix/<n>-<slug>`.
3. Implement. Small commits; move files and change them in separate commits.
4. Run the gate. Verify live where the change is user-visible — start the app,
   drive a real round trip, check the console.
5. `gh pr create`, then `gh pr merge --auto --squash`.
6. Close with a note (format below) once merged.
7. Next issue. Do not report back between issues.

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

Dependencies first, then value, then cost.

1. **#142 API versioning** — unblocks every breaking rename cleanly.
2. **#115 (P0)** login rate limiting, localhost binding, auth on docs endpoints.
3. **#130 realtime envelope** — sequencing and resume. Blocks #131/#132/#135/#136.
4. **#138** finish ADR-001 Step 1 (Quality, Connections, Libraries) — this is
   half done and blocks #120, #145.
5. **#121** Series/Worker/Realtime tests — hard precondition for #118.
6. **#119, #144** worker handlers and event-driven lanes.
7. **#149** naming standardisation — after #142 so route renames are versioned.
8. Everything else by label: `frontend` cluster together, `api` together.
9. **#78, #106** epics close last.

`docs/exec-plans/active/` holds the designs: `ADR-001` (module boundaries),
`ADR-002` (realtime), `AUDIT-001` (scheduling and contention),
`PLAN-module-split.md` (the runbook, with a progress table).

---

## Traps — all of these cost real time already

- **Stop `Deluno.Host` before any build.** Otherwise the DLL copy fails on a file
  lock and the build error is misleading.
- **Playwright kills the dev backend.** After `npm run test:web`, restart with
  `powershell -File scripts\start-local-app.ps1`. If a run dies mid-way it can
  leave port 5199 held — kill the owning PID or the next run fails with
  "already used".
- **Python stdout must be forced to UTF-8** when scripting C# edits on Windows,
  or em-dashes silently become cp1252 bytes. This corrupted user-facing strings
  once already.
- **`"Deluno.Platform.Secrets"`** in the secret protectors is a cryptographic
  purpose label, not a namespace. Renaming it makes every stored secret
  undecryptable. Leave it, whatever #149 decides.
- **Migrations stay in `Deluno.Platform`.** Splitting the C# does not split the
  SQLite file. `MigrationRunnerTests` asserts the Platform count is **18**.
- **Minimal-API endpoints need `[FromServices]`** on injected repositories, or a
  host that has not registered them infers a body parameter and every endpoint
  test fails.
- **The Dockerfile is gone**, along with the Docker release job. Do not
  reintroduce either.
- **`.github/workflows/*` needs the `workflow` token scope.** It is granted now.
- **Untracked `.tmp-*` files** at the repo root predate this work. Leave them.
- **Background automation is ON** (`settings.AutoStartJobs`). The worker skips
  every tick when false — that is deliberate, do not "fix" it.
- **Dev DB has real fixtures** — Breaking Bad (71 eps), The Simpsons (885 eps),
  Blade Runner, two UX fixture libraries. Any new fixture needs a real title or
  metadata will not resolve.

## Technique that makes the big files tractable

`SqlitePlatformSettingsRepository` and `PlatformEndpointRouteBuilderExtensions`
are still thousands of lines. Do not hand-count line ranges and do not regex over
C#. Write a small member-splitter: scan for declarations at 4-space indent, track
brace depth, skip raw string literals (`"""`), and support list / cut / drop by
member name. That is how Security, Notifications and Intake came out cleanly.

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
