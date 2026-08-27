# Deluno — handover

You're picking up Deluno (`C:\Projects\Deluno`, github.com/jampat000/Deluno): a
Windows .NET 10 + React 19 media-automation app replacing Radarr, Sonarr,
Prowlarr, Huntarr, Cleanuparr, Recyclarr, Upgradarr, Trash Guides and Bazarr.

**Read `docs/PRODUCT_NORTH_STAR.md` first.** It records what each of those
platforms actually does — read from their own sources, not from memory — and the
five-question standing check every change answers before it is called done.
Issue #194 used to be the reminder to do this; it is closed, because the check
outlives it.

Then `docs/exec-plans/active/HANDOVER-live-e2e-run.md` for the lab rig and the
traps, and `DESIGN-001` through `DESIGN-005`.

`main` is at `1b2eb3f`, working tree clean. **862 .NET tests, 117 web unit
tests, 17 metadata-gateway tests**, Playwright **272 passed / 10 skipped**.

## The bar, in James's words

Short answers, few questions, pictures over prose. Simplicity is the product.
Repetition is a defect — he will spot it on screen before any test does.
Measure, don't assert.

> *"instead of being ahead we will still be behind"*

**A new axis does not excuse a smaller number on an old one.** Where a tool
offers N of something, Deluno offers all N and then more.

He corrects bluntly and is usually right. When he corrects a premise, change the
work rather than defend the reasoning. Three times last session:

- *"we need to stop using not asked for… if it doesnt have it its missing plain
  and simple"* — right, and the column had been lying about every missing title.
- *"its like there are 2 cards overlapping and its shaking but then it stops"* —
  a `ResizeObserver` feedback loop, caught on the rig with a per-frame recorder
  minutes after he said it.
- *"it also shifts when the A–Z appears and disappears… shouldnt it be there all
  the time?"* — it should. The first fix removed the cause and left the symptom.

## THE NEXT TASK

**[#322](https://github.com/jampat000/Deluno/issues/322)** is the epic and the
running order. #312 is done. **Start with
[#324](https://github.com/jampat000/Deluno/issues/324) — Movies and TV need
their own controls, and the panel that holds them is overcomplicated.**

It moved in front of #311 deliberately. Today `variant` reaches
`library-filter-panel.tsx` and decides exactly two things: a hint under Year and
which `/genres` endpoint to call. The quick-filter chips, the nine sorts, the
eleven poster options and `CatalogueFilters` itself are **one list for both media
kinds**. #311, #306, #307 and #310 all add controls to that panel, and Sonarr's
TV-only fields (Episode Progress, Has Missing Season, Season Count, Scene
Numbering, Type) mean nothing on a film while Radarr's In Cinemas / Physical
Release / Collection mean nothing on a series. Poured into one shared panel that
is 59 controls, most of them inert on whichever shelf you are looking at.

James also reads the current two-panel split as overcomplicated, and merging
Display and Order into one **View** panel is the likely cause. #324 owes a
decision on the toolbar shape, not just a field list.

Then **#311** (TV series status / next airing / episode progress — the data
already arrives), then the rest per #322.

## What the last session did

**#312 — one continuous virtualised shelf with a jump rail.** `161e627`, fixed
in `1b2eb3f`. Paging was the wrong model, not the safe one. The keyset query is
unchanged; the client appends instead of replacing — 100 titles in the first
slice, then slices of 500 behind it.

Measured on the rig at 20,000 titles:

| | |
|---|---|
| First paint | **73 ms** |
| Whole library on the client | **1.4 s**, 41 requests, 7–14 ms a slice |
| Heap | **27.8 MB** |
| DOM nodes | **3,507** |
| Scroll frame | 10 ms median at an ordinary rate, 25 ms flung |

Radarr's 5,279 takes 3–5 s to first paint.

**The rail is derived from the rows on the shelf, not counted again in SQL.** The
issue proposed a grouped query per letter; once the shelf holds the whole library
that is a second implementation of the same filter, and the first disagreement
puts "S — 214" over a shelf holding 210. Walking the loaded rows is exact by
construction — searching "winter" gave 27 stops summing to exactly the 1,056 the
header printed. Under any order other than Title the stops are that field's own
grain: decades, size bands, ladder rungs, months.

Also: **"Not asked for" is gone** from the Subtitles column (it was true of a
library wanting no subtitles and a lie about every missing title in a library
wanting two), and **the smoke suite walks the shipped screen again** — see below.

## What the shelf taught, and it is the same lesson as always

**A control must not decide the layout that decides the control.**

The rail appearing took ten pixels off the scroll container; ten fewer pixels
re-measured the column track; a different column count changed the row count; a
different row count changed the total size; and the total size was what decided
whether the rail appeared. Chrome's loop guard abandoned the pass — which is why
it settled on its own — and while it ran, two absolutely positioned rows drew at
each other's offsets.

Three more instances of the same shape were in the same file:

- `updateColumns` appended its measuring probe **to the very element its own
  `ResizeObserver` was watching**. It measures from the parent now; the custom
  properties cascade, so it is the same measurement.
- The rail had no `min-h-0`, so 27 letters could stretch the flex row taller than
  the shelf — the rail deciding the height of the thing it measures.
- Whether to draw it read `clientHeight` during render with nothing subscribed,
  so the answer was taken while a settings panel had the shelf squeezed to 220px
  and never asked again.

That last one is **deleted rather than fixed**. The rail is always there, its
slot is always reserved, and its width is a function of the bucket labels — which
come from the sort field and the rows, inputs that flow one way.

**If you add anything beside the shelf, it gets a reserved slot or it does not go
there.**

## A suite that said 272 green was 268 green and four red

Four `workstation-workflow.spec.ts` tests had been failing on `main` since
Display and Order merged into **View** — the spec still asked for the old three
buttons. The previous handover recorded 272 passing, which was simply not true.

Deluno **does not run GitHub Actions**, so the only guard is somebody running the
suite locally. Run it before you claim a number, and if the last handover's
number and yours differ, find out why before assuming it was you.

## Non-negotiables

- Work directly on `main` and push for Deluno; MediaMop uses branch + PR with
  `--squash --admin`.
- **Never run GitHub Actions for Deluno**; do for MediaMop.
- Australian English.
- Stop `Deluno.Host` before any build. Kill stray `testhost` processes.
- Publish **SELF-CONTAINED** — the VM has no .NET runtime.
- Verify live rather than trusting a green suite.

## The rig — 10.1.1.142

Deluno at `http://10.1.1.142:5099`, `admin` / `Deluno-Lab-2026!`. Windows
`Administrator` / `Deluno-MM-Lab-2026!`.

**WinRM works from this machine and is by far the fastest way in:**

```powershell
$p = ConvertTo-SecureString 'Deluno-MM-Lab-2026!' -AsPlainText -Force
$c = New-Object System.Management.Automation.PSCredential('Administrator',$p)
$s = New-PSSession -ComputerName 10.1.1.142 -Credential $c
Copy-Item -ToSession $s -Path 'C:\Projects\Deluno\apps\web\dist\*' -Destination 'C:\Deluno\App\wwwroot' -Recurse -Force
```

A front-end change is `npm run build:web` plus that copy — no publish, no
restart. Any C# change needs a republish or you verify a stale backend.

**Seeding a big library.** `scripts/lab/seed-library.py` fills a movies
catalogue with N synthetic titles, spread so every rail stop has something behind
it. Stop the host, copy `movies.db` **and** its `-wal`/`-shm` down, run it
locally, move the VM's stale sidecars aside, copy back. Undo is
`DELETE FROM movie_entries WHERE id LIKE 'seed%'`. **The rig is back to its 11
real movies** — re-seed if you need scale, and clean up after, because 4,400 fake
missing titles would enter the automation cycle.

## James's live arr instances — read-only

`10.1.1.35` — Radarr `:8310`, Sonarr `:8989`, Prowlarr `:9696`, Bazarr `:6767`.
**Look, do not save.** No custom filters, no Interactive Search, no "Search All"
— those fire real queries at his trackers.

## Traps

- **The Write tool silently overwrites an existing file.** Check whether a file
  exists before writing it.
- **Bash heredocs fail on some content** with `unexpected EOF`. Write a Python
  script into the scratchpad and run it with `python <path>`. Python plus
  `io.open(..., encoding='utf-8')` is the reliable way to edit source here.
- **The Bash tool's working directory persists between calls**, including after a
  failed `cd`. Prefer absolute paths or re-`cd` each time.
- **The in-app browser pane's screenshot times out.** Use Claude in Chrome.
- **Screenshots of Radarr's 5,279-movie grid time out.** Use `get_page_text` and
  `read_page`.
- **To catch something visual that will not hold still**, install a
  `requestAnimationFrame` recorder in the page that logs only *changes* to the
  geometry you care about, plus a `window.onerror` hook — `ResizeObserver loop
  completed with undelivered notifications` arrives as an error event and is the
  clearest possible signal that a layout is fighting itself. That is how the
  shake was caught in one pass.
- The gateway is a Cloudflare worker; `wrangler` is authenticated here. **Bump
  `buildCacheKey`'s shape version and `SearchCacheShape` whenever
  `MetadataSearchResult` gains a field.**
- The rig's calendar cannot be exercised: no film has a release date in its
  window.
- Never run publish and Playwright at once.

## Architecture rules that keep being paid for

1. **ADR-001** — Movies and Series are parallel copies and the duplication is
   actively reproducing. Anything new is shared from its first line through
   `MediaTableMap.For(MediaKind)`.
2. **Filters and sorts are indexed columns.** Wanted-state values get the
   V0016/V0017 treatment — cached on the title's row, maintained by a **trigger**
   so no write path can forget them.
3. **The counts above the shelf count the rows on it.** One answer, not two
   queries that agree today.
4. **A page asking for nothing runs exactly the query it ran before the feature
   existed.**
5. **No second scheduler, no second lane, no second worker** (DESIGN-002 rule 3).
6. **Named typed fields, never a generic rule engine.** The last one was deleted
   in #302 because it could express filters nothing could answer.

## Also open

**#325** (sort title — *The Matrix* files under T, and the rail makes it
obvious), **#326** (cached artwork is smaller than the places Deluno draws it —
the title-page backdrop is upscaled 1.8×, measured), **#305** (the worker lane
loop has no tests), **#301** (Subber steps 2–6; #321 is the Bazarr delta), and
**#78 / #81 / #82 / #129** — GA readiness, externally blocked.

## What every defect in this codebase has had in common

One rule written twice in places that could not check each other. Two copies of
`shortQuality`. `DisplayOptions` declared twice. A blob written in one case and
read in another. A consumption recorded in a local variable and persisted from a
different branch. A cache key that did not know what shape it was caching. A rail
whose width decided whether the rail existed.

**When you fix something, the next question is where else that shape lives.**
