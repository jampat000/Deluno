# Handover — 4 September 2026, evening

Deluno, `C:\Projects\Deluno`, github.com/jampat000/Deluno.

**Health:** 1,701 backend tests passing (2 skipped), 310 web tests,
`npm run ci:check` 9/9 — and, for the first time, **GitHub Actions running and
green**.

> The morning handover said 304 web tests. It was 302. Measured, not assumed:
> pull the new file out, re-run, put it back.

---

## What changed today, and the one thing to take from it

Three issues shipped. The third was not on anybody's list, and is the reason
this handover exists.

| | |
| --- | --- |
| **#424** | The Add screen shows a title you already have. `/api/metadata/search` marks each result with `libraryEntryId`; the card offers to open it instead of adding it twice. Verified live on the lab, films and shows. |
| **`providerId`** | `/api/metadata/search` bound the parameter nowhere and passed `null`, so the "enrich this card into the full record" call re-ran the same search and the client silently fell back. Every title was stored with no cast, crew, runtime or certification. |
| **Linux** | GitHub Actions were re-enabled, and `main` went red within the hour on a defect class no Windows run can show. |

### The defect class, because it will recur

**`System.IO.Path` answers for the machine it is running on, and Deluno stores
paths another machine wrote.** The installer is Windows, the container image is
Linux, and a migration from Radarr or Sonarr carries whatever paths that install
recorded. On Windows `Path` treats both separators and
`GetInvalidFileNameChars()` returns the full set; on Linux a backslash is an
ordinary filename character and that set is NUL and `/` alone.

Five places had it. Ranked by what it cost:

1. **`SharingFootprint`** took the volume from
   `Path.GetPathRoot(Path.GetFullPath(path))`. On Linux `C:\Deluno` is a
   *relative* path, so a download on `C:` and a library on `D:` both resolved
   under the working directory and compared equal. Worse on a real Linux
   install: **every** POSIX path is rooted at `/`, so `/downloads` and `/media`
   — separate mounts, the ordinary container arrangement — also compared equal,
   and a hardlink does not cross a mount. The file's own summary calls that the
   direction that "lets a drive fill up silently". It now reads a drive letter
   or UNC share from the path's shape, or resolves the mount point.
2. **`NamingTemplateRenderer`** sanitised with `GetInvalidFileNameChars()` plus
   a hand-written list that had every dangerous character *except* the
   backslash. So a title containing one survived, and `RenderFolder` then split
   on it — a nested folder built out of a title, the one thing that function
   exists to prevent.
3. **`WorkPlanner.InferImportFileName`** — same gap, on release names that come
   from **indexers**. No test covered it on Linux, so CI never saw it; found by
   sweeping for the shape after CI caught #2.
4. **`SubtitleFileNaming`** wrote the whole path into the subtitle's filename.
5. **The subtitle sync job and the dispatch list** reported
   `D:\Media\film.en.srt` where a person expected a name.

**The rule now lives once**, in `MediaPath` (`Deluno.Contracts`): *read* a
stored path by its own shape, and keep `Path` for *acting* on one — so a path
this host cannot reach is still refused rather than half-understood. Every one
of these fixes is a **no-op on Windows**, which is what makes them safe.

---

## The tooling change that made it tractable

`scripts/test-dotnet-serial.ps1` threw on the first failing project.
`Deluno.Integrations.Tests` is alphabetically first, so five failures there hid
whether the other six projects were healthy — one fix, one push, one wait, per
project. It now runs every project and reports the failures together. That
turned six CI rounds into two.

`ApplicationTestHost.StartAsync` also gained an optional `replaceServices` hook.
It is what made the two new endpoint suites possible (the metadata provider
reaches TMDb, which a test must not), and it is the lever for the ~204 untested
routes below.

---

## Outstanding, in the order worth taking it

1. **The Linux surface is not finished.** CI only ever fails where a test
   exists, and until today nothing outside test-covered code had run on Linux at
   all. The container image ships that code. Worth a deliberate pass now that
   there is a working guard — start by grepping for `Path.` used on a *stored*
   path rather than one about to be opened.
2. **Two intermittent test flakes**, filed rather than guessed at.
   `DeleteLifecycleTests.Deleting_a_backup_removes_the_archive_from_disk` and
   `DownloadThroughputRepositoryTests.Readings_come_back_oldest_first…` each
   failed once under full-suite load and passed alone and on four re-runs.
   Specific suspect: `TestStorage.Dispose` calls
   `SqliteConnection.ClearAllPools()`, which is **process-global** while xUnit
   runs classes in parallel. A `testhost` was also seen outliving its completed
   run by 22 minutes, holding file locks that blocked the next build. Not fixed
   because it could not be reproduced on demand, and the clean fix needs
   production's `Pooling = true` to become overridable. (A third flake,
   `Rejects_a_recently_written_file`, *was* diagnosed and fixed: it relied on
   the assertion arriving inside a two-second window.)
3. **The hasFile/orphan disagreement.** Big Buck Bunny reads `hasFile=false`
   with `filePath` still set, while reconciliation calls that same file an
   orphan. The file is intact and `reimport` repairs it. Cause still unknown —
   worth its own investigation, not a theory.
4. **~204 API routes no test touches** (`npm run coverage:inventory` — read the
   *untouched* number, it over-counts coverage). `ApplicationTestHost` makes
   these cheap now.
5. **E2E phases 9, 10, 3.4–3.10, 11.3–11.8** never walked, and the acquisition
   path re-walk on the lab — #417 and #423 are unit-proven only.
6. **[#82](https://github.com/jampat000/Deluno/issues/82)** 14-day soak,
   unblocked; a P0 restarts the clock at Day 1.
   **[#78](https://github.com/jampat000/Deluno/issues/78)** GA readiness needs
   it, and still quotes stale counts (235/86/18/52/3 and 189) and demands a
   *signed* candidate, contradicting the recorded decision not to buy a
   certificate. Both lines want correcting before that issue is worked.

---

## Rig notes that still hold

- Lab is the environment of record: `http://10.1.1.142:5099`, deploy with
  `./scripts/publish-windows.ps1 -Fast` then `./scripts/deploy-lab.ps1` with
  `DELUNO_LAB_PASSWORD` set.
- **Live Chrome no longer needs the owner to sign in by hand.** Log in over the
  API, then write `deluno-auth-token` and `deluno-auth-user` into
  `sessionStorage` and reload — no password touches a form. Keys are defined in
  `apps/web/src/lib/use-auth.tsx`. One quirk: after a fresh navigation the Add
  dialog takes focus back, so the first click-and-type into its search box is
  often swallowed — click and type again rather than concluding it is broken.
- Never write close/fixes/resolves near an issue number in a commit or PR body;
  `ci:check` fails on it.
- Never run two `dotnet` commands at once — file locks. Clear stragglers with
  `Get-Process testhost | Stop-Process -Force`.
