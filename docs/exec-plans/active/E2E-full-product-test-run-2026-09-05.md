# E2E run — 5 September 2026

Against `main` at the head of 5 September, deployed to the lab. This run exists
to walk the phases the 4 September run never reached: **9, 10, 3.4–3.10 and
11.3–11.8**, and to re-walk **acquisition (Phase 8)**, which #417 and #423 had
only ever been proved against by unit tests.

## State this run starts from

The lab was carrying a working config and a library full of debris from earlier
runs. The config was kept — it is what phases 0–8 established and validated, and
throwing it away would have meant rebuilding all of it before reaching the part
that has never been tested. The **media** was cleared.

Everything removed was moved to `C:\Deluno\before-e2e-20260905-082144`, not
deleted — the rule Phase 0.2 applies to the data root.

| What | Before | After |
|---|---|---|
| `Library\Movies` | 6 folders | empty |
| `Library\TV` | 1 folder | empty |
| `Downloads-Complete` | 5 items across three folders | empty |
| `Refined\Movies` | 4 releases | empty |
| Catalogue | 1 movie, 0 series | empty |
| Reconciliation issues | **11** | **0** |

One folder needed `-LiteralPath` to move: `Big Buck Bunny (2008) [{IMDb ID}]`.
PowerShell reads `[...]` as a wildcard, so the first `Move-Item` matched nothing
and reported success. Worth knowing for any lab scripting that touches library
paths — Deluno itself is unaffected, being C#.

### Rig

| What | State |
|---|---|
| Torznab (desktop, `10.1.1.102:9117`) | started, 8 releases built |
| VM → torznab | **HTTP 200 confirmed from the VM**, not assumed |
| Lab Torznab indexer | healthy |
| SABnzbd | healthy |
| qBittorrent | healthy |

## Defects fixed before this run started

Five shipped on 4–5 September, all found by driving the product rather than by
the suite:

| | What was wrong |
|---|---|
| #425 | The Add screen never said you already held a title |
| #426 | The enrichment lookup discarded its provider id, so every added title was stored with no cast, crew, runtime or certification |
| #427 | Five path defects that only bite on Linux, including a hardlink check that told container users their two copies were one file |
| #429 | Re-adding a title you already held cleared the record that its file existed — and put it back on the wanted list, so Deluno would re-download what it was holding |
| #430 | A malformed import body answered 500 "unexpected error" instead of 400 |
| #431 | A folder Deluno itself named read back as "Big Buck Bunny 2008 -DELUNO" on the Import Existing screen |

---

## Phase 8 — acquisition, refinement and import (re-walk)

| # | Do | Must be true | Outcome |
|---|---|---|---|

## Phase 9 — missing, upgrades and automation

| # | Do | Must be true | Outcome |
|---|---|---|---|

## Phase 10 — recovery and cleanup

| # | Do | Must be true | Outcome |
|---|---|---|---|

## Phase 3.4–3.10 — quality

| # | Do | Must be true | Outcome |
|---|---|---|---|

## Phase 11.3–11.8 — the rest of the product

| # | Do | Must be true | Outcome |
|---|---|---|---|
