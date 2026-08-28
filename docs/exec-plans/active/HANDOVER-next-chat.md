# Deluno — handover

You're picking up Deluno (`C:\Projects\Deluno`, github.com/jampat000/Deluno): a
Windows .NET 10 + React 19 media-automation app replacing Radarr, Sonarr,
Prowlarr, Huntarr, Cleanuparr, Recyclarr, Upgradarr, Trash Guides and Bazarr.

**Read `docs/PRODUCT_NORTH_STAR.md` first.** It records what each of those
platforms actually does — read from its own source, not from memory — and the
five-question standing check every change answers before it is called done.

Then `DESIGN-002-subber.md` (the current stream), `HANDOVER-live-e2e-run.md` for
the lab rig, and `DESIGN-001`, `DESIGN-003`, `DESIGN-004`, `DESIGN-005`.

`main` is at `6dc22e5`, working tree clean. **902 .NET tests, 124 web unit
tests**, both run rather than carried forward. Playwright was last run green at
`60527dc` (272 passed / 10 skipped) and **has not been run since** — subtitle
navigation moved twice after it, so run it before you quote it.

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

### Done

| DESIGN-002 step | |
|---|---|
| 1. Languages, held state read from disk, the bar | shipped earlier |
| 2. Providers as Connections | `1a981d0` |
| 3. Search and write, on the library cycle | `1a981d0` |
| 4. The remaining providers | `1a981d0`, trimmed in `b052b66` |
| 5. Backoff | `6081c95` |

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

### Not done

1. **An end-to-end fetch has never run on the rig,** and DESIGN-002 makes that
   the condition for step 6. Gestdown's *search* is proven live — "answered with
   6 subtitle(s) for the test title" — and the write is proven by test, but no
   file has actually landed. The rig has one film with a file (Big Buck Bunny)
   and Gestdown is TV-only; Podnapisi covers films and **does not resolve from
   this machine or the rig**. Give a rig TV episode a real file, or configure a
   provider that resolves, and watch a `.srt` appear.
2. **Step 6 — remove Subber from MediaMop.** `C:\Projects\MediaMop` is cloned
   (77 subber files under `apps/backend/src/mediamop/modules/subber/`). MediaMop
   uses **branch + PR with `--squash --admin`** and **does** run GitHub Actions —
   the opposite of Deluno on both counts.
3. **#321's remaining seven:** adaptive searching *per provider* (the backoff
   that landed is per title+language, which is not the same thing), content
   modification (Sub-Zero options), sync with score thresholds, Whisper,
   translation, post-processing, language equals, HI extensions.
4. **Manual search and blacklist** — DESIGN-002's "new, and worth it" list.
5. **#327** — the subtitle bar has no legend, and it is the only mark on a poster
   that does not.

### The one decision waiting for James

**Subtitle upgrades need a definition of "better".** DESIGN-002 already proposes
it — *must match this release* / *prefer this release* / *anything readable*,
with the consequence written underneath — and that is a decision to put to him
rather than a scoring model to invent. Until then the fetcher takes the first
usable subtitle from the highest-priority provider, preferring a plain track over
hearing-impaired and never taking a forced one.

## What the rig caught that no test would have

Three in one session, all in provider code that looked obviously correct:

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
