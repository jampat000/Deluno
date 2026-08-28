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

Working tree clean, everything pushed. Every number below was measured at
**this session's last code commit** — none carried forward.

| Suite | |
|---|---|
| .NET (`dotnet test Deluno.slnx`) | **984 passed**, 1 skipped |
| Web unit (`npm run test:unit:web`) | **137 passed**, 18 files |
| Playwright (`npm run test:web`) | **272 passed**, 10 skipped |
| Metadata gateway | **17 passed**, 0 failed |
| `npm run ci:check` | 7 passed, 0 warned, 0 failed |

The .NET number went 952 → 984: 22 for subtitle timing sync, 3 for the job names
that turned out to be missing, and 7 for the subtitle re-read cadence. Every one of these was run at this
session's code, not carried forward.

**Two tests flaked under full-suite load and passed alone**, and they are worth
knowing about before a future session chases one:
`ProcessorOutputReadinessTests.Rejects_a_recently_written_file` (.NET) and
`protected route does not crash before auth: /movies` (Playwright). The
Playwright suite failed once at 271 and then ran clean at 272; the navigation
spec passes 40 of 40 on its own. Neither is near anything this session touched —
no web code was changed at all — but a flake nobody wrote down is a flake
somebody re-diagnoses.

**The publish now carries FFmpeg** — 128 MB of LGPL shared build under
`tools/ffmpeg`, fetched by `scripts/fetch-ffmpeg.ps1` and cached, so the first
publish on a new machine downloads 67 MB and every one after that does not.

### The rig at 10.1.1.142, as left

Unchanged from last session except in four ways:

- **It has FFmpeg now**, shipped with the publish under `tools/ffmpeg`. It had
  ffprobe before, by hand — see the correction below.
- **TV Shows asks for `en, fr`** rather than `en`, and there are now two French
  `.srt` files beside E02 and E03. Changed deliberately: English was already
  satisfied, so the only way to make the library want anything — and therefore
  the only way to watch the fetch-to-sync chain run unattended — was to ask for a
  language Gestdown actually has for this show. Spanish was tried first and
  Gestdown has none, which is itself worth knowing. Set it back to `en` and
  delete the two `.fr.srt` files if they are in the way; a `es` backoff row is
  also sitting on each episode and will expire on its own.
- **`Severance - S01E03 … .en.srt` was deleted**, which is what found the
  re-read defect below. Deluno has now noticed it is gone and wants English for
  that episode again; it will fetch it once the ordinary six-hour attempt
  backoff expires, at 10:43 UTC on 28 August. Nothing needs doing.
- **Its three `episode_subtitle_scan` rows were backdated a day** so the
  twelve-hour re-read would fire while somebody was watching. They have since
  been rewritten by a real pass, so this leaves no trace.
  `C:\Deluno\Data\series.db.before-reread-20260828-175940` is the database as
  it was before that, and can be deleted.

Otherwise as before: 11 films and 6 shows, automation off on both on purpose,
Gestdown the only provider, and **all three MKVs are the same 59 MB Big Buck
Bunny remux** under Severance filenames. That last fact is easy to forget and it
matters: the rig's subtitle text and its audio are genuinely unrelated, so the
rig is a good test of sync *refusing* and no test at all of sync *working*.

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

### Timing sync — built, and what it cost

**#321 item 4, and the first of the two things James picked.** DESIGN-002 now has
a *"Timing sync, as built"* section with the whole of it; this is what a person
picking the work up needs to know.

**Deluno ships FFmpeg now.** That was a decision put to James before any code was
written, because it is a 128 MB decision and not mine to make. The premise
DESIGN-002 was working from was still wrong — it says *"Deluno already ships
ffprobe handling, so the same shape applies"*, and it ships the handling and
never shipped the binary.

**A correction, because I got the evidence wrong first time.** I checked the rig
for ffprobe with `Get-Command` and the `DELUNO_FFPROBE_PATH` variable, found
neither, and reported that the rig had been running blind for a whole session.
That was wrong, and reading the scan table is what caught it: every row says
`probe_status: succeeded`. **`C:\Deluno\App\ffprobe.exe` was put there by hand on
27 August**, and Deluno resolves a binary sitting beside its own executable —
which my check never looked at. Stream validation and the embedded-subtitle half
of the scan were both working the whole time.

What *was* genuinely missing is the half this feature needs. `ffmpeg.exe` is on
the rig too, at `C:\Deluno\Tools\ffmpeg.exe` — a folder Deluno has never looked
in. So it sat on the same disk as the app that needed it and was invisible to it,
which is a better argument for shipping FFmpeg than the one I made: the install
that *had* the binary still could not use it.

Fetch is `scripts/fetch-ffmpeg.ps1`, cached in `tools/ffmpeg` (gitignored) and
copied into the publish. LGPL **shared**, pinned to `n9.0`: the GPL builds cannot
ship inside a product and a static LGPL build would oblige us to hand out object
files for relinking.

**What decides a subtitle gets timed: the cutoff, and nothing else.** Bazarr
syncs what scores under a threshold and hands you the threshold. Deluno already
drew that line — a subtitle at `MadeForThisFile` was cut against this encode and
is in time by construction — so the rung that keeps a subtitle on the upgrade
list is the rung that sends it to be timed. No setting.

**It has its own job and its own lane.** `subtitle.sync` on `subtitles.sync`,
because timing is seconds of local FFmpeg and `subtitles.search` exists to spend
a provider's daily allowance. Doing it inline in the fetch would have been one
line and the fourth instance of the mistake this codebase keeps making.

**The guard was wrong twice, and measuring is what found it both times.** The
whole risk of this feature is moving a subtitle that was already fine: a
correlation always has a best shift, and telling a real one from a lucky one is
the entire problem.

| Guard | Real match | Unrelated pair | Verdict |
|---|---|---|---|
| Coverage — how much lands on speech | — | **41%** | Useless. Two films with talking in them are both talking most of the time. |
| Ratio to the mean overlap | **1.64** | **1.13** | Works, either side of a 1.5 threshold. A margin of 0.14 is not a margin. |
| Peak in standard deviations | **4.7–4.8** | **1.3–2.0** | Shipped, at 3.0. |

Measured on the lab episode's real audio through the real FFmpeg. A matching
subtitle displaced by anything from 300 ms to 30 s, in either direction, came
back **exact to the millisecond**, nine times out of nine. The rig's own
Severance subtitle over Big Buck Bunny audio reached 1.3 σ and was left
untouched, which is the outcome that matters more.

**And then unattended on the rig, end to end.** TV Shows was set to want `en, fr`
— Gestdown has French for these episodes and English was already satisfied, so
this was the only way to make the library want anything. Nothing was pressed:

```
Fetched 2 of 3 subtitle(s) looked for in TV Shows.
Added a subtitle timing check to the queue.
Started timing a subtitle against the video's audio.
Severance - S01E03 … .fr.srt: This subtitle does not line up with the video's
  dialogue at any one point, so it has been left exactly as it was.
```

Both fetches landed below the cutoff, both enqueued a `subtitle.sync`, both ran
on the new lane, and both refused — correctly, because the rig's audio is Big
Buck Bunny and its subtitles are Severance. **Three seconds a job**, 07:31:54 to
07:31:57. The two Activity lines naming the new job type come from the job-name
table below, which is the other half of this working.

**Not built, and deliberately:** framerate mismatch (a PAL-to-NTSC subtitle
drifts rather than offsets, and one shift cannot fix it), subtitles already on
disk (only what Deluno fetches below the cutoff is queued — a library full of
somebody's old Bazarr subtitles gets nothing until each is re-fetched), and the
original audio language (the sync prefers the track matching the title's own
language and is never told it, because the wanted row does not carry it; it falls
back to the first audio track, which is where every muxer puts the original).

### Three job types were nameless in Activity, and it was the usual shape

Adding `subtitle.sync` meant naming it in **three separate switch statements** in
`SqliteJobStore` — queued, started, and the queue row's title. They were three
lists in three methods that could not check each other, and it had already gone
wrong: `episode.search`, `intake.sync` and `library.import.existing` were in none
of them, so Activity showed a person the raw string `library.import.existing`
where it meant to say *Library scan*.

One table now, and `JobTypeWordsTests` finds the job types by reflection off the
registered handlers rather than from a fourth hand-written list — so a new
handler with no words fails without anybody remembering to check.

### Deleting a subtitle was invisible to Deluno — fixed

Found trying to make the rig re-fetch one. Deleting
`Severance - S01E03 … .en.srt` from disk changed nothing: the library went on
reporting *"Every file in TV Shows has the subtitles you asked for"*, for ever.

`ListPendingScansAsync` re-read a file when the scan row was missing, the path
changed, the size changed, or the last probe was `unavailable`. **Deleting a
sidecar changes none of those** — it is a different file — so the scan never
looked again and the row saying English was held stood permanently. The commoner
half is the same blind spot in reverse: a subtitle you drop in by hand was never
noticed either.

**The fix is a cadence, and the reason it needs no setting is the interesting
part.** A file is now re-read twelve hours after it was last read, whatever the
video did. Bazarr does the same thing and has to expose *"use cached embedded
subtitles parser results"* as a switch, because it re-parses containers on every
pass and people needed a way to stop it. Deluno already records what the video
was, so it can tell the two halves apart on its own:

| | Costs | When |
|---|---|---|
| The files beside the video | one directory listing | every pass |
| The tracks inside the container | one ffprobe process | only when the video is new, renamed, resized, or was never successfully probed |

`MediaSubtitleScanCandidate.VideoChanged` carries which is needed, and the tracks
in a container cannot move while the container does not. So the standing check's
question — can Deluno decide and explain the consequence once — is answered yes,
and there is no trade-off to hand anybody.

**Two things fell out of it that were not the reported bug.**

`RecordScanAsync` replaces everything it is told about a file and deletes
anything it is not, which is right and is how a deletion corrects itself. It also
means an incomplete list is a destructive one: a folder-only re-read finds no
embedded tracks, and handing that over as the whole truth would delete every
embedded subtitle in a library twelve hours after it was found. That rule is
`LibrarySubtitleScanJobHandler.WholeTruth`, named rather than inlined and tested
four ways — **the rig cannot check it**, because its videos were remuxed with
`-sn` and hold no embedded tracks at all.

And `SubtitleSources.Fetched`'s own summary promises that a rescan does not turn
Deluno's own work into an anonymous file it knows nothing about. Only `provider`
was actually being kept; `source` flipped to `external` the first time a rescan
found the file sitting there. Rare while a rescan needed the video to change, and
routine the moment one runs on a cadence.

**Verified on the rig, on the very file that found the defect.** The three scan
rows were aged by a day — deliberately backdated rather than deleted, because
deleting them would have made every file look never-read, which is the *old*
path. Activity then said **"Read 3 file(s) in TV Shows for subtitles"** where it
had said "Every file in TV Shows has been read" for hours, and afterwards:

| | Before | After |
|---|---|---|
| `subtitleLanguagesHeld` on Severance | 5 | **4** |
| E03's English row, pointing at a file that is not there | present | **pruned** |
| `source` / `provider` on the four survivors | `fetched` / `gestdown` | **unchanged** |
| `match_rung` on the four survivors | 2, 2, 1, 0 | **unchanged** |
| `probe_status` after the pass | `succeeded` | **`cached`** |

That last row is the cheap half doing its job: not one ffprobe was spawned. The
re-fetch itself was not watched, because E03's English is sitting on the ordinary
six-hour attempt backoff from its last fetch and is not eligible until 10:43 UTC
— that is the existing, already-proven machinery, and the fix is that the
language is wanted again at all.

### Not done — pick up here

1. **Content modification** — the second of the two James picked out of #321,
   and now the next thing. Bazarr's Sub-Zero options: strip hearing-impaired
   tags, remove style tags, remove emoji, OCR fixes, common whitespace and
   punctuation fixes, fix all-uppercase, add colour, reverse RTL punctuation. A
   whole category DESIGN-002 does not mention — making a subtitle *usable* after
   fetching it.

   **It has a head start that did not exist this morning.** `SubtitleTimeline`
   reads a subtitle into cues and writes it back canonically, tolerating every
   shape real files arrive in, and it decodes Windows-1252 as well as UTF-8.
   Content modification is a transform over `SubtitleCue.Text` and nothing else,
   and it should ride the `subtitle.sync` lane rather than invent a second one —
   both are "open the file Deluno just wrote and improve it", and a second lane
   for the second half of that sentence is the mistake this codebase keeps
   making. Consider renaming the lane before adding to it.

   Timing sync itself is **done** — see above. Whisper and translation stay in
   the backlog as [#329](https://github.com/jampat000/Deluno/issues/329) and
   [#330](https://github.com/jampat000/Deluno/issues/330).
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

**Ten now, across three sessions, and not one of them was a failing test.**

Two of this session's were found by checking the rig *before* trusting a plan
rather than after writing the code, which is cheaper and should be the habit.

This session's four, and the first is the largest:

7. **The ffmpeg the rig had was in a folder Deluno never looks in.**
   `C:\Deluno\Tools\ffmpeg.exe`, on the same disk as the app that needed it and
   invisible to it, so timing sync would have had no engine. Deluno now ships
   its own. *(This started as a bigger claim — that the rig had no ffprobe
   either and three features were silently dark. That was wrong: ffprobe was
   sitting beside the executable, where Deluno does look. Reading the scan
   table, which says `probe_status: succeeded` on every row, is what corrected
   it. Checking one resolution path and concluding "not installed" is its own
   lesson.)*
8. **Deleting a subtitle from disk is invisible.** The scan re-reads a file whose
   path, size or probe status changed, and deleting a sidecar changes none of
   them. The library still says every file has the subtitles you asked for. See
   above; it wants an issue.
9. **Three job types had no words in Activity**, because their names lived in
   three switch statements that could not check each other.
10. **A subtitle for a different film overlaps a matching one by 41%.** The first
    guard against moving a subtitle that was already fine would have moved it.

And the previous session's three:

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
