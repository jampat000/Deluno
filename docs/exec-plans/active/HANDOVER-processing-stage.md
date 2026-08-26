# Deluno — handover

You're picking up Deluno (`C:\Projects\Deluno`, github.com/jampat000/Deluno): a Windows .NET 10 + React 19 media-automation app replacing Radarr/Sonarr/Prowlarr/Huntarr/Cleanuparr/Recyclarr/Upgradarr/Trash Guides. Issue [#194](https://github.com/jampat000/Deluno/issues/194) is the product bar: do everything the arr-suite does, better and **simpler**.

`main` is at `1b1f8bf`, working tree clean, 740 .NET tests pass.

## Standing rules from James — do not deviate

1. Work directly on `main` for **Deluno**. No feature branches. Commit and push.
2. **MediaMop is different**: `main` is protected, use a branch + PR. Merging needs `--squash --admin` (merge commits are disallowed and `enforce_admins` is on, so the squash-with-admin path is the only one that works).
3. Deluno: never run GitHub Actions. **MediaMop: Actions are expected** — that is how it releases.
4. Verify live in **real Chrome** via `mcp__claude-in-chrome__*`. The in-app browser pane is not a substitute.
5. Australian English in all user-facing copy.
6. Preserve the 20,000+ item scale invariant.
7. **Stop `Deluno.Host` before any build** — it locks the DLLs.
8. Add tests for contract, persistence, routing, status or schema changes.
9. He wants **short answers** and `AskUserQuestion` with brief options. Simplicity is the product. Repetition is a defect.
10. **Do not cut corners on setup.** He called this out directly and he was right — configuring through the API instead of the UI hid two real bugs and created one of my own. Set things up the way a user would.

## The simulation VM — this is the rig now

`Deluno Sim 2025` in Hyper-V, guest name `MEDIAMOP-TEST`, **10.1.1.142**, Windows Server 2025, 6 vCPU / 16 GB / 200 GB, on the `Deluno LAN` switch.

| What | URL | Sign in |
|---|---|---|
| Deluno | http://10.1.1.142:5099 | `admin` / `Deluno-Lab-2026!` |
| MediaMop | http://10.1.1.142:8788 | `admin` / `MediaMop-Lab-2026!` |
| qBittorrent | http://10.1.1.142:8080 | LAN subnet whitelisted — walks straight in |
| Windows (RDP/WinRM) | 10.1.1.142 | `Administrator` / `Deluno-MM-Lab-2026!` |

All three run as **scheduled tasks at startup**, so a reboot brings the rig back by itself (proven — the VM restarted mid-session and everything returned).

Deluno API key for scripting: `deluno_qP_RUDYaIPFbdcxJgkwfm7p3dsvU6kCyuPmixk4Yk_o` (scope `all`).

**Folder topology** — both apps must agree, and that is a user responsibility, not a bug:

```
C:\Deluno\Downloads-Complete\Movies   qBittorrent category save path AND MediaMop Refiner watched folder
C:\Deluno\Refined\Movies              MediaMop Refiner output AND Deluno library "clean output" path
C:\Deluno\Library\Movies              Deluno library root
C:\Deluno\Work\{Movies,TV}            Refiner work dirs (must not overlap each other)
```

**The indexer runs on the desktop, not the VM**: `torznab_seed.py` in this session's scratchpad, started with `TORZNAB_BIND=0.0.0.0 TORZNAB_ADVERTISE=10.1.1.102`. It serves real .torrent files whose bytes qBittorrent fetches over webseeds, so transfers and hash checks are genuine. **It is not running as a service — restart it before any acquisition test.**

## What shipped

**[#280] The Processing stage could never let go of an item** (`1b1f8bf`). This is the headline. `GetOverviewAsync` rewrites a finished download to `waitingForProcessor` when its library refines before importing — that is what makes the Processing node show a count. `WorkPlanner` then read that *same enriched snapshot* and only handed off items whose status was `importReady` or `completed`. So the status that made the stage appear was the status that stopped the hand-off being created. No hand-off meant no import job, and no import job meant the status was never rewritten, so it could not recover. The stage was not unproven, it was non-functional.

**Processor reachability** (`81dd0c2`). The connection test forgave only `405` on its HEAD probe. FastAPI answers `404`, so every FastAPI-based processor read as unreachable while working perfectly. Any status now means reachable; only a transport failure means unreachable.

**The app rendered as raw source** (`7b56e1a`). `MapFallback` used `SendFileAsync`, which does not set a content type, and Deluno sends `nosniff`. Every client-side route served `index.html` as plain text. **The whole app was unusable in a browser once installed.** 260 smoke tests missed it because `playwright.config.ts` points `baseURL` at a separate preview server that does not exist in a real install — see [#291](https://github.com/jampat000/Deluno/issues/291).

**MediaMop 2.4.0 and 2.4.1** shipped. 2.4.0 de-vendored media managers: one connection table with a `kind`, one intake webhook `POST /api/v1/intake/webhook/{source}` with payload dialects, per-connection webhook secrets, and Refiner reporting completions back. 2.4.1 fixed HEAD handling and rewrote the settings screen in plain language. See MediaMop ADR-0013.

## What is open

**[#280] is not finished.** One item went the whole way — grab → qBittorrent → hand-off → MediaMop remux → callback → import, with the file landing in `C:\Deluno\Library\Movies\`. But **the counts never clear**: after a successful import, telemetry still reports `processingCount: 1, waitingForProcessorCount: 1`. The torrent is still seeding so it stays in the client queue, and the import job is keyed to the *refined output* path while the queue item is keyed to the *original download* path, so the enrichment never matches them up and never moves the status on. **Start here.** The remaining #280 questions still need answering: whether the `Math.max(0, …)` clamp is reachable, and whether `ProcessorTimeoutMinutes` surfaces anywhere a user sees it.

**[#293](https://github.com/jampat000/Deluno/issues/293) — P0.** You cannot add a download client using the form's own defaults. Submit is gated on the form being *dirty*, not *valid*: retyping `qBittorrent` over `qBittorrent` enables the button. It is a first-run blocker on the setup ladder.

**[#292](https://github.com/jampat000/Deluno/issues/292)** — a client can be saved with a protocol the dispatcher cannot use, the connection test still reports healthy, and it only fails at grab time. Also: activity said "Deluno sent … to qBittorrent" when nothing was sent.

**[#291](https://github.com/jampat000/Deluno/issues/291)** — the smoke suite never tests how the shipped app serves its own UI.

**[#290]** — menu colours. James chose: move the six nav accents off the semantic hues and light them only on the active or hovered item.

**[#269]** README refresh — description rewrite is not blocked; screenshots need his password via `DELUNO_E2E_USERNAME`/`PASSWORD`.

MediaMop backlog: [#319](https://github.com/jampat000/MediaMop/issues/319) release jobs run sequentially though independent (~7 min), [#320](https://github.com/jampat000/MediaMop/issues/320) no dependency caching, [#321](https://github.com/jampat000/MediaMop/issues/321) the Velopack build is 8.2 min and is the critical path.

**Blocked externally:** #78, #81, #82, #129.

## Traps — save yourself the time

- **`scripts/publish-windows.ps1` calls `.\.dotnet\dotnet.exe`, which does not exist here.** Use the PATH SDK (10.0.400). A publish takes ~10 minutes; background it.
- **Deluno binds loopback only unless `Server:AllowLan` is set** (`Program.cs:45`, deliberate and well-commented). On the VM it is a machine env var.
- **A process started with `Start-Process` inside a WinRM session dies when the session closes.** Clean startup logs, a listening socket, then nothing. That is why everything on the VM is a scheduled task.
- **Host-side `bcdboot` cannot build an offline BCD on this machine** — the host's own live BCD owns the `BCD00000000` hive name, so every attempt collides. Let Windows Setup install rather than applying a WIM by hand.
- **Server 2025 Setup will not read `autounattend.xml` from a fixed disk.** It must be removable media; there is no ADK here, so build a small ISO with `pycdlib`.
- **qBittorrent 5.x refuses to start its WebUI without credentials**, whitelist or not. The log says `WebUI: Credentials are not set`. Generate a PBKDF2 entry (see `qbt-password.py` in scratchpad).
- **Git Bash mangles backslashes** in UNC paths and heredocs. Use PowerShell for anything with `\`.
- **`rg` mangles some route-string matches** in this codebase (shows `.n"` instead of the path). Use `tests/Deluno.Platform.Tests/Routing/endpoint-inventory.snapshot.txt` for the authoritative route list.
- **`/api/openapi/v1.json` 404s on the packaged build**, so you cannot introspect the live API that way.
- **The NAS (`\\storage-city\Data\Media`) is not reachable from the VM's service account.** Guest needs a real password, and the desktop reaches it via a saved Credential Manager entry for `guest`. Libraries are local to the VM for now.
- **MediaMop's API needs `X-Requested-With: XMLHttpRequest`** plus CSRF plus an `Origin` header on every state-changing call.

## Where James's bar sits

- **Repetition is a defect.** The same subject in two cards counts.
- **Simplicity is the product.** When he says "is this the best most user friendly way?", the answer is usually to remove the setting and make Deluno decide, explaining the consequence once in plain words.
- **Write for the person reading it.** He rejected a settings card for saying `"http://… answered"` and offering a bare URL path. Say what to do, name what to check, and give people something they can actually copy.
- He asks "is this your 100%?" and expects a real answer.
