# Deluno — current baseline and handover

Snapshot: **1 September 2026, Australia/Sydney**.

This is the current operational baseline. It replaces the old handover that
described commit `de117a4`, a clean tree, 1,179 backend tests, and no work in
flight. Those claims are no longer current.

## Start here

- Work only from `C:\Projects\Deluno`.
- Read `AGENTS.md` and `docs/PRODUCT_NORTH_STAR.md` before changing anything.
- Preserve the shared working tree. Do not reset, checkout, clean, overwrite,
  stage, commit, push, or close issues merely to make the repository look tidy.
- Run `git status --short --branch` before editing.
- Do not touch or close #78, #81, #82, #269, #329, or #330.
- An issue closes only when its acceptance criteria have been implemented,
  executed, remediated, and evidenced. Broad issues remain open when only one
  slice is proven.

## Git baseline

| Item | Current state |
|---|---|
| Branch | `main` |
| HEAD | `38e5ae806d92a862fdcbd4bcaabef5bb83935add` |
| `origin/main` | same commit; ahead 0, behind 0 |
| Worktree | intentionally dirty shared worktree |
| Tracked changes | 319 modified, 1 deleted |
| Untracked paths | 182 |
| Tracked diff | about 24,942 insertions and 4,306 deletions |
| Whitespace check | `git diff --check` passes |
| Current CI gate | `npm run ci:check` passed 8/8 with no warnings on 1 September after the latest local notification fix |

The dirty tree contains the backlog implementation, tests, documentation, and
test-result artefacts accumulated across the current autonomous run. It is not a
safe candidate for a mechanical cleanup. Integration must be deliberate and
path-scoped after the work has been reviewed.

## Live lab baseline

The Windows lab is at `http://10.1.1.142:5099`. Credentials and the scheduled-task
deployment procedure are recorded in `E2E-full-product-test.md`. Never launch a
second ad-hoc Deluno host; deploy and restart only through the **Deluno Host**
scheduled task after `npm run ci:check` passes.

| Item | Current state |
|---|---|
| Scheduled task | `Deluno Host` running |
| Host PID | 2604 |
| Readiness | ready; all 9 checks ready |
| Deployed executable SHA-256 | `45D004F5F3C53C27622CA9194278351D334D2E64A52F869EBB7D6447E9C8B3B6` |
| Rollback | 10 retained; latest `C:\Deluno\App.rollback-20260901-165400` |
| Catalogue | 2 movies, 1 show; no temporary catalogue titles |
| Notifications | globally disabled |
| Webhooks | one pre-existing `E2E webhook test`; the temporary DLQ fixture webhook was removed |
| Download clients | SABnzbd and qBittorrent healthy |
| qBittorrent | 3 retained completed/import-ready lab downloads |
| SABnzbd | no queued download; retained native/import audit history |
| Jobs | 156 completed, 114 historical dead-letter, 0 active |

The lab is functionally healthy, not a blank database. Readiness confirms all
five databases, migrations, writable storage, a fresh worker heartbeat, and no
stalled or lagged jobs. Historical completed and dead-letter job rows are kept as
audit evidence. One temporary webhook-delivery dead-letter row is also retained
because delivery history deliberately survives deletion of its webhook.

During this baseline audit, an old deterministic SAB entry named
`Breaking.Bad.S01E01.2160p.WEB-DL.x264-DELUNO` was found creating a fresh rejected
sample import every worker cycle. Only that exact SAB history entry
(`4a5ee8bf-96a3-42c7-be7f-46445d57e2c5`) was removed. The imported `DELUNOLAB`
entries and all user/catalogue state were left intact. The last already-created
retry exhausted normally; the jobs API then reported **0 queued, running, or
failed jobs**, and readiness remained 9/9.

## What has been implemented and executed

The detailed evidence ledger is
`E2E-full-product-test-run-2026-08-31.md`. The highest-value completed slices are:

1. **Real download/import path** — qBittorrent movie import and deterministic
   SABnzbd NZB/yEnc TV downloads were executed through Deluno and survived
   scheduled-task restarts. Multi-episode imports now persist final library paths,
   not disposable client paths.
2. **Season-pack safety and atomic import (#342)** — explicit episode manifests,
   catalogue validation, rejection of ambiguous multi-video packs, transactional
   catalogue writes, filesystem compensation, idempotent retry, and positive live
   two-file placement were implemented and exercised.
3. **Upgrade convergence and atomic replacement (#345/#342)** — installed-file
   evidence is evaluated per episode; unsafe whole-season replacement is held
   before external work; exact episode-to-owned-path manifests drive atomic
   multi-file replacement with rollback and restart-safe `already-committed`
   retries. A wrong-owner negative case and a positive two-file replacement were
   executed live.
4. **Calm TMDb removal recovery (#357)** — movie and TV titles, files, history,
   monitoring, and local metadata are retained. Evidence can be acknowledged;
   retry and reviewed remap are separate from removal. Remap preview/apply now has
   confirmation tokens, conflict protection, stale-token checks, TV episode-loss
   protection, and restart proof. This directly implements the product rule that
   an upstream metadata deletion is a title-scoped recoverable condition, not a
   system emergency.
5. **Truthful SAB identity/telemetry (#338)** — SAB add responses now retain native
   `nzo_id` at dispatch creation across grab paths; telemetry converges native and
   dispatch-derived history into one stable row. The exact identity survived a
   live restart.
6. **Automation idempotency (#344)** — the PowerShell contract now sends the real
   `Idempotency-Key`, verifies byte-identical replay, and requires 409 for the same
   key with a conflicting body. A non-dry mixed movie/series write with explicit
   episodes was executed, replayed, conflict-tested, verified, and cleaned up.

No broad issue was closed on the strength of these partial slices.

## Implemented locally but not deployed

The live webhook dead-letter exercise exposed a truthful-remediation defect: an
exhausted delivery was labelled `RetryScheduled` and told the user Deluno would
retry even though `nextAttemptUtc` was null. The local fix:

- records exhausted webhook delivery as `ManualAction`;
- clears retry time;
- tells the user to check the service/network and replay the delivery;
- keeps transient retry wording for attempts that really are scheduled.

Focused notification/integration-failure tests passed **16/16**, and the complete
CI gate passed **8/8** after the fix. This change is **not in the deployed hash
above**. It still needs CI-gated scheduled-task deployment, live dead-letter
replay, restart proof, cleanup, ledger evidence, and an issue comment before that
slice can be called complete.

## Open backlog baseline

There are **27 open issues**:

- protected/external or explicitly untouched: #78, #81, #82, #269, #329, #330;
- Subber/stack epics: #301, #321, #322;
- infrastructure and integration: #337, #338, #344;
- portal/mobile: #339, #340, #341;
- TV/import/convergence: #342, #345;
- media plans and release preferences: #343, #347–#354;
- metadata recovery: #357.

Important remaining acceptance work:

- #337 has no honest container runtime proof because Docker, Podman, and nerdctl
  are absent locally and in the lab.
- #338 still needs the broader client/indexer matrix and visible Health, Activity,
  and title-level failure-mode acceptance.
- #344 still needs the webhook replay slice above, Home Assistant install/action
  proof, existing-library import automation, and clean-host CI orchestration.
- #341 is a product-wide real-device mobile workflow matrix, not a small layout fix.
- #342/#345 still need a real client/indexer season replacement dispatch,
  automatic zero-work convergence, and representative Daily, Anime, specials,
  scene-numbering, and owner-mapping flows.
- #357 is implemented, tested, and live-proven for movie and TV, but remains open
  because the work is still in this uncommitted shared tree rather than durable
  integrated repository history.
- #343 and #347–#354 are broad normative/implementation/proof/UX bodies of work;
  they must not be closed from isolated tests or scaffolding.

## Recommended next sequence

1. Deploy the already-tested webhook terminal-state fix through the scheduled
   task, execute dead-letter → replay → restart proof, clean its temporary fixture,
   and update #344 without closing the broad issue.
2. Continue #338's remaining truthful failure surfaces and client/indexer matrix.
3. Return to the real-client #342/#345 replacement/convergence gaps.
4. Integrate the shared worktree in reviewable, explicit path groups; only then
   reassess issue closure from acceptance evidence.
