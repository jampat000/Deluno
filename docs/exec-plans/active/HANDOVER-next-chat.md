# Deluno — handover

Paste this whole file as the first message of a new chat.

---

You are picking up **Deluno** at `C:\Projects\Deluno` (github.com/jampat000/Deluno) — a
Windows .NET 10 + React 19 media-automation app meant to replace Radarr, Sonarr,
Prowlarr, Huntarr, Cleanuparr, Recyclarr, Trash Guides and Bazarr. James is the
owner. Australian English.

## Non-negotiables

- **Work on `main` and push.** Never run GitHub Actions for Deluno.
- **Stop `Deluno.Host` before any build** and kill stray `testhost` — they lock the
  DLLs (MSB3027). Publish **self-contained**.
- **Verify live on the rig. A green suite is not evidence.** Nearly every defect this
  session was found by looking at the running app, and several passed their tests
  while being wrong.
- **Everything runs independently.** James, more than once: *"nothing relies on each
  other or fights or conflicts or overlaps… everything needs to run independently."*
  One saved file read is never worth coupling two features.
- **No second scheduler** (DESIGN-002 rule 3). Recurring work claims the existing
  heartbeat via `TryClaimScheduledPassAsync`, declared in `SystemTasks`.

## The rig

`http://10.1.1.142:5099`, `admin` / `Deluno-Lab-2026!`. Windows `Administrator` /
`Deluno-MM-Lab-2026!`. It holds 11 movies and 6 shows, **one of which has a file**.

```powershell
$p = ConvertTo-SecureString 'Deluno-MM-Lab-2026!' -AsPlainText -Force
$c = New-Object System.Management.Automation.PSCredential('Administrator',$p)
$s = New-PSSession -ComputerName 10.1.1.142 -Credential $c
Invoke-Command -Session $s -ScriptBlock { Stop-ScheduledTask -TaskName 'Deluno Host'; Start-Sleep 3 }
Copy-Item -ToSession $s -Path 'C:\Projects\Deluno\artifacts\publish\win-x64\*' -Destination 'C:\Deluno\App' -Recurse -Force
Invoke-Command -Session $s -ScriptBlock { Start-ScheduledTask -TaskName 'Deluno Host'; Start-Sleep 12 }
```

A front-end-only change is `npm --prefix apps/web run build` plus copying
`apps/web/dist/*` to `C:\Deluno\App\wwwroot`. **A C# change needs a republish.** The
host runs from a scheduled task called `Deluno Host` — use `Stop-ScheduledTask` /
`Start-ScheduledTask`; `Start-Process` over WinRM dies with the runspace and breaks
DPAPI.

The API is bearer-token: `POST /api/auth/login` with `{username,password}` returns
`accessToken`. James's live arr instances are at `10.1.1.35` — Radarr `:8310`,
Sonarr `:8989`, Prowlarr `:9696`, Bazarr `:6767`. **Look, do not save.**

## Baseline as handed over

Commit `de117a4`, working tree clean, pushed, rig running this build.

| Suite | |
|---|---|
| Persistence | 825 (+1 skipped) |
| Platform | 118 |
| Integrations | 77 |
| Movies | 64 |
| Worker | 54 |
| Series | 38 |
| Tray | 3 |
| **.NET total** | **1,179** |
| Web unit | 151 |
| Metadata gateway | 17 |

Run per project — `dotnet test tests/Deluno.<Name>.Tests`. **The full-solution run
hangs intermittently (#333)**, unexplained; per-project runs are the workaround.

Movies now serve **53 filter fields, 21 sorts, 18 poster options**; series 52 and 20.
Radarr has 33 / 21 / 14.

## Closed this session

#324, #331, #129, #308, #332, #311, #323, #325, #326, #306, #319, #310, #307.
Filed: #332, #333, #334, #335.

## What is in flight

Nothing. The last piece — poster hover actions — is finished, verified on both
shelves and pushed.

## The immediate next thing

**#314 is parked pending a decision and must not be built until James answers.**
A full audit is posted on the issue. It found that **nine scheduled passes are now
declared in `SystemTasks`, and seven more recurring services run with their own
timers and cannot be listed at all**: backup (daily 03:00), ranking model training
(24h), import recovery retention (24h), download dispatch polling (1h), plus three
samplers/publishers that are plumbing rather than tasks. Six decisions are open on
the issue. Ask, do not guess.

After that the recorded order is **#309 remainder** (a saved filter that *does*
something — needs James's decisions first, he pushed back hard on the framing), then
**#328** (tags), **#313**, **#315**, **#316**, **#317**, **#318**, **#320**, **#305**,
then Subber's remainder (#321, close #301).

**Do not touch #78, #81, #82, #269. Do not close #329 or #330.**

## What James has taught, that keeps costing when ignored

- **Repetition is a defect.** After a fix, find where else that shape lives. One rule
  written twice in two places that cannot check each other is where every defect here
  has come from.
- **"Declared, never populated."** A column, state or mark that exists and nothing
  writes. A filter over it returns no rows and looks like a fair answer. Guard by
  reading the value back *through the thing that consumes it*, not off the row.
- **Measure, do not reason.** Query plans, pixel positions, whether a switch produced
  a row. Several times this session a browser sweep passed a broken version because it
  measured the wrong property.
- **Prove a test discriminates.** Break the fix, watch it fail, restore it. Done for
  every guard added this session.
- **Ask when the decision is his.** He would rather be asked than have it guessed, and
  says so.

## Traps that have each cost real time

- **Bash heredocs mangle backslashes.** `\n` inside a quoted heredoc arrives as a
  newline and `\\1` as a control character. Write Python to the scratchpad with the
  Write tool and run it with `python <path>`.
- **Python slice-replacement over JSX is dangerous.** Two edits this session ate
  neighbouring methods or a component's opening. `git checkout <file>` and redo with
  an exact-string replace.
- **`.mark-grail` sets `position: relative`** and beats a Tailwind `absolute` on the
  same element. It silently dropped an element out of its parent, then collapsed
  another to zero height — both times rendering the right gradient invisibly. Put the
  sheen on an element Tailwind never positions.
- **Tailwind cannot parse commas in an arbitrary value.** `shadow-[...rgba(0,0,0,.5)]`
  emits nothing; use `rgb(0_0_0_/_0.55)`.
- **A migration already recorded never runs again.** Never edit a shipped migration —
  add a new one, or existing installs silently lack the column.
- **Two caches sit in front of metadata**: the gateway's KV (`search:vN:`) and
  Deluno's `SearchCacheShape`. Bump **both** when a payload's shape or content
  changes, and deploy the worker *before* relying on it.
- **The in-app browser pane's screenshot times out.** Use Claude in Chrome. Its zoom
  region is in *screenshot* coordinates, not page pixels — convert, or measure with
  `getBoundingClientRect` instead.
- **Never quote a suite number from a run started before your last edit.** Re-run it.

## Recent architecture worth knowing

- `SystemTasks` — every scheduled pass in one place, fixed intervals, read by
  `WorkPlanner`. Two tests read the planner's source and fail if anyone writes an
  interval at a call site again.
- `CatalogueFileFactsMigrationSql` / `CatalogueDecisionFacts` / `RatingSources` —
  migrations, indexes, backfills, write paths, filters and sorts are all **generated
  from one list** so a new entry cannot be half-added.
- `CatalogueMetadata.ToUpdate` is the **only** mapping from a provider result to the
  catalogue. The 18-argument positional overload that lost `Status` and `Studio` four
  times is gone.
- **No filter reads through `ws`** (the correlated wanted-state pick). All 104 are
  index walks, asserted by `FileFilterQueryPlanTests`.
- The poster card: state is a **top bar** whose size the Quality switch decides;
  subtitles own the **bottom edge**; three round actions sit centred on hover; every
  enabled switch draws **one reserved row** beneath, so a column means the same thing
  on every card.

## Open questions James has not answered

1. **#314** — the six decisions on the issue.
2. **#309's second half** — a saved filter that acts as a scope on the library cycle.
   He challenged the framing: *"filters are just display filters"*. Needs rediscussion
   before any of it is built.
3. Whether to add Radarr's fourth poster action (edit) — Deluno's equivalent is the
   drawer the card already opens.
