# E2E full product test — run ledger, 4 September 2026

A live pass through `E2E-full-product-test.md` against the lab, on `main` at
`7a02736`, driven through the UI in real Chrome.

**Why this run.** The previous full pass was 26 August. Everything shipped since
— the Quality & Release redesign (#386), per-profile settings (#394), the five
installer PRs (#400–#404) and the shared composition root — had never been
walked end to end. The in-process suite is at 1,632 tests and the coverage
inventory still counts 208 API routes nothing touches, so this is the tier that
was most out of date.

## The rig, as it actually was

Found rather than assumed, and worth recording:

- The **`Deluno Host` scheduled task was disabled**, and the lab had been
  serving the *installed* Velopack build on 7879 since the #81 work. Two Delunos
  driving one qBittorrent is a confusing rig, so the installed one was stopped
  and the host re-enabled.
- 23 `App.rollback-*` directories, 8.4 GB. Left alone — 162 GB free.
- Torznab runs on the desktop, not the VM. Confirmed the VM can reach
  `10.1.1.102:9117` before starting, because a previous run lost time to that.

| | |
|---|---|
| Deluno | `10.1.1.142:5099`, clean install, readiness 9/9 |
| qBittorrent | `8080` |
| SABnzbd | `8085` |
| MediaMop | `8788` |
| Torznab | `10.1.1.102:9117`, reachable from the VM |

This walks `Deluno.Host`, which is the rig the plan is built around. The
installed tray build was walked separately on #81. Since #400/#401 the two share
one composition and one endpoint map, enforced by
`validate-agent-readiness.ps1`, so this is representative — but it is not
literally the shipped binary, and that limitation stands.

## What this run found

| | |
|---|---|
| [#407](https://github.com/jampat000/Deluno/issues/407) | A library saves with a root folder that does not exist, and nothing ever creates it. The check says "That folder does not exist yet"; Create saves it anyway. `Deluno.Libraries` never calls `Directory.CreateDirectory`, so the library is pointed at nothing permanently. |
| [#408](https://github.com/jampat000/Deluno/issues/408) | Path diagnostics report a non-existent folder as **Writable**. `Readable` probes the path, `Writable` probes the *parent* — one field name, two subjects, rendered as sibling chips. The negative case of the contract #294 fixed. |
| [#409](https://github.com/jampat000/Deluno/issues/409) | **Test connection** spins for 25 seconds, then reports an 8-second timeout and one attempt. Both health tests omit `MaxAttempts`, so they retry three times; the two user-initiated *actions* set `MaxAttempts: 1` explicitly. Reproduced twice, identically. |
| [#410](https://github.com/jampat000/Deluno/issues/410) | The category check confirms a category's **name** and never its save path. `deluno-tv` has an empty path and reports `ready`; its downloads land in `...\Downloads-Complete\deluno-tv`, not `...\TV`. `DownloadClientCategoryCheckResult` has no save-path field at all. |
| [#411](https://github.com/jampat000/Deluno/issues/411) | **SABnzbd reports Healthy with a wrong API key.** The health test probes `mode=version`, which SABnzbd answers to anyone; the client itself uses `mode=queue`, which 403s. The Test button asks a question that cannot fail. |
| [#412](https://github.com/jampat000/Deluno/issues/412) | Create forms use a placeholder that reads exactly like a filled-in default. Seen on Quality Profiles, Library Profiles and Tags, while the download client form holds a real value in the same visual treatment. |
| [#413](https://github.com/jampat000/Deluno/issues/413) | A wrong-scope API key is refused with an **empty 403** — zero-length body, no content type. Correct outcome, no explanation, on the surface used by scripts rather than people. |

**Three of those five are one shape**: a check that validates something *adjacent*
to the thing that matters. #408 reports `Writable` about the parent folder, #410
confirms a category's name but not its destination, #411 tests an endpoint that
does not need the credential under test. Worth one audit rather than three
patches.

## Phase 0 — a genuinely clean install

| # | Must be true | Outcome |
|---|---|---|
| 0.1 | The port stops answering | pass |
| 0.2 | The previous config is recoverable | pass — `Data.before-e2e-20260904-140829` |
| 0.3 | The binary is the revision under test | pass — 673 files verified against the publish output |
| 0.4 | The app renders, not its own source (#291) | pass — verified in real Chrome |
| 0.5 | A clean install asks to create an account | pass — `requiresSetup: true`, setup screen |

An unknown `/api` path returns **404**, not the app shell — the tray defect from
#401 is not present here.

## Phase 1 — first run

| # | Must be true | Outcome |
|---|---|---|
| 1.1 | Sign-in succeeds and lands on the dashboard | pass |
| 1.2 | States what is true, offers the next step, no invented activity | pass — "no library set up yet", "Nothing has happened yet", "Needs You: nothing right now" |
| 1.3 | Rungs in a sensible order, each says what it still needs | pass — Media Management → Library Profiles → Find & Download → Automation, each naming its gap |
| 1.4 | Cold deep link renders through the SPA fallback | pass — `/settings/libraries` |

## Phase 2 — libraries and media management

| # | Must be true | Outcome |
|---|---|---|
| 2.1 | Create form saves without editing anything first (#293) | pass — the empty form names **both** missing fields rather than sitting inert |
| 2.2 | Movies and TV stay separate | pass |
| 2.3 | The preview shows the real resulting path, not a template echo | pass — `Movies\Arrival (2016)`, and live: switching to Title + ID gave `Movies\Arrival (2016) [tt2543164]` |
| 2.4 | Refine before import accepted; the timeout is visible and editable | pass — "Wait up to: 6 hours", "If processing fails: Stop and ask me" |
| 2.5 | Two libraries, two workflows, no cross-talk | pass — Movies "Process, then import", TV "Import immediately" |
| 2.6 | Cleanup mode and empty-folder removal saved and reflected | pending |
| 2.7 | It says so plainly. **It does not save silently and fail later** | **fail — #407** |

The healthy-path folder check lit Exists · Directory · Readable · Writable with
"Deluno can read and write this folder", so #294's fix is holding on the path it
was written for. Navigating away from a dirty form raised a proper "Discard
unsaved changes?" guard.

## Phase 3 — quality

| # | Must be true | Outcome |
|---|---|---|
| 3.1 | Every quality has a size range that makes sense | pass — Low 0.1–3.5 GB, SD 0.3–8.5, 720p 0.8–10, 1080p 1.3–60, 2160p 4–130, ranks ascending 40→120 |
| 3.2 | Saves from the create form without a no-op edit (#293) | pass with a note, below |
| 3.3 | Order persists and drives preference | pass — reordered to WEB 1080p → HDTV 1080p → WEB 720p, survived a reload |
| 3.4 | A release outside the size range is rejected *and says why* | pending |
| 3.5 | Release preferences saved and visible on the profile | pending |
| 3.6 | Custom format from a guide preset, rules visible | pending |
| 3.7 | Custom format by hand | pending |
| 3.8 | Score both formats on the profile | pending |
| 3.9 | The dry run explains the match, not just a number | pending |
| 3.10 | Two profiles coexist and can be assigned separately | pending |

**The note on 3.2.** Every answer on the new-profile form arrives filled in, and
the form judges a real release live as you change them — the redesign works as
intended. One field is the exception: **Profile name is a placeholder that reads
exactly like a value** (`Movies / Standard`, greyed). Because everything else on
that form is a genuine default, the one field that is not looks like one, so
pressing Create costs a round trip on the very first profile anybody makes. It
names the gap rather than sitting inert, so #293 holds; it is a consistency
snag, not a defect.

What the form does well is worth recording too: with WEB 1080p / WEB 720p /
HDTV 1080p allowed, it judged `Dune.2021.2160p.UHD.BluRay.REMUX.HDR.TrueHD.
Atmos-FraMeSToR` live — *"Deluno would not take this — Remux 2160p is not one of
the qualities this profile allows"* — before the profile was even saved.

## Phase 5 — sources and download clients

| # | Must be true | Outcome |
|---|---|---|
| 5.1 | The create form names the missing API key rather than doing nothing (#293) | pass — named all three gaps: name, URL, API key |
| 5.2 | Reports reachable *and usable* | pass on the verdict — `healthy`, *"Reached 10.1.1.102 and received a valid Torznab response"*, 19 ms. **Fail on the wait — #409** |
| 5.3 | Movies-only scope excludes it from TV searches | pass — selecting Movies dropped the 5xxx categories, 15 selected became 8 |
| 5.4 | Saved; the sharing rule is one answer, not five dials | pass — one question, two answers, with the consequence spelled out |
| 5.5–5.11 | | not yet run |

A rig note worth keeping: the torznab seeder died partway through, and Deluno
called it `unreachable` — correctly. The failure was mine, not the product's,
and the honest report is what let me tell the difference quickly. It is now
started in a way that survives.

| 5.5 | **Open New client and press Add immediately** — a client is created from the defaults | pass — the #293 acceptance. The form arrives filled in and Add created a working qBittorrent client with no edits |
| 5.6 | Healthy, and it names the host it reached | pass — "Connected to qBittorrent at localhost:8080", 5 ms |
| 5.7 | The category check reports the truth about the real client | **fail — #410** |
| 5.8 | Wrong API key: degraded, and says the key is the problem | **fail — #411**, reports Healthy |
| 5.9 | Fix the key and re-test: healthy | pass — "Connected to SABnzbd at localhost:8085" |

## Phase 4 — library profiles

| # | Must be true | Outcome |
|---|---|---|
| 4.1 | Names what is missing rather than sitting inert (#293) | pass — same placeholder-as-value snag as 3.2, now seen twice |
| 4.2 | Quality profile and release preferences selectable and persist | pass |
| 4.3 | The library shows which profile governs it | pass — "Everyday movies · used by 1 library · Active" |

## Phase 6 — routing

| # | Must be true | Outcome |
|---|---|---|
| 6.x | Per-library routing is real | pass — Movies routed to Lab Torznab + qBittorrent, status **Ready**; TV still "Deluno can't search for this library" |

## Phase 7 — adding media

| # | Must be true | Outcome |
|---|---|---|
| 7.1 | Metadata resolves; the movie appears | pass — TMDb returned Big Buck Bunny (2008), `tt1254207`, cast and crew |
| 7.4 | Results listed with quality, size, decision | pass — 3 real candidates: 1080p **Best match**, 720p **Eligible**, 2160p **Rejected** |
| 7.5 | It says why in words, not only a number | pass |
| 7.6 | The rejection reason is specific | pass — *"WEB 2160p is not one of the qualities this profile allows (WEB 720p, HDTV 1080p, WEB 1080p)"*, over six lines of reasoning including seeders, size-in-range and codec |

## Phase 8 — acquisition

| # | Must be true | Outcome |
|---|---|---|
| 8.1 | Activity records a send that actually happened (#292) | pass — *"Sent Big.Buck.Bunny.2008.1080p.WEB-DL.x264-DELUNO via Lab Torznab to the download client"* |
| 8.2 | The torrent is really there, right category, right path | pass — in qBittorrent, category `deluno-movies`, `C:\Deluno\Downloads-Complete\Movies`, 100% |
| 8.4 | The item moves to Processing, MediaMop receives the hand-off | **half** — Deluno reports `processingCount: 1`, `waitingForProcessorCount: 1`, which is the correct and honest state. No refiner output appeared, so the hand-off half is unproven |
| 8.5–8.10 | | not run |

The search that produced those candidates is real and was seen arriving at the
indexer, carrying **movie-only categories** — which is 5.3 proven end to end
rather than in the form:

```
GET /api?t=search&q=Big%20Buck%20Bunny%202008&cat=2000,2010,2020,2030,2040,2045,2050,2060,2070&limit=100
```

## Where it stopped, and why

At 8.4, on a rig gap rather than a defect. MediaMop is running on 8788 but its
Refiner is not producing output for the completed download, so nothing arrives
in `Refined\Movies` for Deluno to import. The earlier entries in that folder are
from previous runs. Deluno's own reporting is correct throughout — it says it is
waiting for a processor, because it is.

Two rig facts worth carrying forward:

- **SABnzbd's config lives inside Deluno's data root** (`C:\Deluno\Data\sabnzbd\sabnzbd.ini`), so Phase 0.2's rename resets SABnzbd too. Either move it out or expect to reconfigure it each run.
- **Phase 0 wipes the data root but not the media folders.** `Library\Movies` still holds items from previous runs, including one folder named `Big Buck Bunny (2008) [{IMDb ID}]` with the token unexpanded. That is old, not from this run, and was not investigated.

## Phase 8, finished

MediaMop's refiner is configured correctly and scans every 300 seconds; the wait
was the schedule, not a fault. One manual scan later:

| # | Must be true | Outcome |
|---|---|---|
| 8.5 | The processor produces output Deluno can see | pass — `Refined\Movies\Big.Buck.Bunny.2008.1080p.WEB-DL.x264-DELUNO` |
| 8.6 | The refined file lands in the library with the naming you set | pass — `C:\Deluno\Library\Movies\Big Buck Bunny (2008)\Big Buck Bunny (2008).mkv` |
| 8.7 | `processingCount` and `waitingForProcessorCount` return to zero while the torrent still seeds — the **#280 acceptance** | pass — both zero, torrent `stalledUP` |

## Phase 11 — the rest of the product

| # | Must be true | Outcome |
|---|---|---|
| 11.1 | Saves from the create form (#293) | pass, with #412 |
| 11.2 | The preview shows real resulting paths | pass, and then some — source and destination in full, nine numbered reasoning steps, and three honest warnings: source not visible to Deluno, hardlink unlikely across filesystems, `D:\` differs from `C:\` |
| 11.5 | The test reports honestly | pass — closed port gave `dead-letter`, **`attempts: 3`**, *"the target machine actively refused it. (127.0.0.1:9999)"* |
| 11.6 | It says notifications are paused rather than lying about sending | pass — *"Notifications are paused. Turn on Send notifications to test this webhook."* |
| 11.3–11.4, 11.7–11.8 | | not run |

`attempts: 3` there is worth holding against #409, where the health test made
three attempts and reported one. The right behaviour already exists in the
product; the health tests are the outlier.

## Phase 12 — system

| # | Must be true | Outcome |
|---|---|---|
| 12.1 | Created once and shown once; a wrong scope is refused with a clear message | **half** — created once, secret absent when listed again, read `200`, write `403`, but the 403 is empty (**#413**) |
| 12.2 | Revoke it and re-use it: refused | pass — `401` |
| 12.3 | Take a backup and restore it | pass — tags 1 → 2 → **1 (Kids)** across a restart, five `.pre-restore` copies kept, readiness `200` in 4s. #403's fix holds on a real restore |
| 12.4 | Every check names what to do | pass in substance — 9 of 9 ready, so nothing to name |
| 12.5 | Reports the channel and current version truthfully | pass — *"This runtime is not a Velopack-managed Windows install. Update by installing a newer build package."* It does not pretend it can update itself |
| 12.6 | Navigation is monochrome; no nav element uses a status colour (#290) | pass — labels grey/white, active item an accent bar, count badges neutral. The only colour is an amber dot in the dedicated "Needs a look" panel, which is not navigation |

## Not a defect, worth recording

The dashboard reported **10 failed jobs** after the restore. They are the
leftover `Breaking.Bad.S01` season pack sitting in qBittorrent from a previous
run, which Deluno keeps trying to import into a TV library that has no matching
show. The message is exactly what it should be:

> Season-pack import is blocked: Choose the existing TV show before importing a
> season pack so every file can be matched to its canonical episode. The job
> reached its retry limit and moved to dead-letter.

Clear, actionable, and it gave up rather than looping for ever. That is
Phase 10.4's assertion met by accident.

## Phases 9, 10, 13

Not yet run. Phase 3.4–3.10 also outstanding: the size sliders save per tier
(WEB 1080p 1.5–25 GB, HDTV 1080p 1.3–14, WEB 720p 0.8–8) but the profile's
preview judges release *names*, so "a release outside the range is rejected and
says why" needs the Phase 8 path finished.
