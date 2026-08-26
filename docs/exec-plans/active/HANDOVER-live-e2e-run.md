# Deluno — handover

You're picking up Deluno (`C:\Projects\Deluno`, github.com/jampat000/Deluno): a Windows .NET 10 + React 19 media-automation app replacing Radarr/Sonarr/Prowlarr/Huntarr/Cleanuparr/Recyclarr/Upgradarr/Trash Guides. Issue [#194](https://github.com/jampat000/Deluno/issues/194) is the product bar: do everything the arr-suite does, better and **simpler**.

`main` is at `a82cf29`, working tree clean, 751 .NET tests and 272 web tests pass.

## Standing rules from James — do not deviate

1. Work directly on `main` for **Deluno**. No feature branches. Commit and push.
2. **MediaMop is different**: `main` is protected, use a branch + PR. Merging needs `--squash --admin`.
3. Deluno: never run GitHub Actions. **MediaMop: Actions are expected** — that is how it releases.
4. Verify live in **real Chrome** via `mcp__claude-in-chrome__*`. The in-app browser pane is not a substitute.
5. Australian English in all user-facing copy.
6. Preserve the 20,000+ item scale invariant.
7. **Stop `Deluno.Host` before any build** — it locks the DLLs.
8. Add tests for contract, persistence, routing, status or schema changes.
9. He wants **short answers** and `AskUserQuestion` with brief options. Simplicity is the product. Repetition is a defect.
10. **Do not cut corners on setup.** Configure through the UI, not the API. This is not a style note — it is how three of this run's bugs were found.
11. **Close issues before starting new work.** He pulled me up on drifting into a broad sweep while issues were still open, and he was right.

## Where the board stands

Ten issues closed this run. Two are open and actionable:

- **[#296](https://github.com/jampat000/Deluno/issues/296) — "How this works", waiting on James's read of the copy.** The pattern is built (`apps/web/src/components/app/how-this-works.tsx`) and all seven configuration areas have one: Media Management, Quality & Release, Find & Download, Automation & Recovery, Discover Media, and — lead only, no steps — Preferences and System. James's steer was *configuration items only*, so Dashboard, Movies, TV Shows, Schedule, Transfers and Activity have none; an Activity draft was written and removed because its three steps were the three tabs directly above them. Two changes were made beyond the original ask and he has not reacted to them yet: the panel **collapses, with one preference across all seven**, and it is **no longer info-tinted**, because hue belongs to state (#290) and an explainer is not a state. What remains is his judgement on the copy, which is the whole job. See the status comment on the issue.
- **[#300](https://github.com/jampat000/Deluno/issues/300) — `waiting` means three different things.** `MovieWorkflowService` sets it when a film *has a file and is at or above target*; the frontend hint says it means *not searchable yet*, implying no file. So the tooltip a user reads is probably wrong. Split out of #299 after fixing the visible symptom; the ambiguity itself is untouched and was not chased down.

Also open: **[#194](https://github.com/jampat000/Deluno/issues/194)** the epic, and **#78 / #81 / #82 / #129** — GA readiness and externally blocked.

## What this run did

Fixed and closed the six issues that were open at the start, then ran the first pass of the new end-to-end plan and found five more bugs — none of which any test could see, because each was a place where two things had to agree and nothing compared them.

**[#297] Every grab was sent with a hard-coded category.** `MediaGrabHandler` passed the literal `"movies"` or `"tv"`, so `ResolveCategory`'s fallback could never run and the Movies/TV category fields on every download client — plus the per-library routing category — had never been read by anything. Downloads landed in the client's default folder rather than the one the processor watches. **This is a large part of why the Processing stage was so hard to prove out.**

**[#298] A refined import was filed under its release name and never linked to the film.** It landed as `Big.Buck.Bunny.…-DELUNO (Unknown Year)`, the film stayed Missing, and monitoring would grab it again — a re-download loop that also produced duplicate catalogue entries. This is **#268 on the sibling import path**: the direct path was taught to name from the catalogue and carries a comment saying why; the processor path never was.

**[#294] The folder check had never lit Readable or Writable, for any path.** Server sent `canRead`/`canWriteToParent`/`fullPath`; the UI read `readable`/`writable`/`normalizedPath`/`message`. Both compiled. A healthy folder showed a warning triangle with nothing written under it.

**[#295] A legacy-protocol client told you to change it, then disabled the control that changes it** — and displayed a protocol it did not have, because a `<select>` with an unmatched value shows its first option.

**[#280] is closed.** The whole pipeline ran twice: grab → qBittorrent → hand-off → MediaMop → matched output → import → `Big Buck Bunny (2008)/Big Buck Bunny (2008).mkv`, film reporting **On disk · WEB 2160p · cutoff Met**, and `processingCount`/`waitingForProcessorCount` back to **0** while the torrent kept seeding. The clamp question: unreachable, and gone — the invariant is pinned at the source in the one summary rule the adapters and the telemetry service now share. The timeout question: yes, it surfaces, as "Wait up to" on Processing Workflow.

**[#299] An imported film read DOWNLOADING on its card** while its own detail page said *On disk — imported and verified*. The adapter tested the wanted status before `hasFile`, so a search-scheduling concept overrode the file state on an availability chip. Found while taking the README screenshots.

**[#291]** added a third Playwright project, `shipped`, that drives a real browser against `Deluno.Host` serving its own front end. It runs in `npm run test:web` by default.

## The lesson worth carrying

Three of the four new bugs were **one rule written twice, in two places that drifted**. When you fix something, the next question is where else that shape lives. #268 → #298 is the clearest case: the same defect, next door, for months.

And the reason they were all found in one afternoon is that the app was set up by hand, through the UI, on real software talking to real software. Every one of them was invisible to a green test suite.

## The rig

`Deluno Sim 2025` in Hyper-V, guest `MEDIAMOP-TEST`, **10.1.1.142**, Windows Server 2025.

| What | URL | Sign in |
|---|---|---|
| Deluno | http://10.1.1.142:5099 | `admin` / `Deluno-Lab-2026!` |
| MediaMop | http://10.1.1.142:8788 | `admin` / `MediaMop-Lab-2026!` |
| qBittorrent | http://10.1.1.142:8080 | LAN subnet whitelisted |
| Windows (RDP/WinRM) | 10.1.1.142 | `Administrator` / `Deluno-MM-Lab-2026!` |

All three run as **scheduled tasks at startup** and survive a reboot.

The install is now a **fully configured, populated lab** running the head of `main`: 2 libraries, a torznab indexer, qBittorrent, routing, a TRaSH-template quality profile, 11 films and 6 shows with real metadata, and one film genuinely imported through the processor. The pre-run data root is kept at `C:\Deluno\Data.before-e2e-20260826-201335`, and the previous binary at `C:\Deluno\App.before-e2e-20260826-201404`.

**Deploying a build to the VM:** stop the task, kill `Deluno.Host`, copy `Deluno.Host.exe` and `wwwroot` into `C:\Deluno\App`, start the task. `Storage__DataRoot` and `Server__AllowLan` are machine env vars, so `appsettings.json` does not need editing.

**The torznab indexer runs on the desktop, not the VM**, and is not a service. It now lives in the repo — `scripts/lab/`, with a README — rather than in a session scratchpad that has to be hunted for. Start it before any acquisition test:

```bash
TORZNAB_BIND=0.0.0.0 TORZNAB_ADVERTISE=10.1.1.102 python scripts/lab/torznab_seed.py
```

`scripts/lab/watch-pipeline.ps1` prints where an acquisition has got to in one call — telemetry, queue statuses, hand-offs, jobs and the relevant activity.

## Traps — save yourself the time

- **`scripts/publish-windows.ps1` calls `.\.dotnet\dotnet.exe`, which does not exist here.** Use the PATH SDK. A publish takes ~5 minutes; background it.
- **qBittorrent only applies a category's save path when Automatic Torrent Management is on.** With it off, the category is set correctly and the file still lands in the global default folder. The rig now has `auto_tmm_enabled: true`. Deluno's **Check category** does not catch this, and cannot check the category that will actually be used when the routing override is blank, because it is gated on that field being non-empty. Worth fixing.
- **Background automation is paused on a fresh install** and queued jobs are held — nothing moves past the queue until setup ladder step 4. `/api/health/ready` says so in plain words; read it before assuming the worker is broken.
- **MediaMop's Refiner empties the source folder** when it processes. If that folder is the download client's, seeding breaks.
- **`/api/activity` and friends return `{items: [...]}`.** Reading `$response | Select-Object …` in PowerShell silently gives you nothing.
- **Git Bash mangles backslashes.** Use PowerShell for anything with `\` — including JSON bodies containing Windows paths, which come back as HTTP 400 otherwise.
- **Editing the VM's SQLite from the desktop:** copy `platform.db` **and** its `-wal`/`-shm` down, open it locally (which checkpoints and removes the sidecars), then move the VM's stale sidecars aside before copying the merged file back. Skip that and the stale WAL silently reverts your change.
- **`rg` mangles some route-string matches.** Use `tests/Deluno.Platform.Tests/Routing/endpoint-inventory.snapshot.txt` for the authoritative route list.
- **The in-app browser pane's screenshot times out often** on this app. Retry with a plain `wait` + `screenshot`; it usually succeeds second time. `find` by ref is more reliable than coordinate clicking, because drawer layouts shift as validation messages appear.

## Loose ends worth a look

- **The two import paths still repeat each other.** #298 made them agree; sharing the code outright is the better end state.
- **Deluno's Check category cannot check the category that will actually be used** when the routing override is blank, because it is gated on that field being non-empty — and it does not notice that qBittorrent ignores a category's save path when Automatic Torrent Management is off. Both would have saved time this run.
- **Phases 9–12 of `E2E-full-product-test.md` have not been run**: missing and upgrade cycles, recovery and cleanup, lists, notifications, tags, destination rules, API keys, backup and restore, reboot. SABnzbd is not installed on the rig, so Phase 5's usenet rungs are untouched.
- **A card can no longer ever say Downloading.** That is honest — the catalogue adapter has no live transfer state — but if progress on a library card is wanted, it needs the download telemetry wired in rather than a wanted status standing in for it.

## Where James's bar sits

- **Repetition is a defect.** The same subject in two cards counts — and so does the same rule in two code paths.
- **Simplicity is the product.** When he asks "is this the best most user friendly way?", the answer is usually to remove the setting and make Deluno decide, explaining the consequence once in plain words.
- **Write for the person reading it.** He rejected a settings card for saying `"http://… answered"` and offering a bare URL path. Say what to do, name what to check, give people something they can copy.
- He asks "is this your 100%?" and expects a real answer.
