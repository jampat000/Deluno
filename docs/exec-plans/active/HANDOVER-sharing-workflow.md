# Deluno — handover

You're picking up Deluno (`C:\Projects\Deluno`, github.com/jampat000/Deluno): a Windows .NET 10 + React 19 media-automation app replacing Radarr/Sonarr/Prowlarr/Huntarr/Cleanuparr/Recyclarr/Upgradarr/Trash Guides. Issue [#194](https://github.com/jampat000/Deluno/issues/194) is the product bar: do everything the arr-suite does, better and **simpler**.

Working tree clean, `main` at `516a3eb`. **All gates green and actually run**: 694 .NET tests, 54 web unit tests, `ci:check` 7/7, and the Playwright smoke suite 255 passed / 0 failed across desktop and mobile.

## Standing rules from James — do not deviate

1. Work directly on `main`. No feature branches. Commit and push to main.
2. Never run GitHub Actions — disabled to avoid cost. Local gates only. (`gh` CLI for issues/PRs is fine.)
3. Verify live in real Chrome via `mcp__claude-in-chrome__*` — not code inspection, not the in-app browser pane.
4. Australian English in all user-facing copy.
5. Preserve the 20,000+ item scale invariant — never load the whole catalogue into memory.
6. **Stop `Deluno.Host` before any build** — it locks the DLLs. You will hit this repeatedly.
7. Add tests for contract, persistence, routing, status or schema changes.
8. Use `rg` for search.

**He wants short answers.** He told me twice: "ask me properly I can't understand too much text". Use `AskUserQuestion` with short options rather than walls of prose. When he says a design point is unclear, that means the writing was too abstract — rewrite it in plain words, not more words.

## Running it

```
Storage__DataRoot=C:\Projects\Deluno\.deluno\data
dotnet C:\Projects\Deluno\src\Deluno.Host\bin\Debug\net10.0\Deluno.Host.dll
```

Without `Storage__DataRoot` the host silently creates an empty `data/` in the repo root and you'll think the DB is broken.

Vite: `npx vite --host 127.0.0.1 --port 5173` from `apps/web` via the **Bash** tool with `run_in_background: true` (it dies when a PowerShell call ends).

Chrome is signed in on `http://127.0.0.1:5173`. **His password is private — you cannot log in.** When the session expires, ask him to click Sign in and do other work meanwhile.

## Gates

```
dotnet test Deluno.slnx --configuration Release
```

```
npm run ci:check
```

Plus `npx vitest run` and `npm run build:web` in `apps/web`. `npx tsc -b` is incremental and skips new files — `npm run build:web` is authoritative.

Run them. The smoke suite caught a real regression this session that nothing else did — hiding Recently added on an empty library had removed the only "Add a movie" link from the dashboard. **Never run while `Deluno.Host` is up.** `ci:check` builds the whole solution; the Playwright smoke suite (`npm run test:web`) starts a *competing* Deluno.Host on 5199 and a web server on 5174, so don't run it while James is using the instance.

## The live test rig

A real end-to-end rig exists. Not mocks — real qBittorrent doing real transfers with real hash checks.

- **qBittorrent v5.2.1**, installed, WebUI on `127.0.0.1:8080`. I set `WebUI\LocalHostAuth=false` so Deluno connects without credentials. **Original config backed up at `%APPDATA%\qBittorrent\qBittorrent.ini.deluno-e2e-backup`.**
- **Torznab feed + webseed host** on 9117: `…\scratchpad\torznab_seed.py` (session scratchpad — copy it somewhere durable). Serves genuine `.torrent` files with correct bencode and SHA1 pieces, whose data qBittorrent pulls over BEP-19 webseeds. Media is Big Buck Bunny (Blender, CC-BY). Run with `python -u torznab_seed.py`.
- Test media at `C:\Deluno\e2e` (~60 MB). Library roots `C:\Deluno\Movies` and `C:\Deluno\TV Shows`.
- Deluno is configured: quality profiles, indexer "E2E Local Torznab", download client "qBittorrent (local)", a `url-list` import list, library routing, 4 movies + Breaking Bad.

Bring it up: host → vite → qBittorrent → `torznab_seed.py`. All four must listen on 5099 / 5173 / 8080 / 9117.

## What shipped this session

**Live E2E run found five real defects, all fixed and closed:** [#282](https://github.com/jampat000/Deluno/issues/282) import wrote media into the host's working directory instead of the library (and reported success), [#283](https://github.com/jampat000/Deluno/issues/283) quality profiles never enforced their allowed qualities, [#284](https://github.com/jampat000/Deluno/issues/284) size rules never rejected anything, [#285](https://github.com/jampat000/Deluno/issues/285) season search always 500'd, [#286](https://github.com/jampat000/Deluno/issues/286) a missing `quality_profile_id` column broke every per-title profile assignment.

**Dashboard:** setup ladder reordered by lifecycle, First Acquisition removed, Discover Media no longer a tile, ladder disappears once the basics are configured, setup items no longer duplicated in Needs You ([#275](https://github.com/jampat000/Deluno/issues/275)), throughput wave's clipped "now" marker fixed.

**Sharing and reclaim ([#288](https://github.com/jampat000/Deluno/issues/288)) — done and closed.** A finished download exists twice: in the library permanently, and in the download client where it may still be shared. Some sites require sharing or they ban you; never deleting fills the drive. Deluno now models it:

- `SharingPolicy` + `SharingPolicyEvaluator` (`Deluno.Recovery/Policies`) — mode, optional time target, optional ratio target, stuck behaviour. Pure, returns the sentence a user reads.
- Global default in settings, per-**search-source** override (5 nullable columns, platform migration v25). The requirement belongs to the site, not the library.
- `SharingReclaimService` + `PlanSharingReclaimAsync` in the worker.
- "When a download finishes" card on **Automation & Recovery**.
- **Verified live:** three seeding torrents reclaimed once the rule was met, ~180 MB back, all three library copies untouched.

## How #288 finished

The last three parts landed in `516a3eb` and the issue is closed.

- **The dashboard status line.** The pipeline card carries a Sharing stage and a strip under it — `Big Buck Bunny · 2 days left … 59.0 MB`. Read from what the worker's pass *decided*, via `GET /api/download-clients/sharing`, not recomputed for display: an answer worked out separately from the action could contradict it. `IDownloadSharingRepository` stores the snapshot as one settings row (no migration — the download-health records next door work the same way) and ages it out after ten minutes, so a stopped worker shows nothing rather than yesterday's numbers.
- **`SharingDecision` gained a `Detail`** beside `Reason`. A section headed "Finished, still sharing" whose every row also reads "Still sharing" says the same thing twice. `Reason` is still what the activity feed gets, where nothing has said it yet.
- **The setup-time question** on each search source: *Does this site expect you to keep sharing?* with the user's own rule stated back underneath. Written only when the answer changes, so a hand-tuned rule survives an unrelated edit.
- **The different-drives warning** lives inside that strip rather than in its own card — the one place the fact changes what anyone should do. `SharingFootprint` (Recovery/Policies) decides whether the two copies are one set of file data and writes the sentence.

Finding that last one exposed a real bug: **`DispatchCatalogueLink` never carried `library_id`**, so nothing downstream could tell a hardlinked download from a genuine second copy. Fixed, with a test.

Also fixed in passing: the indexer drawer sent `privacy: "private"` on *every* save, relabelling any edited source. It only sends it on create now. Whether that field should exist at all is a separate question — nothing reads it.

## Open issues

- [#272](https://github.com/jampat000/Deluno/issues/272) machine telemetry (CPU/mem/disk I/O) — James asked for this explicitly; **both** disk readings (Deluno's own I/O *and* whole-disk load).
- [#273](https://github.com/jampat000/Deluno/issues/273) `DownloadProgress` is published once per grab with progress and speed both **zero**. The 3s telemetry poll is a stopgap.
- [#274](https://github.com/jampat000/Deluno/issues/274) audit every live surface for genuine realtime motion.
- [#276](https://github.com/jampat000/Deluno/issues/276) two idle speed readings on one screen.
- [#278](https://github.com/jampat000/Deluno/issues/278) mobile never looked at. [#279](https://github.com/jampat000/Deluno/issues/279) arrangeable panels (needs a direction call from James).
- [#280](https://github.com/jampat000/Deluno/issues/280) Processing stage unverified — blocked, needs a real FileFlows/MediaMop.
- [#281](https://github.com/jampat000/Deluno/issues/281) the `useQueries` tuple cliff.
- [#287](https://github.com/jampat000/Deluno/issues/287) cleanup still deletes behind the client's back on the *old* `remove-source-after-import` path — #288 replaced it but the old path is still there. **This is the obvious next one.**
- [#289](https://github.com/jampat000/Deluno/issues/289) what the throughput wave is for. **James wants the speed tile to show upload as well as download** — needs a sampler column and migration, not just frontend.
- Pre-existing GA items blocked externally: #78, #81, #82, #129.

## Traps — save yourself the time

- **Git Bash mangles backslashes** in `python - <<'PY'` heredocs. `r"C:\\x"` arrives as `C:\x`. Build paths with `chr(92)`, or use the Edit tool. It bites the **host command** too: `Storage__DataRoot=C:\\Projects\\Deluno\\.deluno\\data dotnet …` arrives as `C:ProjectsDeluno.delunodata`, and the host quietly builds a fresh empty install there and shows you the first-run setup screen — which reads exactly like your database being gone. Single-quote it instead: `export Storage__DataRoot='C:\Projects\Deluno\.deluno\data'`, and check the log says `storage initialized at C:\Projects\Deluno\.deluno\data` before believing anything the browser shows you.
- **Don't navigate Chrome with a dirty form.** `useUnsavedChanges` raises a native "Leave site?" dialog that CDP cannot dismiss — the renderer freezes and you must ask James to click it.
- **`performance.getEntriesByType('resource')` silently stops recording at 250 entries.** Call `performance.clearResourceTimings()` before measuring, or you'll conclude polling is broken when it isn't.
- **`document.visibilityState === 'hidden'`** (Chrome window not focused) makes TanStack stop polling *entirely* — `refetchIntervalInBackground: false`. Check visibility before measuring cadence.
- **Settings PATCH does not use `UpdatePlatformSettingsRequest`.** The endpoint binds `PatchPlatformSettingsRequest` and runs `PlatformSettingsPatchMerger.Apply`. Adding a field to the update contract alone means it's silently dropped.
- **`AddParameter(command, "@id", item.Id);` appears 4× in `SqliteConnectionsRepository`** — not a unique anchor. Anchor positionally.
- **Two snapshot tests fail by design** on any new route or migration: `endpoint-inventory.snapshot.txt` and `MigrationRunnerTests`.
- **`useQueries` widens every result to `{}`** past ~20 entries *or* when one entry gains an options callback. Put new dashboard queries in their own `useQuery`.
- **Playwright can't intercept requests a service worker handles.** Config sets `serviceWorkers: "block"`.
- A previous session left a **fake qBittorrent** (`verifier.py`) running on 8080. If the client behaves oddly, check what actually owns the port.

## Where James's bar sits

He rejected the dashboard twice before accepting it. What works is layout and honesty, not decoration.

- Measure it: `document.documentElement.scrollHeight / innerHeight` near 1 for a pane.
- Repetition is a defect. The same subject in two cards counts.
- Idle states must look watched, not dead — but preferably by saying something true rather than animating.
- **Simplicity is the product.** When he says "is this the best most user friendly way? it's not easy for beginners", the answer is usually to remove the setting entirely and make Deluno decide, explaining the consequence once in plain words.
- Expect three passes. Ask which direction lands before building more.
- He asks "is this your 100%?" and expects a real answer.
