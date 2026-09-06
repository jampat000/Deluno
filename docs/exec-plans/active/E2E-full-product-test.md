# Deluno — full product end-to-end test

A live test of everything Deluno does, run against a real install on the simulation VM, set up the way a user would set it up.

**Why it exists.** Every unit and smoke test passed while the Processing stage was non-functional, the app rendered as raw source in a browser, and a download client could not be added using the form's own defaults. None of those are visible to a test that stubs the thing it is testing. This plan is the counterweight: one pass, on real software, talking to real software, with a person or agent driving the browser.

**How to use it.** Work top to bottom. Each step says what to do and what must be true afterwards. Record the result in the Outcome column — `pass`, or the issue number raised. A step that cannot be completed is a finding, not a step to skip.

**The rule that matters.** Set everything up **through the UI**. Configuring through the API hides real bugs and invents fake ones. The only permitted API use is reading state to verify an assertion.

---

## Run 1 — 26 August 2026

The first pass through this plan, on a wiped install. It reached the end of Phase 8 and stopped there deliberately: seven defects turned up on the way, and fixing and verifying them was worth more than ticking further rows.

**What the pipeline did.** Fresh install → 2 libraries → indexer → download client → routing → quality profile from a TRaSH template → add a film → search → grab → qBittorrent → hand-off → MediaMop → matched output → import → `Big Buck Bunny (2008)/Big Buck Bunny (2008).mkv`, with the film reporting **On disk · WEB 2160p · cutoff Met** and the Processing stage back to zero while the torrent kept seeding. Twice, with two different releases.

**Found and fixed, none of which any test could see:**

| | |
|---|---|
| [#297](https://github.com/jampat000/Deluno/issues/297) P0 | Every grab sent with a hard-coded category, so downloads never reached the folder the processor watches. The Movies/TV category fields and the routing category had never been read by anything. |
| [#298](https://github.com/jampat000/Deluno/issues/298) P0 | A refined import was filed under its release name with "(Unknown Year)" and never linked to the film, which stayed Missing — a re-download loop that also produced duplicate catalogue entries. |
| [#294](https://github.com/jampat000/Deluno/issues/294) | The folder check had never lit Readable or Writable for any path, and said nothing at all about a healthy folder. |
| [#295](https://github.com/jampat000/Deluno/issues/295) | A legacy-protocol client told you to change it and disabled the control that changes it, while displaying a protocol it did not have. |

Plus the four this run set out to close: [#280](https://github.com/jampat000/Deluno/issues/280), [#292](https://github.com/jampat000/Deluno/issues/292), [#293](https://github.com/jampat000/Deluno/issues/293), [#290](https://github.com/jampat000/Deluno/issues/290), [#291](https://github.com/jampat000/Deluno/issues/291), [#269](https://github.com/jampat000/Deluno/issues/269).

**Two of those were the same defect written twice.** #298 is #268 on the sibling import path; #294 is two halves of one contract that nobody had ever compared. Both survived because the lesson was learned in one place and not carried next door. When a fix lands, the next question is where else that shape lives.

**Setup facts the plan did not know:**

- **qBittorrent only applies a category's save path when Automatic Torrent Management is on.** With it off the category is set correctly and the file still lands in the global default folder. The rig needs `auto_tmm_enabled: true`. Deluno's **Check category** does not catch this, and cannot check the category that will actually be used when the routing override is blank, because it is gated on that field being non-empty.
- **Background automation is paused on a fresh install**, and queued jobs are held. `/api/health/ready` says so plainly — *"Background automation is paused; queued jobs are intentionally held."* Nothing moves past the queue until step 4 of the setup ladder is done. This is correct and well reported; it is just easy to miss.
- **MediaMop's Refiner writes output even when it decides no changes are needed**, into `<output>/<source leaf>/`, which is exactly the shape `FindCorrelatedProcessorOutputs` expects. It also empties the source folder, which will break seeding if that folder is the client's.

**Still to run:** Phases 9–12 — missing and upgrade cycles, recovery and cleanup, lists, notifications, tags, destination rules, API keys, backup and restore, reboot. SABnzbd is now installed and its deterministic NZB → NNTP/yEnc → native history → TV import path is proven in the 31 August run ledger; commercial-provider retention and availability remain outside this lab.

**Open follow-up:** the Movies card for the imported film shows a `DOWNLOADING` chip while its detail page says *On disk — imported and verified*; the torrent is still seeding, so queue state appears to leak into the library card. Header counts are correct.

---

## Run 2 — 5 September 2026 (partial)

Started after six pull requests landed in one day (#437–#442, DESIGN-007). Today's
build was published and deployed to the rig — 246 files, readiness 9/9 — and the
run stopped early on purpose: three defects turned up in the first hour and
writing them up was worth more than ticking rows.

**Verified live on the rig.** The failure and blocklist console renders with all
three sections on real hardware: the list, the seventeen failure rules grouped by
whose fault the failure was, and the schedules. Changing a rule, resetting it,
changing the file-check cadence, running the file check by hand and previewing a
recycle-bin empty all worked end to end against a real backend.

**Found, none of which any test could see:**

| | |
|---|---|
| #445 | `npm run ga:regression` never reached step two. Step one leaves MSBuild's persistent node servers alive; they inherit the handles `Start-Process -Wait` is waiting on. Step one finished at 18:50:17 and thirteen minutes later there was no `dotnet test` process — killing the nodes started step two within seconds. **The GA gate could not complete unattended and nothing said so.** |
| #445 | The dashboard announced "136 jobs have failed" over a queue holding **455**. Job statuses were counted inside `ListAsync(200)`, so every number saturated at two hundred — and the System screen, counting differently, said 455 on the same data. The soak's daily rule watches `jobs_failed` for an upward trend, so a saturating metric would report healthy through the failure the soak exists to catch. |
| #446 | **459 dead-letter import rows for one piece of work**, accumulated over seventeen hours, and the one thing the owner is told to do about it did nothing. The dashboard says open Activity and put them back in the queue; pressing that button left the count unchanged, because retry promotes one row per dedupe key and all 459 shared one. Fixed in #445: enqueueing work whose row has already given up revives that row instead of adding another, so there is one row per piece of work and retry can promote all of them. |

**The same lesson as Run 1, twice.** The regression-gate hang is the defect
`ci-check.ps1` had already fixed, one file over, with a comment describing it.
When a fix lands, the next question is where else that shape lives.

**Still to run:** Phases 9–13 were not worked through. They remain the untouched
part of this plan, now on a build six pull requests newer than the one Run 1
walked.

**Rig note.** `SABnzbd E2E Interactive` would not start over WinRM, so the usenet
path was unavailable this run. qBittorrent, MediaMop and the torznab seeder were
all up. *Fixed on 6 September — see the readiness section below; it did not need
an interactive session.*

---

## Run 3 readiness — 6 September 2026

**The core loop closes.** Until it did, there was nothing to run phases 9–13
against: every phase after 8 assumes a library with something in it, and the rig
had an empty library.

Six defects stood between "a title is wanted" and "a file is in the library",
each one a layer confidently reporting something untrue and the layer above
believing it:

| | |
|---|---|
| #453 | qBittorrent adds asynchronously. The adapter compared the hash list before and after the add, so **every new torrent grab was reported as a failure** and the infohash was never recorded. The source of the rest of this chain. |
| #451 | `NormalizeAction` did not list `forget`, killing every path that asks a client to forget a release. Nothing caught it because every caller mocks the service that was refusing. |
| #450 | The force said "there was nothing to clear" about the very obstacle it was offered for. |
| #455 | A re-download inherited the previous attempt's hand-off outcome and was reported `imported` over an empty library. |
| #445 | Failed jobs counted from a 200-row page — 136 reported against 455 real — so the GA regression gate never reached step two. |
| #448 | Retry left out the pile it had promoted from. |
| #454 | A completed import reserved its refined folder for ever, so a release could be imported exactly once. The rule that fixes this already existed one loop above, with a comment naming the failure. |

Rig state on `86d0fa98`:

```
grab          : dispatch=sent queueItem='1800621d8a6a3a60c5280a172c3f6cf803bac2f5'
client        : status=imported
downloaded    : 0 in Downloads-Complete\Movies
refined       : 1 in Refined\Movies
library       : 1 in Library\Movies
catalogue     : hasFile=True wanted=covered
import status : 'imported'
```

```
C:\Deluno\Library\Movies\Big Buck Bunny (2008)\Big Buck Bunny (2008).mkv   61,878,609 bytes
```

Named from the catalogue rather than the release string, which is the other half
of what the refine-before-import path was getting wrong.

**The lesson, a third time.** Six of the seven were a rule that already existed
somewhere else in the codebase, sometimes with a comment describing the exact
failure it was there to prevent. After a fix lands, the next question is where
else that shape lives.

**The usenet path is available again, and this time it is a script.** Run 2
recorded "SABnzbd will not start over WinRM, it needs an interactive session" as
a fact about the rig, and wrote the whole usenet half of this plan off because of
it. It was half true and it was not the reason: SABnzbd checks its own session id
before parsing any argument and always concludes it is a Windows service, so the
answer was to make it one. It now starts with the machine like its three
neighbours.

Underneath that was the part nobody had looked at: SABnzbd's configuration had
gone — no news server, no category folder, relative paths, and an API key Deluno
no longer agreed with. Deluno was saying so clearly and accurately ("SABnzbd
rejected authentication with 403", next action "check the credential or API
key"); nobody was reading it. `scripts/lab/provision-usenet.ps1` puts all of it
back and then proves it, moving a genuine yEnc article over NNTP and comparing
the decoded SHA-256 against the source.

**Ready for:** phases 9.4–9.9 and 10–13, none of which have ever been run, on
both the torrent and the usenet path.

---

## The rig

**The Hyper-V VM is retired.** It was destroyed on 6 September 2026 along with
Hyper-V itself. Six weeks of hand-building had made it unable to answer the one
question phases 0 and 1 ask — its `C:\Deluno` held forty-eight directories,
twenty-eight of them `App.rollback-*` — and a machine nobody can rebuild had
already lost a GA gate once when its SABnzbd configuration quietly vanished.

The replacement is a physical server with a NAS-hosted library. Its address and
credentials live in [`scripts/lab/rig.json`](../../../scripts/lab/rig.json), not
in this document, so moving the rig again is a value change rather than an
archaeology exercise.

| What | Where | Sign in |
|---|---|---|
| Deluno | `<rig>:5099` | `admin` / `Deluno-Lab-2026!` |
| MediaMop | `<rig>:8788` | `admin` / `MediaMop-Lab-2026!` |
| qBittorrent | `<rig>:8080` | LAN subnet whitelisted |
| SABnzbd | `<rig>:8085` | API key from its config |
| Windows | `<rig>` (RDP/WinRM) | `Administrator` |

Build it:

```powershell
./scripts/lab/provision-rig.ps1 -ComputerName <ip> -Password <admin> `
    -ServiceAccount deluno -ServiceAccountPassword <pw> `
    -LibraryPath '\<nas>\<share>' -NasUser <u> -NasPassword <p>
```

That installs the pinned software set, the folder topology, the client
configuration and the services — and **stops before Deluno's first run**,
because phase 0.5 below is "a clean install asks to create an account, not to
sign in". Provisioning that would destroy the first thing this plan tests.

All four services start without a person and come back after a reboot — Deluno,
qBittorrent and MediaMop as scheduled tasks, SABnzbd as a real Windows service,
because it refuses to run any other way (see below). Hold them to that:

```powershell
./scripts/lab/ensure-rig-services.ps1            # -ReportOnly to look first
```

**On a NAS library, the services do not run as SYSTEM.** A SYSTEM process
authenticates to SMB as the machine account rather than as a user, so a
workgroup NAS refuses it — that, not networking, is why the retired VM could
never reach a share. They run as a dedicated account with the share credential
stored in *that account's* vault, and provisioning probes the share **as that
account** rather than from the provisioning session, because a WinRM session has
no delegatable network credentials and would fail either way.

**Two fixtures run on the developer desktop, not the rig**, because there is no
Python on the rig. Neither is a service. Start them before any acquisition step:

```bash
TORZNAB_BIND=0.0.0.0 TORZNAB_ADVERTISE=<desktop-ip> python scripts/lab/torznab_seed.py
```

```powershell
./scripts/lab/provision-usenet.ps1 -Verify       # nntp/nzb fixture + SABnzbd + Deluno
```

`provision-usenet.ps1` is idempotent and skips whatever is already right. With
`-Verify` it pushes the fixture NZB through SABnzbd, waits for it to complete,
and compares the decoded SHA-256 against the source before cleaning up after
itself, so "the usenet path works" is a thing the rig demonstrates rather than a
thing this document asserts.

**Why SABnzbd is a service and not a task.** It checks its own session id at
startup, before parsing any argument:

```python
if hasattr(sys, "frozen") and win32ts.ProcessIdToSessionId(...) == 0:
    servicemanager.StartServiceCtrlDispatcher()
```

Every process launched over WinRM is in session 0, and so is every SYSTEM
scheduled task, so SABnzbd always decides it is a Windows service — which is
also why `SABnzbd.exe install` could not be run remotely either. As a real
service the dispatcher connects and its options come from the `CommandLine`
value under its own service key.

Folder topology — both apps must agree, and that is a user responsibility rather
than a bug. `provision-rig.ps1` creates all of it:

```
C:\Deluno\Downloads-Complete\{Movies,TV}   qBittorrent category save paths AND MediaMop's watched folders
C:\Deluno\Refined\{Movies,TV}              MediaMop output AND Deluno's library clean-output path
C:\Deluno\Work\{Movies,TV}                 Refiner work dirs (must not overlap each other)
\<nas>\<share>                            Deluno library root
```

The library moving to a share is new, and it is the interesting part: an import
across a network boundary is a copy rather than a rename, which is what most
people actually run and what the retired VM could never test. Emby points at the
same share independently, so whether Deluno named and nested a file correctly is
answered by something other than reading the path.

One correction carried over from the old rig: qBittorrent's `deluno-tv` category
had an **empty** save path while `deluno-movies` named a subfolder, so TV landed
in the root of `Downloads-Complete` and left a stray `deluno-tv` folder behind.
Provisioning names both.

Verify live in **real Chrome**. The in-app browser pane is not a substitute.

---

## Phase 0 — a genuinely clean install

| # | Do | Must be true | Outcome |
|---|---|---|---|
| 0.1 | Provision the server from bare Windows | `provision-rig.ps1` completes every stage, and says what it did | |
| 0.2 | Confirm Deluno's data root is empty | Provisioning stopped before first run; there is no config to preserve because there has never been one | |
| 0.3 | Deploy the build under test | The binary is the revision under test, and `deploy-lab.ps1` proves readiness before returning | |
| 0.4 | Load the rig in Chrome | The **app renders** — not its own source. This is the #291 regression | |
| 0.5 | Confirm the first-run screen appears | A clean install asks to create an account, not to sign in | |

## Phase 1 — first run

| # | Do | Must be true | Outcome |
|---|---|---|---|
| 1.1 | Create the account through the browser | Sign-in succeeds and lands on the dashboard | |
| 1.2 | Read the dashboard of an empty install | It states what is true and offers the next step. No invented activity | |
| 1.3 | Read the setup ladder | Rungs are in a sensible order and each says what it still needs | |
| 1.4 | Reload the page on a deep link (`/settings/libraries`) | Renders. Cold deep links go through the SPA fallback | |

## Phase 2 — libraries and media management

| # | Do | Must be true | Outcome |
|---|---|---|---|
| 2.1 | Create a Movies library at `C:\Deluno\Library\Movies` | Saves. **Create form saves without editing anything first** (#293) | |
| 2.2 | Create a TV library | Saves. Movies and TV stay separate | |
| 2.3 | Set naming for both | The preview shows the real resulting path, not a template echo | |
| 2.4 | Set the Movies library to **refine before import**, output `C:\Deluno\Refined\Movies` | The workflow is accepted; the timeout ("Wait up to") is visible and editable | |
| 2.5 | Leave TV on direct import | Two libraries, two workflows, no cross-talk | |
| 2.6 | Set cleanup mode and empty-folder removal | Saved and reflected in the library list | |
| 2.7 | Point a library at a path that does not exist | It says so plainly. It does not save silently and fail later | |

## Phase 3 — quality

| # | Do | Must be true | Outcome |
|---|---|---|---|
| 3.1 | Open Quality and read the built-in definitions | Every quality has a size range that makes sense | |
| 3.2 | Create a quality profile with a cutoff | Saves from the create form without a no-op edit (#293) | |
| 3.3 | Reorder the quality tiers | Order persists and drives preference | |
| 3.4 | Set a **size rule** (min/max/preferred) on a quality | Saved. A release outside the range is later rejected *and says why* | |
| 3.5 | Set **release preferences** (preferred/must-contain/must-not-contain) | Saved and visible on the profile | |
| 3.6 | Create a **custom format** from a guide preset | Saves, and the guide's rules are visible rather than hidden | |
| 3.7 | Create a custom format by hand with a matching criterion | Saves | |
| 3.8 | Score both formats on the profile | Score appears in the decision later | |
| 3.9 | Run the custom-format **dry run / preview** against a release name | It explains the match, not just a number | |
| 3.10 | Create a second profile (e.g. 1080p-max) | Two profiles coexist and can be assigned separately | |

## Phase 4 — library profiles

| # | Do | Must be true | Outcome |
|---|---|---|---|
| 4.1 | Create a library profile from the create form | Saves. If incomplete, it **names what is missing** rather than sitting inert (#293) | |
| 4.2 | Attach a quality profile and release preferences to it | Both are selectable and persist | |
| 4.3 | Assign the profile to the Movies library | The library shows which profile governs it | |
| 4.4 | Assign a different profile to TV | Per-library governance is real, not global | |
| 4.5 | Change a profile and observe the libraries using it | The change reaches every attached library, and the UI says which ones | |

## Phase 5 — sources and download clients

| # | Do | Must be true | Outcome |
|---|---|---|---|
| 5.1 | Add the torznab indexer (`http://10.1.1.102:9117/...`) | The create form names the missing API key rather than doing nothing (#293) | |
| 5.2 | Test the indexer | Reports reachable *and usable* | |
| 5.3 | Set its categories and media scope | Movies-only scope excludes it from TV searches | |
| 5.4 | Set a request interval and sharing rule | Saved; the sharing rule is one answer, not five dials | |
| 5.5 | **Open New client and press Add immediately** | A client is created from the defaults. This is the #293 acceptance | |
| 5.6 | Point it at qBittorrent, set credentials, test | Healthy, and it names the host it reached | |
| 5.7 | Check the categories exist in qBittorrent | The category check reports the truth about the real client | |
| 5.8 | Install and add **SABnzbd**; test with a wrong API key | Degraded, and says the key is the problem | |
| 5.9 | Fix the key and re-test | Healthy | |
| 5.10 | Try to save a client with an unsupported protocol via the API | Rejected at write time, naming the supported values (#292) | |
| 5.11 | Add a client pointing at a closed port | Unreachable — a transport failure, distinguished from a bad answer | |

## Phase 6 — routing

| # | Do | Must be true | Outcome |
|---|---|---|---|
| 6.1 | Route the Movies library to the torznab source and qBittorrent | Saved | |
| 6.2 | Route TV to SABnzbd | A library can prefer a different client | |
| 6.3 | Set per-client categories in routing | Categories reach the client on grab | |
| 6.4 | Remove a source from a library and search | That library no longer queries it, and says so | |

## Phase 7 — adding media

| # | Do | Must be true | Outcome |
|---|---|---|---|
| 7.1 | Add a movie by search | Metadata resolves; the movie appears in the library | |
| 7.2 | Add a TV show, choose monitoring | Seasons and episodes appear with the monitoring you chose | |
| 7.3 | Add with **search on add** enabled | A search runs immediately and is visible in Activity | |
| 7.4 | Run a **manual search** on the movie | Results are listed with quality, size, seeders, score | |
| 7.5 | Read one result's decision explanation | It says why in words, not only as a number | |
| 7.6 | Find a release the profile rejects | The rejection reason is specific: below cutoff, size rule, must-not-contain | |
| 7.7 | Bulk-select several movies and act | The bulk action reports per-item outcomes | |

## Phase 8 — acquisition, refinement and import

Start the torznab indexer first.

| # | Do | Must be true | Outcome |
|---|---|---|---|
| 8.1 | Grab a release manually | Activity records a **send that actually happened** (#292) | |
| 8.2 | Watch qBittorrent | The torrent is really there, in the right category, saving to the right path | |
| 8.3 | Watch the Deluno queue | Progress and speed are the client's real readings | |
| 8.4 | Let it complete | The item moves to Processing, and MediaMop receives the hand-off | |
| 8.5 | Watch MediaMop remux and call back | Deluno records the callback | |
| 8.6 | Watch the import | The refined file lands in `C:\Deluno\Library\Movies` with the naming you set | |
| 8.7 | **Check the pipeline counts after import** | `processingCount` and `waitingForProcessorCount` return to zero while the torrent is still seeding. This is the #280 acceptance | |
| 8.8 | Check the dashboard stage strip | Processing empties. Importing is never a negative number | |
| 8.9 | Grab into the TV library via SABnzbd | Dispatch is attempted and its outcome is reported honestly | Pass — real generated NZB/yEnc transfers imported S01E01 and multi-episode S01E04/E05; native history, shared destination identity, restart durability, and one-job dedupe were verified. |
| 8.10 | Kill the processor mid-flight, wait past the timeout | Deluno reports the timeout somewhere a user sees it, and the failure mode you chose is what happens | |

## Phase 9 — missing, upgrades and automation

| # | Do | Must be true | Outcome |
|---|---|---|---|
| 9.1 | Open Wanted / missing for both media types | Counts match the library's real state | |
| 9.2 | Run a **missing search** cycle by hand | It runs, reports per-item outcomes, and is visible in Activity | |
| 9.3 | Configure a recurring missing search | Schedule saves and the next run time is shown | |
| 9.4 | Lower a profile cutoff so an owned item is now upgradeable | The item appears in Upgrades, with the reason | |
| 9.5 | Run an **upgrade search** | A better release is chosen and the replacement is explained | |
| 9.6 | Let an upgrade import | The old file is handled per the cleanup policy, not orphaned | |
| 9.7 | Confirm sharing obligations are respected | A private-source item is not removed before its rule is met | |
| 9.8 | Set search cycle limits (max items, retry delay) | Honoured — the run stops where you said | |
| 9.9 | Pause automation | No cycles run while paused, and the UI says why nothing is happening | |

## Phase 10 — recovery and cleanup

| # | Do | Must be true | Outcome |
|---|---|---|---|
| 10.1 | Force a stalled download | Health flags it with a strike count | |
| 10.2 | Let it exceed the strike threshold | The configured remediation runs, and refuses to grab the same bad release again | |
| 10.3 | Import a sample-sized file | Classified as a sample, with a safe action offered | |
| 10.4 | Import an unmatched file | Classified as needs-review, with a manual import path | |
| 10.5 | Use manual import to place it | It goes through the same resolver and naming | |
| 10.6 | Check queue removal is opt-in | An external client's queue entry is not removed without explicit permission | |

## Phase 11 — the rest of the product

| # | Do | Must be true | Outcome |
|---|---|---|---|
| 11.1 | Create a tag and apply it | Saves from the create form (#293); filtering by it works | |
| 11.2 | Create a destination rule and preview it | The preview shows real resulting paths | |
| 11.3 | Add an import list, preview it | Preview does not add or search anything | |
| 11.4 | Selectively approve previewed titles | Only what you approved is added | |
| 11.5 | Add a notification webhook, send a test | Saves from the create form; the test reports honestly | |
| 11.6 | Turn notifications off, test again | It says notifications are paused rather than lying about sending | |
| 11.7 | Set metadata provider options | Saved; provider credentials never appear in the UI or API | |
| 11.8 | Run the migration assistant against an arr config | Imports what it can and reports what it could not | |

## Phase 12 — system

| # | Do | Must be true | Outcome |
|---|---|---|---|
| 12.1 | Create a scoped API key | Created once and shown once; a wrong scope is refused with a clear message | |
| 12.2 | Revoke it and re-use it | Refused | |
| 12.3 | Take a backup and restore it | Restores to the same state | |
| 12.4 | Read System health | Every check names what to do, not just that something is wrong | |
| 12.5 | Read the update screen | Reports the channel and the current version truthfully | |
| 12.6 | Check both themes and mobile width | Navigation is monochrome; no nav element uses a status colour (#290) | |
| 12.7 | Reboot the VM | All four services return by themselves; Deluno's state survives | Pass (6 September) — cold restart, nothing started by hand: 5099, 8080, 8085 and 8788 all answering, SABnzbd's news server and category intact, Deluno's library and catalogue unchanged. |

## Phase 13 — screenshots

| # | Do | Must be true | Outcome |
|---|---|---|---|
| 13.1 | With the library populated, capture dashboard, movies, shows, queue, quality, indexers, activity | Post-redesign, real data, no credentials on screen | |
| 13.2 | Replace `screenshots/` and check the README renders | #269 closes | |

---

## What this plan cannot cover honestly

- **Commercial Usenet-provider behaviour.** The rig now proves a completed SABnzbd transfer and Deluno import with a deterministic local NZB/NNTP/yEnc media fixture. It does not prove an external provider's authentication, propagation, retention, takedowns, throttling, or article availability.
- **The NAS.** `\\storage-city\Data\Media` is not reachable from the VM's service account, so libraries stay local to the VM.
- **Scale.** The 20,000-item invariant is a fixture concern, not something this rig demonstrates. Use `scripts/new-large-library-fixture.ps1`.

Say so when reporting, rather than letting an untested area read as tested.
