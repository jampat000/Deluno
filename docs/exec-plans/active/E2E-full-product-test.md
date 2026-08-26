# Deluno — full product end-to-end test

A live test of everything Deluno does, run against a real install on the simulation VM, set up the way a user would set it up.

**Why it exists.** Every unit and smoke test passed while the Processing stage was non-functional, the app rendered as raw source in a browser, and a download client could not be added using the form's own defaults. None of those are visible to a test that stubs the thing it is testing. This plan is the counterweight: one pass, on real software, talking to real software, with a person or agent driving the browser.

**How to use it.** Work top to bottom. Each step says what to do and what must be true afterwards. Record the result in the Outcome column — `pass`, or the issue number raised. A step that cannot be completed is a finding, not a step to skip.

**The rule that matters.** Set everything up **through the UI**. Configuring through the API hides real bugs and invents fake ones. The only permitted API use is reading state to verify an assertion.

---

## The rig

| What | Where | Sign in |
|---|---|---|
| Deluno | http://10.1.1.142:5099 | `admin` / `Deluno-Lab-2026!` |
| MediaMop | http://10.1.1.142:8788 | `admin` / `MediaMop-Lab-2026!` |
| qBittorrent | http://10.1.1.142:8080 | LAN subnet whitelisted |
| SABnzbd | http://10.1.1.142:8085 | API key from its config |
| Windows | 10.1.1.142 (RDP/WinRM) | `Administrator` / `Deluno-MM-Lab-2026!` |

All services run as scheduled tasks at startup and survive a reboot.

**The torznab indexer runs on the desktop, not the VM**, and is not a service. Start it before any acquisition step:

```bash
TORZNAB_BIND=0.0.0.0 TORZNAB_ADVERTISE=10.1.1.102 python torznab_seed.py
```

Folder topology — both apps must agree, and that is a user responsibility rather than a bug:

```
C:\Deluno\Downloads-Complete\Movies   qBittorrent category save path AND MediaMop Refiner watched folder
C:\Deluno\Refined\Movies              MediaMop Refiner output AND Deluno library clean-output path
C:\Deluno\Library\Movies              Deluno library root
C:\Deluno\Work\{Movies,TV}            Refiner work dirs (must not overlap each other)
```

Verify live in **real Chrome**. The in-app browser pane is not a substitute.

---

## Phase 0 — a genuinely clean install

| # | Do | Must be true | Outcome |
|---|---|---|---|
| 0.1 | Stop the Deluno scheduled task on the VM | The port stops answering | |
| 0.2 | Rename the data root aside (`data` → `data.before-e2e`) | The previous config is recoverable, not destroyed | |
| 0.3 | Copy the new build over the install directory | The binary is the revision under test | |
| 0.4 | Start the task, load `http://10.1.1.142:5099` in Chrome | The **app renders** — not its own source. This is the #291 regression | |
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
| 8.9 | Grab into the TV library via SABnzbd | Dispatch is attempted and its outcome is reported honestly | |
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
| 12.7 | Reboot the VM | All three services return by themselves; Deluno's state survives | |

## Phase 13 — screenshots

| # | Do | Must be true | Outcome |
|---|---|---|---|
| 13.1 | With the library populated, capture dashboard, movies, shows, queue, quality, indexers, activity | Post-redesign, real data, no credentials on screen | |
| 13.2 | Replace `screenshots/` and check the README renders | #269 closes | |

---

## What this plan cannot cover honestly

- **Usenet transfers.** SABnzbd can be connected, tested, categorised and dispatched to, but there is no usenet provider or real NZB on this rig, so a completed usenet download is out of scope. Everything up to and including the dispatch attempt is testable; the transfer is not.
- **The NAS.** `\\storage-city\Data\Media` is not reachable from the VM's service account, so libraries stay local to the VM.
- **Scale.** The 20,000-item invariant is a fixture concern, not something this rig demonstrates. Use `scripts/new-large-library-fixture.ps1`.

Say so when reporting, rather than letting an untested area read as tested.
