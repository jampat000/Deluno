# Deluno — handover

You're picking up Deluno (`C:\Projects\Deluno`, github.com/jampat000/Deluno): a Windows .NET 10 + React 19 media-automation app replacing Radarr/Sonarr/Prowlarr/Huntarr/Cleanuparr/Recyclarr/Upgradarr/Trash Guides. Issue [#194](https://github.com/jampat000/Deluno/issues/194) is the product bar: do everything the arr-suite does, better and **simpler**.

Working tree clean, `main` at `f7bf9e6`. **All gates green and actually run**: 714 .NET tests, 45 web unit tests, `ci:check` 7/7, and the Playwright smoke suite 260 passed / 0 failed across desktop and mobile.

## Standing rules from James — do not deviate

1. Work directly on `main`. No feature branches. Commit and push to main.
2. Never run GitHub Actions — disabled to avoid cost. Local gates only. (`gh` CLI for issues/PRs is fine.)
3. Verify live in real Chrome via `mcp__claude-in-chrome__*` — not code inspection, not the in-app browser pane.
4. Australian English in all user-facing copy.
5. Preserve the 20,000+ item scale invariant — never load the whole catalogue into memory.
6. **Stop `Deluno.Host` before any build** — it locks the DLLs. You will hit this repeatedly.
7. Add tests for contract, persistence, routing, status or schema changes.
8. Use `rg` for search.

**He wants short answers.** Use `AskUserQuestion` with short options rather than walls of prose. When he says a design point is unclear, that means the writing was too abstract — rewrite it in plain words, not more words.

## Running it

Start the host from **PowerShell**, not Git Bash (see the traps):

```powershell
$env:Storage__DataRoot = 'C:\Projects\Deluno\.deluno\data'
Start-Process -FilePath "dotnet" -ArgumentList 'C:\Projects\Deluno\src\Deluno.Host\bin\Debug\net10.0\Deluno.Host.dll' -RedirectStandardOutput 'C:\Users\User\AppData\Local\Temp\claude\host.log' -RedirectStandardError 'C:\Users\User\AppData\Local\Temp\claude\host.err.log'
```

Then check the log says `storage initialized at C:\Projects\Deluno\.deluno\data` before believing anything the browser shows you.

Vite: `npx vite --host 127.0.0.1 --port 5173` from `apps/web` via the **Bash** tool with `run_in_background: true` (it dies when a PowerShell call ends).

Chrome is signed in on `http://127.0.0.1:5173`. **His password is private — you cannot log in.** When the session expires, ask him to click Sign in and do other work meanwhile.

## Gates

```
dotnet test Deluno.slnx --configuration Release
```

```
npm run ci:check
```

Plus `npx vitest run` in `apps/web` and `npm run test:web` from the repo root. `npx tsc -b` is incremental and skips new files — `npm run build:web` is authoritative.

Run them. **Never while `Deluno.Host` is up** — `ci:check` builds the whole solution, and the smoke suite starts a *competing* Deluno.Host on 5199 and a web server on 5174.

## The live test rig

Real end-to-end. Not mocks — real qBittorrent doing real transfers with real hash checks.

- **qBittorrent v5.2.1** on `127.0.0.1:8080`, `WebUI\LocalHostAuth=false`. Original config backed up at `%APPDATA%\qBittorrent\qBittorrent.ini.deluno-e2e-backup`.
- **Torznab feed + webseed host** on 9117: `torznab_seed.py`, now copied into this session's scratchpad. Run with `python -u torznab_seed.py`. It serves **Big Buck Bunny, Sintel and Breaking Bad S01E01/S01E02 only** — Tears of Steel and Elephants Dream are in the library but have no releases, so they stay permanently missing. That is the rig, not a bug.
- Test media at `C:\Deluno\e2e`. Library roots `C:\Deluno\Movies` and `C:\Deluno\TV Shows`. Downloads land in `C:\Deluno\Downloads-Complete`.
- All four ports must listen: 5099 / 5173 / 8080 / 9117.
- **The Movies library now has cleanup set to "Remove after import"** — deliberately, because that is the setting that used to be dangerous and is now safe. Leave it on; it is the interesting state.
- qBittorrent holds three seeding torrents whose sharing rule has not expired. That is what makes the dashboard's Sharing stage worth looking at.

**There is no peer on this rig**, so upload always reads 0.0 MB/s. That is correct, not a bug. A second qBittorrent instance was tried and abandoned — its WebUI would not bind, and adding a real swarm to James's machine is not something to do without asking.

## What shipped this session — nine issues closed

**[#287](https://github.com/jampat000/Deluno/issues/287) cleanup no longer breaks seeding.** `remove-source-after-import` deleted the completed file directly and told nobody, which errors the torrent and stops seeding. Two settings could delete the same file on different schedules; each now has a domain. The library's cleanup covers files Deluno found on disk; anything it downloaded through a search source belongs to the sharing rule, which removes it *through* the client. `DownloadProtocols.HasSharingPhase` makes that judgement in one place. Transmission, Deluge and uTorrent gained `delete-with-data`.

**[#289](https://github.com/jampat000/Deluno/issues/289) + [#276](https://github.com/jampat000/Deluno/issues/276) one speed surface.** The hero's throughput wave is gone; `LiveWave` deleted. One card carries the stored shape with the live reading as its headline, in both directions, and always states a reading rather than the word "Idle". Upload runs the whole way down: every torrent client reports it per item, the summary totals it, the sampler stores it (`upload_mbps`, jobs migration v14).

**[#272](https://github.com/jampat000/Deluno/issues/272) machine telemetry.** One strip at the foot of System Pulse: CPU, memory, Deluno's own disk I/O, and whole-volume load. `MachineProbe` (Infrastructure/Observability) reads it with plain .NET and one kernel32 call — no performance counters, no WMI. Jobs migration v15, sampled once a minute by `MachineTelemetrySampler`, served by `GET /api/monitoring/machine` and on the monitoring snapshot.

**[#273](https://github.com/jampat000/Deluno/issues/273) transfers move on data.** `DownloadProgressPublisher` reports the client's own readings under the client's own queue-item id. The frontend overlays them rather than refetching. The three-second stopgap poll and `pipeline-activity.ts` are gone.

**[#274](https://github.com/jampat000/Deluno/issues/274) realtime audit.** Every surface checked against a running instance. Found the mirror image of the pipeline bug: the search planner announced `AutomationState` for every library it *looked at*, so an idle dashboard refetched three queries every thirty seconds. Six fetches in eighty seconds became one in a hundred and eight.

**[#281](https://github.com/jampat000/Deluno/issues/281) the `useQueries` cliff** is gone — twenty-two individual `useQuery` hooks.

**[#278](https://github.com/jampat000/Deluno/issues/278) mobile.** Looked at five pages on a real Pixel 7. `Needs you` now sits above `System pulse` below the breakpoint. `tests/smoke/mobile-review.spec.ts` captures each page full-length and asserts no sideways scroll and the stacked order.

**[#279](https://github.com/jampat000/Deluno/issues/279) closed on James's call** — the lifecycle order is a good default and nobody has asked to change it.

Three bugs turned up that nobody was looking for: a second summariser that would have reported zero upload however fast you were seeding; `DispatchCatalogueLink` never carrying `library_id`, so a hardlinked download was charged full price on the dashboard; and the automation churn above.

## What's open

**[#269](https://github.com/jampat000/Deluno/issues/269) update the GitHub README.** James chose screenshots of his **real instance**. The blocker: capturing them needs either his password (for Playwright) or his browser session token, and moving that token out of the browser is correctly blocked. The offer to make him is a small Playwright capture script that reads `DELUNO_E2E_USERNAME` / `DELUNO_E2E_PASSWORD` from the environment — he sets them, runs one command, and gets seven 1920×1080 PNGs into `screenshots/`; the password never reaches you. The description rewrite is not blocked: the current "What it does" list predates sharing and reclaim, machine telemetry, and the honesty work.

**[#280](https://github.com/jampat000/Deluno/issues/280) verify the Processing stage.** James does not use FileFlows any more — he is testing **MediaMop** as its replacement and wants Deluno connected to his existing instance.

What is established: MediaMop **2.3.11** is live and healthy at `http://app-server:8788` (10.1.1.35), data at `\\app-server\c$\ProgramData\MediaMop`, port in `current-port.txt`. It exposes `/openapi.json` with 86 paths including a `refiner` module (`/api/v1/refiner/jobs/watched-folder-remux-scan-dispatch/enqueue`, `/api/v1/refiner/path-settings`). **Its API is session-authenticated with CSRF, so you cannot call it.**

What that means: MediaMop has no endpoint that accepts Deluno's handoff payload, so Deluno's **webhook** mode will not work without work on one side. The realistic integration is Deluno's **watched-output-folder** mode, and for that you need two things from James:

1. MediaMop's refiner **input/watched folder** — where Deluno should put a finished download.
2. MediaMop's refiner **output folder** — where MediaMop writes the cleaned file for Deluno to watch.

Both need to be reachable from *this* machine (10.1.1.102) as well as app-server, so they are probably UNC paths or a share. Ask him for those two paths rather than digging through his 160 MB production SQLite.

**Blocked externally:** [#78](https://github.com/jampat000/Deluno/issues/78), [#81](https://github.com/jampat000/Deluno/issues/81), [#82](https://github.com/jampat000/Deluno/issues/82), [#129](https://github.com/jampat000/Deluno/issues/129) — installer validation on clean Windows environments, a 14-day soak, and code signing. None can be done from here.

**[#194](https://github.com/jampat000/Deluno/issues/194)** is the standing product bar and stays open.

## Traps — save yourself the time

- **Git Bash mangles backslashes.** In `python - <<'PY'` heredocs, `r"C:\\x"` arrives as `C:\x` — build paths with `chr(92)` or use the Edit tool. It bites the *host command* too: `Storage__DataRoot=C:\\Projects\\Deluno\\.deluno\\data dotnet …` arrives as `C:ProjectsDeluno.delunodata`, and the host quietly builds a fresh empty install there and shows you the first-run setup screen — which reads exactly like your database being gone. Use PowerShell's `$env:` form.
- **A Python heredoc containing an apostrophe inside a `'''` block can break the bash parser.** If `python - <<'PY'` dies with "unexpected EOF", write the script to a file with the Write tool and run it.
- **`ByValTStr` without an explicit `CharSet` marshals ANSI.** That made `DISK_PERFORMANCE` eight bytes short and `DeviceIoControl` answered `ERROR_INVALID_PARAMETER`, which reads exactly like an unsupported call. Two hours. There is a comment on it in `MachineProbe`.
- **A minimal-API GET whose parameter the host has not registered is inferred as a *body* parameter**, and that does not fail on the route — it fails the whole route table. Seven unrelated API tests went red. Use `[FromServices]`.
- **Don't navigate Chrome with a dirty form.** `useUnsavedChanges` raises a native "Leave site?" dialog CDP cannot dismiss; the renderer freezes and you must ask James to click it.
- **`performance.getEntriesByType('resource')` stops recording at 250 entries.** Call `performance.clearResourceTimings()` before measuring.
- **`document.visibilityState === 'hidden'`** makes TanStack stop polling entirely. Check visibility before concluding a surface is dead.
- **`innerText` reflects CSS `text-transform` in Chrome**, so a card titled "Needs you" comes back as "Needs You". Match case-insensitively.
- **CDP `Runtime.evaluate` times out at 45s.** Don't `await` a long sleep inside `javascript_tool`; sleep in Bash and measure in a second call.
- **Two snapshot tests fail by design** on any new route or migration: `endpoint-inventory.snapshot.txt` (sorted by full route string) and `MigrationRunnerTests`.
- **The claude-in-chrome `resize_window` does not actually resize this window** — `innerWidth` stays 2560. Use the Playwright mobile project for anything responsive.
- **Moving the browser's session token out of the page is blocked** by the permission classifier, correctly. Don't try to work around it.
- The dispatch-link lookup window was six hours, which excluded exactly the case that matters — a large torrent takes longer than that to download. It is 90 days now.

## Where James's bar sits

He rejected the dashboard twice before accepting it. What works is layout and honesty, not decoration.

- Measure it: `document.documentElement.scrollHeight / innerHeight` near 1 for a pane.
- **Repetition is a defect.** The same subject in two cards counts. So does a section heading and its own rows saying the same words — that is why `SharingDecision` carries a short `Detail` beside the whole `Reason`.
- Idle states must look watched, not dead — but preferably by saying something true rather than animating.
- **Simplicity is the product.** When he says "is this the best most user friendly way? it's not easy for beginners", the answer is usually to remove the setting entirely and make Deluno decide, explaining the consequence once in plain words.
- Expect three passes. Ask which direction lands before building more.
- He asks "is this your 100%?" and expects a real answer.
