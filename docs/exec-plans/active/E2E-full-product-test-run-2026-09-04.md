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

## Phases 4, 6–13

Not yet run. Phase 3.4–3.10 also outstanding: the size sliders save per tier
(WEB 1080p 1.5–25 GB, HDTV 1080p 1.3–14, WEB 720p 0.8–8) but the profile's
preview judges release *names*, so "a release outside the range is rejected and
says why" can only be proven with a real file in Phase 8.
