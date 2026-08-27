# Deluno — handover

You're picking up Deluno (`C:\Projects\Deluno`, github.com/jampat000/Deluno): a Windows .NET 10 + React 19 media-automation app replacing Radarr/Sonarr/Prowlarr/Huntarr/Cleanuparr/Recyclarr/Upgradarr/Trash Guides. Issue [#194](https://github.com/jampat000/Deluno/issues/194) is the product bar: do everything the arr-suite does, better and **simpler**.

`main` is at the head of this run, working tree clean, **811 .NET tests**,
**94 web unit tests** and Playwright at **272 passed / 10 skipped**. The rig at 10.1.1.142 is running this build.

The web count went *down* by eight on purpose: two test files went with the two
dead modules they were testing. `media-status-presentation.test.ts` asserted the
shape of an eleven-value colour table a title could only ever hold two values of,
and `library-filters.test.ts` exercised a client-side filter engine nothing had
imported since the catalogue became server-paged.

## Where the board stands

**Closed this run: [#302](https://github.com/jampat000/Deluno/issues/302), for
the second time and properly, and [#303](https://github.com/jampat000/Deluno/issues/303).**
DESIGN-001 is built through step 4 of its six-step order — all of step 4 this
time, not the grid half of it.

Open and actionable:

- **DESIGN-001 step 5 — live transfer state.** *Downloading* is a mark and a
  colour and has no source: `titleMark()` takes an `isTransferring` flag nothing
  sets, and there is deliberately no Downloading chip in the filter row because a
  chip that can never match is worse than none. It has to come from download
  telemetry and must never be inferred from a wanted status — that is the bug
  #299 fixed.
- **[#301](https://github.com/jampat000/Deluno/issues/301)** — Subber. It
  inherits the settled vocabulary, and the bar under a movie's poster already has
  its landing site: `SubtitleLanguagesWanted`/`Held` are on both catalogue
  contracts, zero, with a grey bar drawn for "asked for nothing".

Also open: **[#194](https://github.com/jampat000/Deluno/issues/194)** the epic,
and **#78 / #81 / #82 / #129** — GA readiness and externally blocked.

## What this run did

**Re-closed #302.** James re-opened it saying the design had not been executed
100%, and he was right twice over: step 4 had finished the grid and stopped, and
the guard test written to prevent exactly this had not noticed, because it
watched for **one spelling** of the defect — `tone="x"` beside a state's label.
Four more tables were spelling it other ways, and a fifth was dead code holding
a competing definition. The full account is in DESIGN-001's step 4. The short
version:

- `MEDIA_STATUS_PRESENTATION` coloured a missing title **amber** — the signal
  reserved for "a person is needed" — off an eleven-value `MediaStatus` a title
  could only ever hold two values of, both from `hasFile`.
- `WANTED_STATUS_PRESENTATION` gave the four stored wanted statuses a *second*
  set of tones: Missing blue there, red on the poster.
- `quickFilterConfig` wrote the mark colours out by hand, three lines under a
  comment calling that row the legend.
- Both detail-page headers picked a Badge `variant` per status.
- `filterAndSortLibraryItems` and its 45-value `FilterField` union: imported by
  nothing, and holding a fourth definition of Upgradable and a `downloading`
  branch that could only ever match zero rows.

`MediaItem` has no `status` at all now. The guard is restated in the shape the
offenders took: outside `status-tones.ts`, no module may put a colour on the same
line as a mark's name, whether it spells the colour `tone`, `variant` or `bg-*`.
Reintroducing the old detail-page badge was checked to fail it before the guard
was trusted.

**The dashboard had survived both passes**, because a screen that invents a new
*name* for a state carries no mark label for a colour rule to catch. It counted
"Watching for", "Still missing" — amber — and "Could be upgraded", beside a ring
labelled "On disk", "Still missing" and **"Upgradeable"**, a letter off the mark
it was drawing. Both read the table now and the strip is the design's own counts
line. **Needs You** lost its two self-resolving entries: it read **2** on an
install whose sidebar was simultaneously saying *All good · Nothing needs you*.
A second guard was added for this shape — the names DESIGN-001 retired may not
come back.

**One left flagged, not fixed:** the sidebar's "needs you" and the dashboard's
count it from **two different sources**, which is why they contradicted each
other. Same defect family, different subsystem — `attentionTotal()` in
`lib/use-attention.ts` versus `setupStatus.attentionItems`. It wants one
attention model the way `status-tones.ts` is now one colour table.

**Three repetitions James would have found on screen**, all removed while
verifying live: the list row said monitoring three times over; the movie's summary
strip restated the mark as **FILE: Missing** in amber next to **CUTOFF: Below
target** in amber, on a movie with no file to be below target with; and every
episode row carried a File column saying "Not imported" beside a Status column
saying "Missing". The strip is three cells now — quality, monitoring, import
issues — and none of them is the mark.

**Closed #303.** Three orphaned pieces that each worked and never met. The wiring
went **inside the library search cycle**, not in the heartbeat lane where the
missing call was expected: the lane would have needed its own copy of the
time-of-day window, the interval, missing-versus-upgrade, the manual override and
`MaxItemsPerRun`, and a second copy of a scheduling rule is how the last four
defects in this codebase were built.

**Verified live on the rig**, not just green: shelf, list, both detail pages and
the episode list all draw the same mark from the same table, and no amber appears
on a title anywhere. **The calendar could not be exercised with data** — every
film on the rig has no release date stored and every episode aired outside the
page's ±45/+120-day window, so its endpoint was confirmed to run clean and its
contract change is unit-covered, but nobody has seen the new chip.

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
`upcoming` is new and actually set, from release dates for a movie and the
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

**[#298] A refined import was filed under its release name and never linked to the movie.** It landed as `Big.Buck.Bunny.…-DELUNO (Unknown Year)`, the movie stayed Missing, and monitoring would grab it again — a re-download loop that also produced duplicate catalogue entries. This is **#268 on the sibling import path**: the direct path was taught to name from the catalogue and carries a comment saying why; the processor path never was.

**[#294] The folder check had never lit Readable or Writable, for any path.** Server sent `canRead`/`canWriteToParent`/`fullPath`; the UI read `readable`/`writable`/`normalizedPath`/`message`. Both compiled. A healthy folder showed a warning triangle with nothing written under it.

**[#295] A legacy-protocol client told you to change it, then disabled the control that changes it** — and displayed a protocol it did not have, because a `<select>` with an unmatched value shows its first option.

**[#280] is closed.** The whole pipeline ran twice: grab → qBittorrent → hand-off → MediaMop → matched output → import → `Big Buck Bunny (2008)/Big Buck Bunny (2008).mkv`, movie reporting **On disk · WEB 2160p · cutoff Met**, and `processingCount`/`waitingForProcessorCount` back to **0** while the torrent kept seeding. The clamp question: unreachable, and gone — the invariant is pinned at the source in the one summary rule the adapters and the telemetry service now share. The timeout question: yes, it surfaces, as "Wait up to" on Processing Workflow.

**[#299] An imported movie read DOWNLOADING on its card** while its own detail page said *On disk — imported and verified*. The adapter tested the wanted status before `hasFile`, so a search-scheduling concept overrode the file state on an availability chip. Found while taking the README screenshots.

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

The install is now a **fully configured, populated lab** running the head of `main`: 2 libraries, a torznab indexer, qBittorrent, routing, a TRaSH-template quality profile, 11 movies and 6 shows with real metadata, and one movie genuinely imported through the processor. The pre-run data root is kept at `C:\Deluno\Data.before-e2e-20260826-201335`, and the previous binary at `C:\Deluno\App.before-e2e-20260826-201404`.

**Deploying a build to the VM:** stop the task, kill `Deluno.Host`, copy `Deluno.Host.exe` and `wwwroot` into `C:\Deluno\App`, start the task. `Storage__DataRoot` and `Server__AllowLan` are machine env vars, so `appsettings.json` does not need editing.

**The torznab indexer runs on the desktop, not the VM**, and is not a service. It now lives in the repo — `scripts/lab/`, with a README — rather than in a session scratchpad that has to be hunted for. Start it before any acquisition test:

```bash
TORZNAB_BIND=0.0.0.0 TORZNAB_ADVERTISE=10.1.1.102 python scripts/lab/torznab_seed.py
```

`scripts/lab/watch-pipeline.ps1` prints where an acquisition has got to in one call — telemetry, queue statuses, hand-offs, jobs and the relevant activity.

## Traps — save yourself the time

- ~~**`scripts/publish-windows.ps1` calls `.\.dotnet\dotnet.exe`, which does not exist here.**~~ Fixed — it falls back to the PATH SDK. A publish still takes ~5 minutes; background it.
- ~~**The web assets are not content-hashed.**~~ Fixed — they are `deluno.<hash>.js` now, so a stale bundle no longer survives a deploy. If something still looks unfixed, check the served file before re-diagnosing anyway.
- **The Deluno calendar cannot be seen on this rig.** Every movie has no `in_cinemas`/`digital`/`physical` date stored, and every episode aired years before the page's ±45/+120-day window. `/api/movies/calendar` returns `[]` over any range. The metadata editor has no date fields either, so a calendar change cannot be verified by looking — only by unit test.
- **`Deluno.Host` binds 5099 on the rig and 5199 under Playwright.** The Schedule page lives at `/calendar`, not `/schedule`.
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
