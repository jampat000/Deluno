# Deluno — current baseline and handover

Snapshot: **3 September 2026, Australia/Sydney**.

Replaces the 1 September handover, which described an intentionally dirty
shared worktree at `38e5ae8`. That is no longer the operating mode: the tree is
clean, every branch is merged, and work reaches `main` through pull requests.

## Start here

- Work only from `C:\Projects\Deluno`.
- Read `AGENTS.md` and `docs/PRODUCT_NORTH_STAR.md` before changing anything.
- Run `git status --short --branch` before editing.
- An issue closes only when its acceptance criteria have been implemented,
  executed, remediated and evidenced. Broad issues stay open when one slice is
  proven.

## Git baseline

| Item | State |
|---|---|
| Branch | `main`, clean, ahead 0 behind 0 |
| HEAD | `52e0916` — *a download the client calls errored is not ready to import (#373)* |
| Merged 2 September | #365, #366, #367, #368, #369, #370, #371, #372, #373 |
| Open PRs | none |
| Local branches | none but `main` |

`main` **is** branch-protected and asks for one approving review. A solo
squash-merge therefore needs `gh pr merge --squash --admin`; admin enforcement
is off for exactly this, and the product owner has authorised agent-run merges.
AGENTS.md said the branch was unprotected until 2 September; it was true once
and stopped being true without the map noticing.

## Lab baseline

`http://10.1.1.142:5099`, credentials and rig topology in
`E2E-full-product-test.md`. Deploy only through the **Deluno Host** scheduled
task.

| Item | State |
|---|---|
| Readiness | ready, 9/9 |
| Executable | built by `publish-windows.ps1 -Fast` — loose assemblies, not a single-file bundle |
| Catalogue | 2 movies, 1 show |
| Jobs | 0 queued/running/failed; historical completed and dead-letter rows retained |
| Download clients | qBittorrent and SABnzbd healthy |
| Torznab fixture | **stopped**. It runs on the desktop, not the VM: `TORZNAB_BIND=0.0.0.0 TORZNAB_ADVERTISE=10.1.1.102 python scripts/lab/torznab_seed.py` |

## Deployment is no longer slow — use the new scripts

Deploying used to take about nine minutes and was moving roughly 300 MB to
deliver, typically, under a megabyte of change. Two causes, both fixed:

- **the publish folder was never cleaned.** `dotnet publish` does not remove
  what it no longer produces and the web assets are content-hashed, so every
  chunk since the project began was still there: 3,096 asset files where a
  build makes 83. The lab had 9,586 files where 670 are real; the first new
  deploy removed 9,506 orphans from it.
- **every deploy rebuilt a 163 MB single-file bundle.** Right for a release
  artifact, pointless for iteration.

```powershell
./scripts/publish-windows.ps1 -Fast   # ~20s, executable is 0.15 MB
$env:DELUNO_LAB_PASSWORD = '<lab password from E2E-full-product-test.md>'
./scripts/deploy-lab.ps1              # SHA-256 per file, copies only what differs
```

`deploy-lab.ps1` verifies every file on the host matches the publish output,
removes what the build no longer produces, restarts the task and proves
readiness. Use `-Rollback` for anything you would struggle to rebuild. For a
front-end-only change, `npm run build:web` and replace `App\wwwroot`.

**Never run two `dotnet` commands at once.** A second process takes file locks
on the built assemblies and fails the first with `MSB3027`/`MSB3021`, which
reads like a broken build and is not one. Several ten-minute losses on
2 September came from exactly this.

## Product decisions taken on 2 September

These are the owner's calls. They are not negotiable defaults to re-derive.

| Decision | Consequence |
|---|---|
| **Deluno ships unsigned.** No code-signing certificate is being bought. | Windows SmartScreen warns on first install; README, the release checklist and the release-notes draft all say so and give the two clicks past it. `release.yml` used to hard-fail an unsigned 1.x build, so **no 1.x release could be produced at all** — fixed in #371. |
| **#330 machine translation: not planned.** Deluno does not translate subtitles. | Closed. No code existed. |
| **#349 playback goals: parked** for discussion. | Do not work it. Do not close it. |
| **#81 and #82 are the very last things**, and can run in parallel. | Stop when you reach them. |
| **#337: free rein on the VM / Hyper-V.** | Install a container runtime there; neither this machine nor the lab has one today. |
| **#339: Cloudflare is available** for email. Park it if it becomes a project. | |
| **#329 Whisper: go ahead on the lab VM**, and **measure** it — model size, CPU/GPU time per file, memory, effect on the rest of the app — then recommend whether it is worth shipping. A "no" is an acceptable answer. | |

## The mistake to not repeat

**Three issues were closed by accident in one day** — #354, #338, #357 and #78
across four merges — because pull-request bodies and commit messages said
things like *"this does not close #354"*. GitHub's parser does not read
negation and does not care that you are quoting; one of the closures came from
the very commit that documented the rule.

Writing it into `AGENTS.md` did not stop it. It is now mechanical:
`scripts/check-issue-closing-keywords.ps1` runs inside `npm run ci:check` and
fails on a HEAD commit message that would close an issue. Write `Refs #NNN`,
and keep the number away from close/fixes/resolves.

All four were reopened. If an issue disappears from the open list unexpectedly,
check whether a merge closed it before assuming somebody decided it was done.

## Open backlog — 18 issues

**Release-preference chain**, the largest interlocked body of work. #354 is the
normative contract and closes only after #347–#353 do, so it cannot close
early. Dependency order: **#351 → #350 → #352/#353**, with **#343** as its own
lifecycle track. #349 is parked.

**Subber.** #301 is the outcome, #321 the Bazarr-parity delta, #329 Whisper. An
audit on 2 September found **#321's body substantially stale** — the
unknown-language question and embedded-as-a-choice are built end to end, and
`.en.sdh.srt` naming exists. Read
[the audit comment](https://github.com/jampat000/Deluno/issues/321#issuecomment-5508112058)
before estimating. Genuinely unbuilt: Language Equals, must/must-not-contain
language profiles, custom post-processing command, anti-captcha, the audio
column. Content modification has 5 of Bazarr's 8.

**On subtitle credentials:** Deluno already models them per provider. Gestdown
and Subf2m need nothing, Podnapisi is optional username/password, SubDL and
SubSource take a key. OpenSubtitles.com needs username, password **and** an API
key — that is the provider's REST API requirement, not Deluno's invention, and
the key is free from the same account. Do not "fix" this.

**#338** telemetry: three slices merged on 2 September. Remaining — the
per-client contract depth, and a complete grab → client → import trace
surviving restart for a torrent path.

**#357** metadata recovery: phone/keyboard/screen-reader evidence merged.
Remaining — populated file/assignment retention coverage, and a live-lab pass.

**Blocked or last:** #337 (needs a runtime), #339 (needs email), #340 (iOS
needs macOS and Xcode, which do not exist here), #341, #269, #322, and #78 →
#81 → #82.

## What the lab keeps proving

On 2 September the deployed lab exposed **five defects a green 1,500-test suite
had passed over**, two of them in code written and tested minutes earlier:

- an unreachable indexer threw a `NullReferenceException` and returned HTTP 500
  — `IntegrationResilienceResult<T>.Value` is `T?` and the payload was a
  struct, so the null-test guarding it could never fail;
- a guard written `hardReject = input.PreferencePlan is null` **cleared** a hard
  gate an earlier rule had raised;
- a lower-priority unknown family reopened a comparison a higher-priority one
  had already decided, making "your file is better" unreachable in practice;
- a completed download the client called *errored* was reported ready to import;
- the drawer said the same sentence twice and headed a refusal "Why Deluno
  likes it".

Deploy and look before claiming a slice works. The suite has never once been
the thing that found these.

## Verification

```powershell
npm run ci:check                                    # 9 checks
dotnet test Deluno.slnx --configuration Release     # 1,512 passed, 2 benchmark skips
npm run test:web                                    # 286 passed, 10 skipped
```

Real-Chrome verification against the deployed lab is required for user-facing
changes — batched per group of slices, not per slice. The lab session lives in
`sessionStorage` with roughly an hour's life, so a new tab starts signed out and
the owner has to sign in; park the tab on the target deep link first so the
redirect lands where the check is.
