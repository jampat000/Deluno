# Deluno — handover

You're picking up Deluno (`C:\Projects\Deluno`, github.com/jampat000/Deluno): a Windows .NET 10 + React 19 media-automation app replacing Radarr/Sonarr/Prowlarr/Huntarr/Cleanuparr/Recyclarr/Upgradarr/Trash Guides. Issue [#194](https://github.com/jampat000/Deluno/issues/194) is the product bar: do everything the arr-suite does, better and **simpler**.

`main` is at `23b1708`, working tree clean, **810 .NET tests**, **102 web unit
tests** and Playwright at **272 passed / 10 skipped**. The rig at 10.1.1.142 is
running this build.

## Where the board stands

**Closed this run: [#300](https://github.com/jampat000/Deluno/issues/300) and
[#302](https://github.com/jampat000/Deluno/issues/302).** DESIGN-001 is built
through step 4 of its six-step order.

Open and actionable:

- **[#303](https://github.com/jampat000/Deluno/issues/303)** — automatic
  per-episode search. Three orphaned pieces, all still orphaned: nothing calls
  `PlanEpisodeSearchesAsync`. #300 fixed the half of it that was a defect —
  `ListEligibleWantedEpisodesAsync` filtered on `wanted`, a word nothing writes,
  so the query matched nothing in production and its test seeded the value by
  hand. It reads `missing` now. **What is left is the wiring**: the heartbeat's
  automation lane calls `PlanLibrarySearchesAsync` and nothing calls the episode
  equivalent. Mirror it there.
- **[#301](https://github.com/jampat000/Deluno/issues/301)** — Subber. It
  inherits the settled vocabulary, and the bar under a film's poster already has
  its landing site: `SubtitleLanguagesWanted`/`Held` are on both catalogue
  contracts, zero, with a grey bar drawn for "asked for nothing".
- **DESIGN-001 step 5 — live transfer state.** *Downloading* is a mark and a
  colour and has no source: `titleMark()` takes an `isTransferring` flag nothing
  sets, and there is deliberately no Downloading chip in the filter row because a
  chip that can never match is worse than none. It has to come from download
  telemetry and must never be inferred from a wanted status — that is the bug
  #299 fixed.

Also open: **[#194](https://github.com/jampat000/Deluno/issues/194)** the epic,
and **#78 / #81 / #82 / #129** — GA readiness and externally blocked.

## What the last run did

**Closed #300.** `waiting` meant three different things: set by the workflow on a
title that *has* a file and meets its target, set by the migration importer when
the source app reported a file, and described by the front end as "not searchable
yet — it has not been released", which is the opposite state. Four words now,
one meaning each — `missing`, `upgrade`, `covered`, `upcoming` — in
`WantedStatuses`, which replaced **three private copies** of the same switch.
Each of those mapped anything unrecognised to `missing`, the most dangerous
direction to guess in because `missing` means "go and download this". The shared
one throws instead, and immediately caught two test suites seeding `wanted`.
`upcoming` is new and actually set, from release dates for a film and the
earliest air date for a show, through `MovieAvailability` — the rule that already
gates searching — rather than a second copy of it in SQL. V0014 and V0015 migrate
the rows.

**Closed #302.** `lib/status-tones.ts` is the one place a state gets a colour.
The amber fix is the one that matters: it belonged to four states that were
proceeding normally, and every one of those teaches people to stop reading the
colour that has to stay trustworthy. It covers four states now and the test
asserts that list *exactly*. **Four** tone vocabularies became one, not the two
the issue named — `AttentionDot` and `LibraryImpact` had their own as well.

**Built the mark** (DESIGN-001 step 4): one dot on the four-rung ladder, a half
for "not monitored", and a bar for what you asked for beyond the title. The
filter row is now the legend, the counts and the filters at once — it used to
repeat four numbers that a summary band above it was already showing.

**Four things flagged in the run before, all fixed.** The web assets are
content-hashed (a stale bundle survived a deploy and looked exactly like a fix
that had not worked — it cost a wrong diagnosis); a show stopped counting
episodes that have not aired as missing; the publish script finds a `dotnet` that
exists; and the detail pages stopped searching a 25-item summary for the one
title they were already showing.

## And the run before those

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

- ~~**`scripts/publish-windows.ps1` calls `.\.dotnet\dotnet.exe`, which does not exist here.**~~ Fixed — it falls back to the PATH SDK. A publish still takes ~5 minutes; background it.
- **The web assets are not content-hashed** (`assets/deluno.js`, not `deluno.<hash>.js`). After deploying `wwwroot`, a browser will happily keep serving you the old bundle — a hard reload is not optional, and a "the fix did not work" result is worth re-checking against the served file before re-diagnosing. It cost a wrong diagnosis this run.
- **Only `wwwroot` needs redeploying for a front-end change.** `npm run build:web`, copy `apps/web/dist` over `C:\Deluno\App\wwwroot`, hard reload. No publish, no service restart.
- **A crashed Playwright run can leave `Deluno.Host` holding port 5199**, and the next run then fails with "already used" or times out at the login form. Kill it before re-running.
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
