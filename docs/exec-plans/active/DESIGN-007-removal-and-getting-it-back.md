# DESIGN-007 — Removing a title, and being able to get it back

> **Status: proposed, not settled.** The audit below is fact — it is what the
> code does today. The table is a proposal, and the questions in
> [What still needs deciding](#what-still-needs-deciding) are genuinely open.

James, on the scenario that started this:

> "If title already exists or was previously downloaded and has been deleted —
> currently radarr and sabnzbd etc keep that history and prevent the title from
> being downloaded again but doesn't really tell the user why or how to fix
> it."

And on why a table rather than another fix:

> "I think the better way is if we put a design together for delete scenarios
> and the outcomes... we dont want to have delete just being delete with nothing
> matching, it needs to be 1:1 mapping where possible for all delete routes and
> scenarios."

---

## Why this exists

Deluno removes things in five places, and they do not agree.

| Path | What it removes | Ownership check |
|---|---|---|
| `POST /api/{movies,series}/bulk` — remove | Catalogue row, tracked files to the recycle bin, intake exclusions | — |
| `DELETE /api/{movies,series}/{id}` | The same, via the same helper | — |
| Health remediation | Client queue entry, `"delete"` | **Yes** — "will not remove an external-client item or its payload without proven ownership" |
| `ReclaimCompletedAsync` | Client entry and payload, `"delete-with-data"` | — |
| `POST /{id}/force-redownload` | Client entry and payload, hand-off reset, exclusions | — |

Three of those five spell the action as a bare string. One consults the
cleanup preview. The one a person triggers deliberately — the force — consults
nothing, which is the wrong way round.

**And none of the removal paths touch the download client at all.** Removing a
title cancels its pending jobs, moves its files to the recycle bin and deletes
the catalogue row. The client keeps the transfer, the infohash and the history;
the processor keeps its hand-off. Add the title again and it will not download,
and until PR #432 nothing said why. That is not a missing feature at the edge —
it is the direct cause of the scenario at the top of this page.

---

## The three questions

These are not "delete scenarios". Every case is a combination of three
questions, and only the third is about deleting.

1. **What does Deluno believe?** The wanted row: `HasFile`, quality, monitored,
   exclusion.
2. **What is actually there?** The file on disk.
3. **Who else is holding a copy?** The download client's transfer, the download
   client's *history*, the processor's hand-off.

**Question 2 has no answer today.** There is no `File.Exists` anywhere in
`Deluno.Movies` or `Deluno.Media`, and no endpoint that re-checks a library
file — the route inventory has metadata refresh and subtitle rescan and nothing
else. `HasFile` is whatever the import last set. Delete a file outside Deluno
and the library still reports *Quality met*; the acquisition-blockers card
answers "already here at the quality you asked for" and offers no override,
because already-held is deliberately not clearable.

That is a defect, not a policy choice, and no table fixes it. It is a
**prerequisite**: question 2 must be answerable before the table below can be
evaluated.

---

## The outcomes a removal decides

Seven, and each one is independently right or wrong:

| # | Outcome | Values |
|---|---|---|
| 1 | The library file | keep · recycle bin · leave (already gone) |
| 2 | The client transfer | leave · remove · remove with data |
| 3 | The client's memory of the release | keep · forget |
| 4 | The processor hand-off | leave · reset to waiting |
| 5 | Import exclusion | none · add · remove |
| 6 | Deluno's wanted state | untouched · missing · covered · gone |
| 7 | A search | none · start one |

Outcomes 2 and 3 are separate on purpose, and that separation is the whole of
the re-download problem. A torrent client refuses a release because it still
holds the infohash, so removing the transfer *is* forgetting it. A usenet
client refuses it from history, which outlives the transfer — so on SABnzbd and
NZBGet, 2 and 3 are two different requests and doing only the first changes
nothing while reporting success.

---

## The table

Rows are what the person did. Columns are the seven outcomes.

| Trigger | 1 file | 2 transfer | 3 memory | 4 hand-off | 5 exclusion | 6 state | 7 search |
|---|---|---|---|---|---|---|---|
| **Remove, keep files** | keep | remove | forget | reset | none | gone | none |
| **Remove, delete files** | recycle bin | remove with data | forget | reset | none | gone | none |
| **Remove, and don't add it back** | per above | remove | forget | reset | add | gone | none |
| **Delete the file, keep the title** | recycle bin | remove with data | forget | reset | none | missing | start one |
| **Force a re-download** | keep | remove with data | forget | reset | remove | missing | start one |
| **Failed import, health remediation** | leave | remove | keep | leave | none | untouched | none |
| **File vanished off disk** (reconcile) | leave | leave | leave | leave | none | missing | start one |

Three rows deserve their reason stated.

**Remove → forget.** Today removal leaves the client holding everything, which
is what makes a re-add fail. Forgetting on removal is the change that stops the
scenario at the top of this page from arising in the first place; the force
button then exists for the cases removal did not cause.

**Health remediation keeps the client's memory.** It is the one row where the
release genuinely failed, and remembering it is the mechanism that stops an
endless re-download loop. Radarr is right about this. Clearing it here would
trade a silent failure for a loud one.

**"File vanished" touches nothing but Deluno.** A reconcile reports what it
found; it does not act on a client on the strength of a file being absent,
because "absent" has innocent causes — an unmounted share, a renamed folder, a
drive that has not spun up.

---

## What still needs deciding

1. **Does removing a title always forget it at the client, or only when the
   person also deleted the files?** The table says always. The argument against
   is a private tracker: forgetting means removing the transfer, which ends the
   seed and may cost ratio. The argument for is that leaving it is exactly what
   makes a re-add fail silently.
2. **Should a force warn that it stops a seed?** Deluno already knows enough to
   say so — `SharingFootprint` reasons about whether the client's copy and the
   library's are one set of data. Currently the confirmation says "along with
   its files" and never mentions seeding.
3. **Should the reconcile run on a schedule, on opening a title, or only when
   asked?** A per-title check is one `stat` and could run whenever a detail page
   or the blockers card is read. A library-wide sweep is a job.
4. **Does an unmonitored title get reconciled?** Deluno is not watching it, but
   the library grid still claims it holds a file.

---

## Finding the gaps, rather than noticing them

A table written by hand only covers the cases whoever wrote it thought of. The
scenario at the top of this page is exactly that failure: nobody sat down and
decided that removing a title should leave the client holding it — the case was
never enumerated at all.

So the table is not the source. **The axes are**, and the table is their
cross-product with a decision in each cell:

| Axis | Values |
|---|---|
| Trigger | remove · remove+delete · remove+exclude · delete file only · force · remediation · reconcile |
| Library file | present · absent · never existed |
| Client transfer | none · downloading · stalled · completed · seeding |
| Client memory | none · history entry · still holds infohash |
| Hand-off | none · open · finished · failed |
| Monitored | yes · no |

Not every cell is reachable, and most collapse. The point is that the reachable
ones are **enumerable**, so a case that has never been decided is discoverable
by machine rather than by somebody remembering it at 1am. The table-driven test
below asserts that every reachable combination matches exactly one row: a cell
matching none is a gap, and a cell matching two is a contradiction. Both fail
the build.

That is the difference between a design that decays and one that keeps working:
when someone adds a seventh trigger or a client with a new state, the missing
cells announce themselves.

### The register of what we do not know

| # | Unknown | How we would find out |
|---|---|---|
| 1 | Does removing a title while it is still downloading leave an orphan in the client for ever? | Drive it on the rig: add, grab, remove mid-download, look at qBittorrent |
| 2 | Does SABnzbd's duplicate check key on the nzo id we store, or on the release name? | Its history delete takes the id; the *duplicate* check may not. Needs a SABnzbd instance — the lab has none |
| 3 | Does deleting the client's payload ever take the library file with it? | Only if they are one hardlink and the client deletes by inode. `SharingFootprint` knows whether they are one copy; the behaviour needs proving on the rig |
| 4 | What does the processor do with a hand-off it has already answered? | Now answerable — the idempotency key carries the row revision, so it should re-process. Unproven against MediaMop |
| 5 | Is a completed dispatch enough to conclude "the client still refuses this"? | No — the client may have been cleared by hand. Which is why the blocker must say "may still hold" rather than "holds" |

Number 5 is a design constraint rather than a question: **Deluno cannot see a
download client's memory without asking it, and it may not be reachable.** So
the "previously downloaded" blocker states what Deluno knows — that it fetched
this once and no longer holds the file — and offers the override, rather than
claiming a fact about the client it has not verified.

---

## Better ways to deliver the outcome

Each of these questions the mechanism rather than the table. Two are worth
doing; one is worth deciding against on the record.

### A. Decide from Deluno's own record, not the client's refusal — **recommended**

The blockers reader currently asks the world what is in the way. But Deluno
already stores every dispatch it ever made, so it can answer "I fetched this
before and no longer have the file" from its own tables — with no client, no
network, and no chance of a wrong answer because a client is offline.

This is strictly better and it is what makes row 2 of the work order possible.
The client's memory still has to be *cleared* to re-download; it just should
not be what Deluno *consults* to explain.

### B. Remember the release that failed, not the title — **recommended**

Radarr blocklists a release. Deluno's exclusion is title-level, which is a
blunter instrument than the problem: a failed import means *this file was bad*,
not *this film is bad*.

If Deluno recorded failure per release — name, indexer, infohash — the next
search could skip that release and take the next candidate **without anyone
being told anything**. That turns "why will this not download" into a question
that never gets asked, which beats answering it well. The force button then
exists only for the cases a person genuinely has to overrule.

This is the single biggest improvement available here, and it is larger than
the rest of this document.

### C. File identity instead of file path — **worth doing, later**

`HasFile` plus a path cannot tell *deleted* from *moved* from *renamed*. If
import recorded a durable identity — size, and the hardlink or inode where the
platform offers one — a reconcile could re-link a moved file instead of
declaring it missing and downloading it again.

The better outcome is not "we noticed it went and fetched another one". It is
"it moved, and Deluno followed it". That saves the download entirely, and it is
the difference between a tool that copes and one that pays attention.

### D. Make removal reversible instead of careful — **decided against**

Removal could be a transaction undoable for some days: catalogue row
soft-deleted, files already in the recycle bin, client state recorded well
enough to re-establish. Most of the risk in the table above would evaporate,
because mistakes would be recoverable rather than prevented.

Against it: the recycle bin already covers the irreversible half — the files —
and it has a restore. What remains recoverable-in-principle is a catalogue row,
which is cheap to recreate by adding the title again. Adding a second
undo system to protect the cheap half would be machinery earning its keep only
on the day somebody makes a mistake, and it would sit between every removal and
its outcome for ever. **Not doing this** is the decision; the recycle bin is
the recovery story.

---

## How this gets enforced

A document that five code paths can ignore is the problem, not the fix.

- **One policy object** — the table above expressed once, in code, as the only
  place the seven outcomes are decided.
- **Every path routed through it**: both removal endpoints, health remediation,
  `ReclaimCompletedAsync`, and force-redownload. The verbs come from
  `DownloadClientActions`, not from string literals.
- **A table-driven test** that walks every row and asserts all seven outcomes,
  in the manner of `gold-stays-gold` and `light-theme-brightness` — reading the
  real thing rather than a copy of it, so a row cannot drift without failing.
- **The ownership check applies to all of them**, including the force. It is
  currently reachable only from health remediation, which is the path a person
  did not ask for.

## Order of work

1. **The reconcile.** Question 2 has to be answerable first; until it is, every
   answer the blockers card gives about a deleted title is confidently wrong.
2. **The "previously downloaded" blocker.** A completed dispatch is invisible to
   `AcquisitionBlockerSources.FindAsync` today, which is why the exact scenario
   at the top of this page produces no card and no button.
3. **The policy and the routing**, absorbing the seeding warning and the shared
   ownership check.
