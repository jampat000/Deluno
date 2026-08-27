# Deluno — handover

You're picking up Deluno (`C:\Projects\Deluno`, github.com/jampat000/Deluno): a Windows .NET 10 + React 19 media-automation app replacing Radarr, Sonarr, Prowlarr, Huntarr, Cleanuparr, Recyclarr, Upgradarr, Trash Guides and Bazarr.

**Read `docs/PRODUCT_NORTH_STAR.md` first.** It records what each of those platforms actually does — read from their own sources, not from memory — and the five-question standing check every change answers before it is called done. Issue #194 used to be the reminder to do this; it is closed, because the check now outlives it.

Then `docs/exec-plans/active/HANDOVER-live-e2e-run.md` for the lab rig and the traps, and `DESIGN-001` through `DESIGN-005`.

`main` is at `e83d311`, working tree clean. **862 .NET tests, 107 web unit tests, 17 metadata-gateway tests**, Playwright 272 passed / 10 skipped.

## The bar, in James's words

Short answers, few questions, pictures over prose. Simplicity is the product. Repetition is a defect — he will spot it on screen before any test does. Measure, don't assert.

And the clause added this session, after being shown Deluno had 6 filter fields to Radarr's 33 while gaining an axis Radarr lacks:

> *"instead of being ahead we will still be behind"*

**A new axis does not excuse a smaller number on an old one.** Where a tool offers N of something, Deluno offers all N and then more.

He will correct you when you are wrong, sometimes bluntly. He is usually right — three times this session: Cleanuparr's scope, "hunt what's shown", and that Radarr's whole-library render is fine. When he corrects a premise, change the work, don't defend the reasoning.

## THE NEXT TASK

**[#322](https://github.com/jampat000/Deluno/issues/322)** is the epic and the running order. Start with **[#312](https://github.com/jampat000/Deluno/issues/312) — one continuous virtualised shelf with an A–Z rail.**

Paging the library was the wrong model, not the safe one. Reaching title 3,000 in a 6,000-movie library is thirty round trips; Ctrl+F finds one page. Radarr renders the lot in 3–5s behind a witty message and is better for it. The answer is neither: keep the keyset query, feed it into one continuous list in the background, virtualise the DOM. `library-grid.tsx` already imports `useVirtualizer`.

Then **#311** (TV series status / next airing / episode progress — the data already arrives), then the rest per #322.

## What this session did

- **Closed #194** by writing its bar into the north star with a standing check, and pointing `AGENTS.md` at it first.
- **#301 step 1 finished** — per-library subtitle languages, and reading what files already hold from three sources (fetched / sidecar / embedded). Verified live: Big Buck Bunny reads 2 of 2, full green.
- **Quality is granular again** — `shortQuality()` collapsed 21 tiers to "4K"/"1080p"/"720p", in two separate copies.
- **A filter system** — quality, genre, size, year, runtime, rating in SQL; nine sorts including size, quality-by-ladder-rank and **bitrate**, all index walks.
- **Search acts on what's on screen**, replacing "Hunt N missing" which counted one set and searched another.
- **The metadata broker** was sending 14 fields of ~30. Now sends certification, studio, network, collection, director, trailer, tagline, original language and status. **Deployed** — `npm --prefix services/metadata-gateway run deploy`.
- **Walked Radarr, Sonarr, Prowlarr and Bazarr live** and filed 17 issues (#306–#322).

### Three defects found by looking, not by tests

1. **The metadata blob has always been read with the wrong keys.** `metadata_json` is PascalCase; every reader asked camelCase with an exact-key lookup. Certification, collection, studio, language, path, tags, HDR format, release dates and the external ratings returned null for every title on every install, always — and it looked exactly like a provider that had sent nothing.
2. **A manual search request was never consumed.** Both libraries on the rig sat at `searchRequested = true` for days, re-entering the cycle every 30s. The code cleared a *local* flag and by doing so skipped the only branch that persists it.
3. **A cache key must carry the shape of what it cached.** The gateway deploy came back empty — KV was serving the old shape and would have for 12 hours. Deluno's own metadata cache had the same trap with a longer TTL.

## Non-negotiables

- Work directly on `main` and push for Deluno; MediaMop uses branch + PR with `--squash --admin`.
- Never run GitHub Actions for Deluno; do for MediaMop.
- Australian English.
- Stop `Deluno.Host` before any build. Kill stray `testhost` processes — they lock DLLs.
- Publish **SELF-CONTAINED** — the VM has no .NET runtime.
- Verify live rather than trusting a green suite.

## The rig — 10.1.1.142

- Deploy: `powershell -File scripts/publish-windows.ps1`, stop the `Deluno Host` scheduled task, copy `artifacts/publish/win-x64/Deluno.Host.exe` to `C:\Deluno\App\`, replace `C:\Deluno\App\wwwroot` with `apps/web/dist`, start the task.
- Front-end only: `npm run build:web` + copy `wwwroot` + hard reload. No publish.
- Any C# change needs a republish, or you verify a stale backend.
- Login `admin` / `Deluno-Lab-2026!`; VM admin password `Deluno-MM-Lab-2026!`.
- **`ffprobe.exe` is now at `C:\Deluno\App\`** and `ffmpeg.exe` at `C:\Deluno\Tools\`. Before this session the rig had never validated an import stream or read an embedded track. **Re-copy them if the VM is rebuilt.**
- Big Buck Bunny is a #301 fixture: real embedded `eng`/`spa` tracks plus a `.es.forced.srt` that must never count.

## James's live arr instances — read-only

`10.1.1.35` — Radarr `:8310`, Sonarr `:8989`, Prowlarr `:9696`, Bazarr `:6767`. Reachable from Chrome via the Claude in Chrome tools.

**Look, do not save.** Do not save a custom filter, do not trigger Interactive Search or "Search All" — those fire real queries at his trackers.

## Traps

- **The Write tool silently overwrites an existing file.** It clobbered `ui-adapters.test.ts` this session and 4 tests vanished; `git checkout HEAD -- <file>` got them back. Check whether a file exists before writing it.
- **Bash heredocs fail on some content** with `unexpected EOF`. Write a Python script into the scratchpad and run it with `python <path>` instead.
- **Screenshots of Radarr's 5,279-movie grid time out.** Use `get_page_text` and `read_page` — faster and they capture more.
- **The in-app browser pane's screenshot times out.** Use Claude in Chrome.
- Assets are content-hashed, so a stale bundle should not survive a deploy — check the served file anyway if something looks unfixed.
- Never run publish and Playwright at once.
- The gateway is a Cloudflare worker. `wrangler` is authenticated on this machine. **Bump `buildCacheKey`'s shape version and `SearchCacheShape` whenever `MetadataSearchResult` gains a field**, or warm caches serve the old shape.
- The rig's calendar still cannot be exercised: no film has a release date within its window.

## Architecture rules that keep being paid for

1. **ADR-001** — Movies and Series are parallel copies and the duplication is "actively reproducing". Anything new is shared from its first line through `MediaTableMap.For(MediaKind)`.
2. **Filters and sorts are indexed columns.** Wanted-state values get the V0016/V0017 treatment — cached on the title's row, maintained by a **trigger** so no write path can forget them. `Sorting_by_the_file_stays_an_index_walk` and `CatalogueSearchStateOnPageTests` fail if a page stops being a seek.
3. **The counts above the shelf count the rows on it.** Two queries, one answer.
4. **A page asking for nothing runs exactly the query it ran before the feature existed.**
5. **No second scheduler, no second lane, no second worker** (DESIGN-002 rule 3).
6. **Named typed fields, never a generic rule engine.** The last one was deleted in #302 because it could express filters nothing could answer.

## Also open

**#305** (the worker lane loop has no tests — that's what hid the batch-barrier bug), **#301** (Subber steps 2–6; #321 is the Bazarr delta), and **#78 / #81 / #82 / #129** — GA readiness, externally blocked.

## What every defect this session had in common

One rule written twice in places that could not check each other. Two copies of `shortQuality`. `DisplayOptions` declared twice. A blob written in one case and read in another. A consumption recorded in a local variable and persisted from a different branch. A cache key that did not know what shape it was caching.

**When you fix something, the next question is where else that shape lives.**
