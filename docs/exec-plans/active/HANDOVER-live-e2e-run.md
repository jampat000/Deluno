# Deluno — handover

You're picking up Deluno (`C:\Projects\Deluno`, github.com/jampat000/Deluno): a Windows .NET 10 + React 19 media-automation app replacing Radarr/Sonarr/Prowlarr/Huntarr/Cleanuparr/Recyclarr/Upgradarr/Trash Guides. Issue [#194](https://github.com/jampat000/Deluno/issues/194) is the product bar: do everything the arr-suite does, better and **simpler**.

`main` is at the head of this run, working tree clean, **854 .NET tests**,
**104 web unit tests**, **13 metadata-gateway tests** and Playwright at
**272 passed / 10 skipped**. The rig at 10.1.1.142 is running this build.

The web count went *down* by eight on purpose: two test files went with the two
dead modules they were testing. `media-status-presentation.test.ts` asserted the
shape of an eleven-value colour table a title could only ever hold two values of,
and `library-filters.test.ts` exercised a client-side filter engine nothing had
imported since the catalogue became server-paged.

## What this run did — #301, step 1 finished

**Deluno now knows what subtitles you already have**, before it fetches any.

The plan had this as "read the embedded streams out of the MKV". James corrected
it while it was being built: *"shouldn't the whole premise of porting Subber over
to Deluno be that it knows when subtitles are downloaded and added? I get that
ffprobe can detect subs in downloaded media but that should only be part of the
equation."* Right, and the bigger half was missing — the `.srt` sitting **beside**
the video, which is what a Bazarr-era library is full of. Held now has three
sources: `fetched` (Subber writes the row itself, with its provider), `external`
(a file beside the video, or in a `Subs` folder), `embedded` (ffprobe). The store
is the truth; scanning only teaches it about what Deluno did not fetch.

`en`, `eng` and `English` are one language now. The setting stores the first,
ffprobe emits the second, a sidecar is named any of the three, and two of those
are different ISO 639-2 codes for one language. Three vocabularies would have
made `eng` embedded and `en` wanted read as missing — the defect this codebase
keeps producing.

Not guessed at: a bare `Movie.srt` is `und` and counts for nothing; a **forced**
track is not coverage; **hearing-impaired** is, and is not counted twice beside a
plain track.

`movie_subtitle_state` / `episode_subtitle_state` are one SQL body through
`MediaTableMap`, per ADR-001 — the pair costs its Step 2 two table names, not two
implementations. `library.subtitles.scan` is planned by the library cycle and
rides the **import** lane, sliced like `library.import.existing`. A shelf with no
languages is never planned a scan and never runs the rollup: measured at 20,000
films, the rollup is **0.26 ms per hundred-title page** and the read that decides
whether to run it **0.014 ms** (`SubtitleScaleBenchmark`).

**Found while verifying: a manual search request that was never consumed.** Both
libraries on the rig had been stuck at `searchRequested = true` for days,
re-entering the cycle every thirty seconds. The code meant to consume it and
cleared a *local* flag, which skipped the only branch that writes
`search_requested = 0`. Invisible while the cycle had nothing to do there —
immediately expensive once #301 gave it something, queueing a scan every tick.
Fixed, with a test that fails without the fix.

**Verified live, both sources.** First with two sidecars beside Big Buck Bunny
(English, and a forced Spanish): the card drew **half green, half red** — one of
two held, the forced one correctly ignored. Then `ffprobe.exe` was put on the rig
(it had never had one) and English and Spanish were muxed into the file as real
tracks with the English sidecar deleted, so English could only come from inside
the container. The card went to **full green, 2 of 2**, and the forced Spanish
sidecar is still on disk and still not counted.

That second pass needed one more fix: **a file read without ffprobe is read
again once ffprobe is there.** Only the subtitles beside it could be seen the
first time, and nothing would ever have gone back for the tracks inside it.
`unavailable` and `failed` are treated differently on purpose — a missing binary
is an environment state that changes, a file ffprobe cannot parse is a fact about
the file, and retrying the second every cycle would read a corrupt file forever.

### What step 1 does *not* do

- **A `.srt` dropped in by hand beside a file Deluno has already read is not
  noticed.** The scan marker watches the video, not the folder. The ffprobe case
  self-heals now; this one wants an explicit "read these files again" action, the
  way Search now works.
- **A newly imported file is read on the next library cycle, not at import.**
  Deliberate: wiring it into both import paths is the #268 → #298 shape. When
  Subber fetches, it writes the row directly, so this only affects files that
  arrive with subtitles already in them.
- **The detail pages do not list the languages a title holds.** That belongs with
  manual search, step 3.
- **The TV rollup has no episode files on the rig to exercise.** Same SQL body
  through the map as movies, covered at episode grain by
  `SubtitleBarPersistenceTests`, but nobody has seen a show's bar with real data.

## And then the library screen — James's five, plus #194

**[#194](https://github.com/jampat000/Deluno/issues/194) is closed**, and the
only reason it could be is that it no longer depends on anybody remembering it.
`docs/PRODUCT_NORTH_STAR.md` now carries the eight apps Deluno replaces, what it
owes for each, and a **five-question standing check** every change answers before
it is called done. `AGENTS.md` points at it first, so it is read at the start of
a session rather than discovered. The doc existed already and was in nobody's
source-of-truth list.

**Quality is granular again.** `shortQuality()` collapsed twenty-one tiers into
three answers — anything with 2160 became "4K" — so a 60 GB Remux and a 7 GB WEB
read identically everywhere. The stored value was already the answer; nothing
re-derives it now. There were **two copies** of that function, and the dashboard
never imported the grid's.

Three columns also stopped describing files that do not exist: the quality badge
read `currentQuality ?? targetQuality`, so a missing movie wore its *target*
beside a red dot. Size printed "Unknown" where the truthful answer is that there
is nothing to measure, and a filled star sat beside the word "Unknown".

**The topbar is one height and one icon size.** It was three tokens side by side
— 42px, 38px, and a third — with the mobile search icon a pixel larger than the
bell. Search is wider and keeps its label down to `lg`.

**The library toolbar is two rows.** Nine controls became four plus three
actions: row one searches and acts, row two narrows and arranges. Display and
Order were one question asked twice and are one **View** panel; Monitoring and
Views each took a whole control for one setting and are inside **Filters** now.

**There is a filter system.** Quality, genre, size, year, runtime and rating,
all applied in SQL, plus Runtime and Popularity orders. Named typed fields, not
a rule engine — this codebase already shipped the generic version and deleted it
in #302 because it could express filters nothing could answer. The full design,
including the two sorts deliberately *not* built and why, is in
**DESIGN-003-library-filters.md**.

### Three things that fell out of building it

- **"Hunt 10 missing" hunted ten while the button said five.** The action built
  its own query and ignored the search box and the monitoring filter. Both come
  from `buildCatalogueParams()` now.
- **The metadata broker has never sent runtime, popularity or vote count.**
  V0012 added those columns as "the facts the library list has always displayed
  but never had"; the repository writes them and the API accepts them, and
  `mapTmdbResult` in `services/metadata-gateway` dropped all three. So on every
  broker install — which is the managed default — they are null for every title
  ever added. Fixed with tests. **The gateway is a deployed Cloudflare worker, so
  the rig keeps showing blanks until somebody redeploys it**, and until then the
  new Runtime and Popularity orders have nothing to sort by there.
- **Saved views did not save the filters.** `rulesJson` was waiting for "a
  server-side rule contract" and now has one.

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
- **[#301](https://github.com/jampat000/Deluno/issues/301)** — Subber. **Step 1
  is done** (languages, and reading what you already have). **Step 2 is next:
  providers as Connections** — one end to end, Gestdown or Podnapisi since
  neither needs an account, with health and a test button. Then step 3, search
  and write, on the existing lane and planned from the existing cycle.

Also open: **#78 / #81 / #82 / #129** — GA readiness and externally blocked.
[#194](https://github.com/jampat000/Deluno/issues/194) is closed; its bar lives
in `docs/PRODUCT_NORTH_STAR.md` and is the thing to read first.

**Next, in order:**

1. **Redeploy the metadata gateway** so runtime, popularity and vote count start
   arriving. Until then three columns, one filter and two orders are correct
   code over empty data.
2. **#301 step 2 — providers as Connections.** One end to end, Gestdown or
   Podnapisi since neither needs an account, with health and a test button.
3. **DESIGN-003's leftovers** — sorting by size or quality (three options
   costed there), and the compact list's fixed columns.
4. **DESIGN-001 step 5** — live transfer state, so Downloading has a source.
5. The two attention models that disagree — `attentionTotal()` versus
   `setupStatus.attentionItems`.

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
- ~~**The rig has no `ffprobe`.**~~ Fixed — `C:\Deluno\Appfprobe.exe` is
  there now, from the same BtbN build `.github/workflows/release.yml` pins, and
  `C:\Deluno\Toolsfmpeg.exe` beside it for building fixtures. Until this
  run the rig had **never** validated an import stream, run the replacement
  guard, or read an embedded track: the release artifact bundles ffprobe, but the
  rig is deployed by hand-copying `Deluno.Host.exe`, which skips it. Keep copying
  it when you rebuild the VM.
- **Big Buck Bunny on the rig is a #301 fixture now.** Its `.mkv` carries real
  embedded `eng` and `spa` subtitle tracks, muxed in, and a
  `.es.forced.srt` sits beside it. The forced one is there on purpose: it must
  never count. Expect the card to read 2 of 2, full green.
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
