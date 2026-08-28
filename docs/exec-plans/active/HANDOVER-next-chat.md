# Deluno — handover

You're picking up Deluno (`C:\Projects\Deluno`, github.com/jampat000/Deluno): a
Windows .NET 10 + React 19 media-automation app replacing Radarr, Sonarr,
Prowlarr, Huntarr, Cleanuparr, Recyclarr, Upgradarr, Trash Guides and Bazarr.

**Read `docs/PRODUCT_NORTH_STAR.md` first.** It records what each of those
platforms actually does — read from its own source, not from memory — and the
five-question standing check every change answers before it is called done.

Then `DESIGN-002-subber.md` (the current stream), `HANDOVER-live-e2e-run.md` for
the lab rig, and `DESIGN-001`, `DESIGN-003`, `DESIGN-004`, `DESIGN-005`.

## Baseline

`main` at **`62afe5c`**, working tree clean, everything pushed. Every number
below was run at this commit — none carried forward.

| Suite | |
|---|---|
| .NET (`dotnet test Deluno.slnx`) | **952 passed**, 1 skipped |
| Web unit (`npm run test:unit:web`) | **137 passed**, 18 files |
| Playwright (`npm run test:web`) | **272 passed**, 10 skipped |
| Metadata gateway | **17 passed**, 0 failed |
| `npm run ci:check` | 7 passed, 0 warned, 0 failed |

### The rig at 10.1.1.142, as left

A working subtitle install, deliberately holding one of every case so the next
session can see all three rungs without setting anything up.

- **Movies** — 11 films, automation off, wants `en, es`. Big Buck Bunny is the
  only one with a file.
- **TV Shows** — 6 shows, automation off, 12 h interval, wants `en`. Only
  Severance has files: three episodes under
  `C:\Deluno\Library\TV\Severance (2022)\Season 01`.
  - `S01E01` and `S01E02` are named `...1080p.WEB.H264-TEPES` and hold Gestdown's
    TEPES subtitle — **at the cutoff**, no attempt row, Deluno has stopped.
  - `S01E03` is deliberately `...1080p.BluRay.x265-NOBODY`, so the same subtitle
    lands **below** the cutoff and stays on the upgrade list with a backoff.
  - So its poster reads `MISSING · 3/20` and its subtitle bar is two-thirds
    gold, one-third green.
- **Gestdown** is the only configured provider, healthy. No account needed.
- Automation is off on both libraries **on purpose** — subtitles run anyway now,
  which is the whole point of `774589c`. Turning it on starts release searching.

## The bar, in James's words

Short answers, few questions, pictures over prose. Simplicity is the product.
Repetition is a defect — he will spot it on screen before any test does.
Measure, don't assert.

> *"instead of being ahead we will still be behind"*

**A new axis does not excuse a smaller number on an old one.**

He corrects bluntly and is usually right. When he corrects a premise, change the
work rather than defend the reasoning. Four times last session:

- *"so radarr has 33 and we still only have 31 how is that possible?"* — right,
  and worse than the number: twelve of the 31 are axes Radarr has none of, so on
  Radarr's own list Deluno has **15 of 32**. See #306.
- *"all this should be the same size height"* — three heights on one toolbar row.
- *"this is still not organised properly considering what we saw in radarr"* —
  four different control shapes in a row that should have had one.
- *"https://yifysubtitles.tv/"* — he was right the site is alive. It still could
  not be used, and finding out why took four requests rather than an assumption.
- *"I think we misunderstood each other"* — a whole top-level nav area where two
  tabs were wanted. When an instruction names an existing part of the app, the
  likely reading is *like that one*, not *next to it*.

## THE CURRENT TASK — finish the Subber stream

James: *"I want to close off the subtitle / subber stream of work which includes
removing it from mediamop as well so lets put all focus into that and dont stop
until its done completely."*

**#301** is the epic, **#321** is the Bazarr delta, and DESIGN-002 is the plan.

**DESIGN-002's six build steps are all done.** What is left of the stream is
#321's remaining seven settings, manual search, blacklist and #327 — additions
on top of a loop that now runs end to end, not gaps in it.

### Done

| DESIGN-002 step | |
|---|---|
| 1. Languages, held state read from disk, the bar | shipped earlier |
| 2. Providers as Connections | `1a981d0` |
| 3. Search and write, on the library cycle | `1a981d0` |
| 4. The remaining providers | `1a981d0`, trimmed in `b052b66` |
| 5. Backoff | `6081c95` |
| 6. Remove from MediaMop | MediaMop [#327](https://github.com/jampat000/MediaMop/pull/327), merged |
| Upgrades — the open half of step 5 | `3de1f65`, `48fcb4a` |

Plus the provider screen, #321's first two settings, and the settings' home:
**Media Management → Subtitles** for the per-library languages, **Find &
Download → Subtitle Providers** for the sources (`6dc22e5`). A first attempt made
Subtitles a top-level area of its own, which was a misreading — James said *"in
staying with the theme in media management"*, meaning consistent with how that
area works, not beside it.

**Six providers, not MediaMop's eight.** OpenSubtitles `.org` and `.com` were one
source counted twice — separate credentials on its settings screen, one handler
underneath. YifySubtitles is gone: its old `/api?q=` path answers with HTML on
every host it used, and `yifysubtitles.tv` — which James correctly pointed out is
alive — has a real `/api/search/` returning *films*, while the listing behind
them serves an interstitial marked `noindex, nofollow` that redirects to an
unrelated third-party domain. An advertising gate, not a subtitle source.

### The end-to-end fetch, and what getting it cost

**A `.srt` has landed on the rig, twice.** Big Buck Bunny's MKV was remuxed with
`-sn` into `Severance (2022)/Season 01/Severance - S01E01 - ... .mkv`, imported
by the existing-library import, and Gestdown wrote **44,445 bytes of real
English** beside it. The bar went to 1 of 1, `held` carries `fetched` and the
provider, and Activity reads *"Fetched 1 of 1 subtitle(s) looked for in TV
Shows."*

**Proving it found the defect,** and it is the one this feature was most likely
to ship with. The only way to make the first fetch happen was to press *search
now*. Subtitle scanning and fetching were planned **inside the release-search
branch**, so they inherited its two switches: a library with "Search
automatically" off — which that screen calls *keep this library manual*, meaning
manual **releases** — asked for English every day and never got it, silently. So
did a library with searching on but neither missing nor upgrade selected. That
is exactly the person Bazarr exists for, and Deluno was refusing them.

Fixed in `774589c`: `next_subtitle_search_utc`, its own column with its own
guarded writer, planned by the same cycle in the same window under the same
manual override — DESIGN-002 rule 3 intact — but no longer behind the release
switches. `next_search_utc` deliberately does not fold it in, so a paused
library still reads paused. Four tests, each failing without the fix.

**Then proven unattended:** a second episode file was imported, the library left
at auto **off**, missing off, upgrade off, nothing requested, and the second
`.srt` (46,197 bytes) appeared on its own.

### What James decided, and what came out of it

Four blockers were put to him in one round. He answered all four, and two of the
answers changed the work rather than confirming it.

**The subtitle bar's words are the ladder's words** — three goes to get there.
"Held" first (*"missing is good, held sucks as far as choice of words"*), then
"Have", then "Ready"/"Done", and finally the right answer: *"users need to also
be able to distinguish between done and ready for subtitles its a little
ambiguous compared to the status of the files."* The bar had invented synonyms
for rungs the dot already names. It now reads **Quality met / Upgradable /
Missing**, the same words and colours as the dot, because DESIGN-002 says the bar
is a miniature of that ladder.

**Subtitles share no timing at all** — *"I dont agree that it shares a cycle or
schedule and this was told to you back when I said nothing should be shared or
have to wait for another process or anything."* The first fix that day had
freed the two switches and left the clock borrowed. Now: own five-minute
cadence, no search window, own retry delay, and an import makes subtitles due
immediately. Measured on the rig: import at 04:15:54, `.srt` at 04:16:03.

**"Better" was researched, not chosen from a menu** — *"this is the thing that
we need to look into with bazaar and how it does it properly."* Bazarr's
scoring was read at master. Its eleven weights are gates with a tiebreaker tail:
at the shipped 90%, the right episode alone scores 86% and fails; add `source`
and it is 93% and passes. So its default means *cut for the same kind of
release*. Deluno's cutoff goes one rung further — *"we need the best method, no
point spreading lies about subs that may be out of sync"* — to a subtitle that
names your exact release group. Gold shipped with it.

**Scope** — timing sync and content modification stay in the stream; Whisper
([#329](https://github.com/jampat000/Deluno/issues/329)) and machine translation
([#330](https://github.com/jampat000/Deluno/issues/330)) went to the backlog.

### Then the rule got bigger than subtitles

*"we need to ensure nothing shares a schedule or timer, everything this app does
needs to fire independently when it wants to and when it needs to."*

`b1109aa`. Lanes were grouped by the resource they contend on, which is a good
reason to size them differently and a bad reason to make them queue behind each
other: the lease is `ORDER BY scheduled_utc` across every job type on a lane, so
on the two-slot intake lane a backlog of `intake.sync` starved
`library.subtitles.search` outright. Now **18 lanes, one per kind of work**, and
three planning lanes where the three planners used to be awaited in sequence.

Lanes stopped polling. One that leases nothing asks when its *own* next job is
due (`NextDueUtcAsync`, served by `ix_job_queue_type_status_scheduled`) and
sleeps until then, waking early on a signal.

**Measured on the rig, idle, two minutes each — and the middle row is the point:**

| | |
|---|---|
| 7 shared lanes, 30 s tick | 1.30% CPU, 150 MB |
| 18 lanes, first cut | 1.64% CPU, 161 MB |
| 18 lanes, tuned | **0.98% CPU, 139 MB** |

The query arithmetic said the split would be cheaper and the machine said
otherwise. Sizing the planners by what they plan and widening the settings cache
from 1 s to 15 s took it below where it started. Independence ended up cheaper
than sharing, but only after it was measured.

**Lane width follows the machine** — *"4 slot lane doesnt sound enough though we
need to maximise this what if someone was a power user."* They were constants, so
a six-core box and a thirty-two-core server ran identical widths. Local work
scales with cores; network work goes to twice that, because a thread asleep on a
socket is not using a core and what protects a tracker is
`IOutboundRequestThrottle` pacing per host.

**A regression this caused, and how it was found.** Nothing signals a job that
was already queued when the process starts, so with a five-minute backstop a
restart stranded the queue for that long. Caught on the rig — an import enqueued
before a deploy was still queued five minutes after the host came back. A lane's
first act is now to look before waiting, and startup jitter is capped at two
seconds rather than a quarter of the interval. The tests did not catch it; the
rig did.

### And the TV mark, which was a bug not a legend

*"the colour/status legend works for movies but it doesnt quite work for tv with
regards to quality met and subtitles because of the show and then the episodes."*

`62afe5c`. **A show's rung was computed twice from different inputs and the two
disagreed.** The server stored it from the title-level row — `has_file` and
`current_quality` of whichever file the import saw first — while the browser
recomputed it from episode counts. Severance with three of twenty episodes was
"Quality met" to the chips and "Missing" on its own poster, and the chips summed
to **seven for six shows**.

A collection has no title-level file, so it can have no title-level quality.
`SeriesRung` in `Deluno.Contracts` is the only place that answers it now; the
facets stopped rebuilding the ladder a third time out of `has_file` and
`quality_cutoff_met`.

**How far along, without a fifth rung.** Three of twenty and none of
eighty-seven were both Missing and both red. Sonarr solves this with a filled bar
on the poster's edge — but *"adding a bar isnt a good idea, the bar is strictly
for subtitles."* Drawn into the dot as an arc it was correct and illegible: 15%
of a nine-pixel dot is about one pixel, and at 0% the whole dot washed out. So it
is **text on the chip**: `MISSING · 3/20` beside `MISSING · 0/87`. Dots are one
size everywhere now, because the chip's was hard-coded at 9 against the legend's
13.

### Not done — pick up here

1. **Timing sync**, then **content modification**. James chose these two out of
   #321's larger items; Whisper and translation went to the backlog as
   [#329](https://github.com/jampat000/Deluno/issues/329) and
   [#330](https://github.com/jampat000/Deluno/issues/330). Timing sync is the one
   that saves the most hand-editing, and Deluno already ships ffprobe handling.
2. **Manual search and blacklist** — DESIGN-002's "new, and worth it" list.
   Manual search is more useful than it was: it can now show which rung each
   candidate is on, which is the thing a person is actually choosing between.
3. **#321's smaller remainder:** adaptive searching *per provider* (the backoff
   that landed is per title+language, which is not the same thing),
   post-processing, language equals, HI extensions.
4. Then **[#322](https://github.com/jampat000/Deluno/issues/322)'s running
   order** — #306 (the honest filter count and its migration, which closes
   #319), #328 (Tags), #311 (TV status, next airing, episode progress — now
   partly served by `SeriesRung`).

### Audited and deliberately left alone

Recorded so it is not re-litigated, and so the reasoning can be attacked if it is
wrong.

- **Missing and upgrade searches share `SearchIntervalHours`.** They already have
  independent cursors, and the planner dedupes to at most one of each per
  library on a twelve-wide lane, so neither can starve the other. One setting
  meaning "how often this library visits its indexers" is honest, and both kinds
  visit the same indexers.
- **The subtitle cutoff is not a setting.** It is `SubtitleCutoff.Rung`, one
  constant at the top rung, because James asked for *"the best method, no point
  spreading lies about subs that may be out of sync."* If anybody ever wants
  "same source is good enough for the kids' films", it becomes a per-library
  choice beside the quality profile.
- **The subtitle bar counts only over episodes a show holds.** A file you do not
  have cannot be short of a subtitle, and the dot now says `3/20` so the poster
  is not claiming completeness. One consequence: the bar can go *backwards* as
  new episodes land without subtitles. That is correct.

## What the rig caught that no test would have

**Six now, across two sessions, and not one of them was a failing test.**

This session's three:

4. **Gestdown puts a bare `TEPES` in its `version` field**, and answers some
   queries with a comma-separated list of releases. `MediaFileNameFacts` looks
   for the trailing `-GROUP` convention — right for a file name, wrong for both
   of those — so every Gestdown subtitle would have scored at the bottom rung and
   been re-fetched for ever.
5. **A new field never reached the browser.** The API sent
   `subtitleLanguagesSettled`, the bar drew nothing, and nothing failed:
   `adaptMovieItems` and `adaptSeriesItems` copy the catalogue row field by
   field, twice, and every field is optional so the types were happy.
6. **A restart stranded the whole queue for five minutes.** Nothing signals work
   that was already queued when the process starts, and the new backstop is five
   minutes. Only visible by watching a real deploy.

And the earlier three:

1. **Gestdown answers with `matchingSubtitles`,** not `subtitles`. Both my client
   and MediaMop's read the wrong key — so *"ported from code that works"* is not
   evidence. Call the real endpoint.
2. **A host that would not resolve was reported as "wrong or expired
   credentials".** The search swallowed the DNS failure and returned an empty
   list. Unreachable and unhelpful are separate outcomes now.
3. **A menu inside the library toolbar is invisible.** The card clips its
   children with `overflow-hidden` to keep its rounded corners. `MenuSelect`
   already solved that by portalling out — and its comment names that very
   toolbar. A second popover written by hand re-created a fixed defect.

## Non-negotiables

- Work directly on `main` and push for Deluno; **MediaMop** uses branch + PR with
  `--squash --admin`.
- **Never run GitHub Actions for Deluno**; do for MediaMop.
- Australian English.
- Stop `Deluno.Host` before any build. **Kill stray `testhost` processes** — they
  lock the test DLLs and the build fails with MSB3027.
- Publish **SELF-CONTAINED** — the VM has no .NET runtime.
- Verify live rather than trusting a green suite.

## The rig — 10.1.1.142

Deluno at `http://10.1.1.142:5099`, `admin` / `Deluno-Lab-2026!`. Windows
`Administrator` / `Deluno-MM-Lab-2026!`.

```powershell
$p = ConvertTo-SecureString 'Deluno-MM-Lab-2026!' -AsPlainText -Force
$c = New-Object System.Management.Automation.PSCredential('Administrator',$p)
$s = New-PSSession -ComputerName 10.1.1.142 -Credential $c
Copy-Item -ToSession $s -Path 'C:\Projects\Deluno\apps\web\dist\*' -Destination 'C:\Deluno\App\wwwroot' -Recurse -Force
```

A front-end change is `npm run build:web` plus that copy. **A C# change needs a
republish**, and the host runs from a scheduled task called `Deluno Host` —
`Stop-ScheduledTask` / `Start-ScheduledTask`. Starting it with `Start-Process`
over WinRM does not work: the process dies with the runspace, and DPAPI cannot
decrypt stored secrets from a WinRM session.

## Traps

- **The Write tool silently overwrites an existing file.**
- **Bash heredocs fail on some content.** Write a Python script into the
  scratchpad and run it with `python <path>`. Watch `\uXXXX` escapes: a heredoc
  with `'PYEOF'` passes them through literally and they will not match the file.
- **The Bash tool's working directory persists between calls**, including after
  a failed `cd`.
- **The in-app browser pane's screenshot times out.** Use Claude in Chrome — and
  its `ref` and coordinate clicks can land on the wrong element after a reload.
  Verify what actually happened with `javascript_tool` rather than the screenshot.
- **Never quote a suite number from a background run you started before your last
  edit.** Re-run it.
- The gateway is a Cloudflare worker; `wrangler` is authenticated here. **Bump
  `buildCacheKey`'s shape version and `SearchCacheShape` whenever
  `MetadataSearchResult` gains a field.**
- `scripts/lab/seed-library.py` gives a 20,000-title library. The rig is on its
  11 real movies; clean up after seeding.
- James's live arr instances at `10.1.1.35` — Radarr `:8310`, Sonarr `:8989`,
  Prowlarr `:9696`, Bazarr `:6767`. **Look, do not save.**

## Waiting behind Subber

**#322** is the epic and running order for the rest. **#324 is done**
(`c5ac944`, `60527dc`): the control set is declared once per `MediaKind` on the
server and served to the browser, so a filter field is one row of data rather
than eight edits across two languages. Filters went 9 → **31 on movies, 28 on
TV**, and **#308 came out of it complete** — "not searched in the last 30 days"
runs on the rig and includes the never-searched.

The toolbar settled as: **pick one thing is a menu** (Library, Sort), **build
something is a drawer** (Filter, View).

Captured on the way past, so it is not re-derived:

- **#306** carries the drafted migration and the honest count. It closes **#319**
  in the same migration. Its migration numbers have shifted — movies V0017 and
  series V0018 were taken by the subtitle attempt tables.
- **#328** — Tags. The `tags` table and `/api/tags` exist; nothing can carry one.
- **#327** — the subtitle bar's legend.

Then **#311** (TV series status, next airing, episode progress), which now has
somewhere TV-only to live.

## What every defect in this codebase has had in common

One rule written twice in places that could not check each other. Two copies of
`shortQuality`. `DisplayOptions` declared twice. A rail whose width decided
whether the rail existed. A subtitle setting on two screens. A sidebar area and a
topbar title kept in two lists, so a new area was nameless.

**When you fix something, the next question is where else that shape lives.**
